// PreToolUse guard for every tool: what only the owner does to a trunk and its pull requests, enforced in code rather
// than left to a session's judgement or the auto-mode classifier (factory blockers, Nigel 2026-11-25).
//
// Refused outright, whoever asks:
//   completing, merging, approving, abandoning or auto-completing a pull request, or bypassing a branch policy
//   (gh pr merge, gh pr review --approve, az repos pr update/set-vote, a REST call that sets a pull request's status,
//   an MCP tool that merges or approves one);
//   pushing to main, master or trunk, or force-pushing to one.
// Asked of the owner, so an unattended card parks instead of doing it:
//   deleting a branch, local or remote;
//   force-pushing any other branch, --force-with-lease included.
// Everything else passes, including an ordinary push of a workstream branch or a project's primary.
//
// A tripwire for cooperating sessions, like the lock and holdout guards: it reads the command line, not what a script
// it runs does.

const { execFileSync } = require('child_process');

const SHELLS = new Set(['Bash', 'PowerShell']);
const TRUNKS = new Set(['main', 'master', 'trunk']);

function decide(decision, reason) {
    process.stdout.write(JSON.stringify({
        hookSpecificOutput: {
            hookEventName: 'PreToolUse',
            permissionDecision: decision,
            permissionDecisionReason: reason,
        },
    }));
    process.exit(0);
}

const deny = reason => decide('deny', `${reason} Only Nigel does this; say so in your report and carry on with the rest.`);
const ask = reason => decide('ask', reason);

// The commands of a command line, split at && || ; | and newlines, each as its words without quotes.
function commands(line) {
    return line.split(/&&|\|\||[;|\n]/)
        .map(part => (part.match(/"[^"]*"|'[^']*'|\S+/g) || []).map(word => word.replace(/^["']|["']$/g, '')))
        .filter(words => words.length > 0);
}

// The words after "git" and any -C <dir> or -c <x>, with the folder -C named.
function gitArguments(words) {
    const at = words.findIndex(word => /(^|[\\/])git(\.exe)?$/i.test(word));
    if (at < 0) {
        return null;
    }
    let folder = null;
    let index = at + 1;
    while (index < words.length && /^-[Cc]$/.test(words[index])) {
        if (words[index] === '-C') {
            folder = words[index + 1];
        }
        index += 2;
    }
    return { folder, rest: words.slice(index) };
}

function branchOf(ref) {
    const target = ref.includes(':') ? ref.slice(ref.lastIndexOf(':') + 1) : ref;
    return target.replace(/^\+/, '').replace(/^refs\/heads\//, '');
}

function currentBranch(folder, cwd) {
    try {
        return execFileSync('git', ['-C', folder || cwd || '.', 'symbolic-ref', '--short', 'HEAD'],
            { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'], timeout: 5000 }).trim();
    } catch {
        return '';
    }
}

function checkPush(rest, folder, cwd) {
    const options = rest.filter(word => word.startsWith('-'));
    const positional = rest.filter(word => !word.startsWith('-'));
    const refs = positional.slice(1);
    const forced = options.some(option => /^(--force(-with-lease)?(=.*)?|--force-if-includes|-[a-z]*f[a-z]*)$/.test(option))
        || refs.some(ref => ref.startsWith('+'));

    if (options.some(option => option === '--delete' || /^-[a-z]*d[a-z]*$/.test(option)) || refs.some(ref => ref.startsWith(':'))) {
        ask('Deleting a remote branch is Nigel\'s call.');
    }
    if (options.some(option => option === '--mirror' || option === '--all')) {
        deny('A push of every branch at once can reach the trunk.');
    }

    const targets = refs.length > 0 ? refs.map(branchOf) : [currentBranch(folder, cwd)];
    const trunk = targets.find(target => TRUNKS.has(target.toLowerCase()));

    if (trunk) {
        deny(`Pushing to ${trunk} goes through a pull request that Nigel completes.`);
    }
    if (forced) {
        ask('A force-push rewrites history others may have built on.');
    }
}

function checkGit(words, cwd) {
    const git = gitArguments(words);
    if (!git || git.rest.length === 0) {
        return;
    }
    const [verb, ...rest] = git.rest;

    if (verb === 'push') {
        checkPush(rest, git.folder, cwd);
    }
    if (verb === 'branch' && rest.some(word => /^(-[a-zA-Z]*[dD][a-zA-Z]*|--delete)$/.test(word))) {
        ask('Deleting a branch is Nigel\'s call.');
    }
}

function checkPullRequestCli(words) {
    const line = words.join(' ').toLowerCase();

    if (/\bgh\s+pr\s+merge\b/.test(line) || /\bgh\s+pr\s+review\b.*--approve\b/.test(line)
        || /\bgh\s+api\b.*\/pulls\/\d+\/merge\b/.test(line)) {
        deny('Merging or approving a pull request is Nigel\'s.');
    }
    if (/\baz\s+repos\s+pr\s+set-vote\b/.test(line)) {
        deny('Voting on a pull request is Nigel\'s.');
    }
    if (/\baz\s+repos\s+pr\s+update\b/.test(line)
        && /--status\s+(completed|abandoned)|--auto-complete\s+(true|yes)|--bypass-policy\s+(true|yes)/.test(line)) {
        deny('Completing, abandoning or auto-completing a pull request, or bypassing its policy, is Nigel\'s.');
    }
}

// A REST call, by any client, that completes, abandons, auto-completes or force-merges a pull request.
function checkRest(line) {
    const lower = line.toLowerCase();
    if (!/pullrequests|\/pulls\//.test(lower)) {
        return;
    }
    if (/"status"\s*:\s*\\?"?(completed|abandoned)|autocompletesetby|bypasspolicy|\/merge\b|\bvote\b/.test(lower)) {
        deny('Completing, abandoning, merging or voting on a pull request is Nigel\'s.');
    }
}

let raw = '';
process.stdin.on('data', chunk => { raw += chunk; });
process.stdin.on('end', () => {
    let tool = '';
    let input = {};
    let cwd = '';
    try {
        const parsed = JSON.parse(raw);
        tool = parsed.tool_name || '';
        input = parsed.tool_input || {};
        cwd = typeof parsed.cwd === 'string' ? parsed.cwd : '';
    } catch {
        process.exit(0);
    }

    if (/(merge|complete|approve|abandon)_?pull_?request|pull_?request_?(merge|complete|approve|abandon)/i.test(tool)) {
        deny(`${tool} acts on a pull request in a way only Nigel does.`);
    }

    if (SHELLS.has(tool) && typeof input.command === 'string') {
        for (const words of commands(input.command)) {
            checkGit(words, cwd);
            checkPullRequestCli(words);
        }
        checkRest(input.command);
    }

    process.exit(0);
});
