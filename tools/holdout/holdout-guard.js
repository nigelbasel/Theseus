// PreToolUse guard for every tool: keeps the holdout scenarios independent of the work they judge.
//
// The holdout folder (and, optionally, its remote address) comes from Theseus's settings, never from this file, so
// the repo names nobody's machine (method rule P19). With no folder chosen, the holdout is the default one, a folder
// per project under Theseus's own data folder, holdouts\<project>, and the guard covers that whole folder exactly as it
// covers a chosen one (1.1e AC 6c).
//
// By role, from the session's environment, which its own commands cannot change (verifier-card, Nigel 2026-11-19):
//   no role                    a tool naming the holdout is refused;
//   THESEUS_ROLE=verifier      may read, search and list the holdout, and write only in its results folder; may not
//                              write anywhere else, nor run a shell command naming the holdout;
//   THESEUS_HOLDOUT_AUTHOR=1   may do anything in the holdout; not write Theseus's files, the guard or its hook.
//
// For every role but author, no shell command runs with its working directory inside the holdout, and no file tool
// writes Theseus's own settings or board.
//
// A tripwire for accidents by cooperating sessions, like the lock guard: it reads the fields a tool acts on (paths,
// commands, URLs, patterns), each word of a shell command as a path from the working directory, and the working
// directory itself, not file contents; a shell command in the code repos that reaches no holdout is not policed. A search rooted above the holdout (a parent folder) is not refused, so it can return scenario lines: a
// known limit (verifier-card review S2).

const fs = require('fs');
const os = require('os');
const path = require('path');

// macOS and Linux have no drive letters and no Theseus: a holdout there is a <repo>-holdout folder, or one an absolute
// POSIX path in a settings file names.
const POSIX = process.platform !== 'win32';

const PATH_FIELDS = ['file_path', 'path', 'notebook_path'];
const TEXT_FIELDS = ['command', 'url', 'glob', 'pattern'];
const WRITING = new Set(['Write', 'Edit', 'MultiEdit', 'NotebookEdit']);
const READING = new Set(['Read', 'Grep', 'Glob', 'LS']);
const SEARCHING_FROM_CWD = new Set(['Grep', 'Glob']);
const SHELLS = new Set(['Bash', 'PowerShell']);

// Theseus's own data folder, where its settings and board live.
function theseusFolder() {
    if (POSIX && !process.env.LOCALAPPDATA) {
        return path.join(os.homedir(), '.theseus');
    }
    const local = process.env.LOCALAPPDATA || path.join(process.env.USERPROFILE || '', 'AppData', 'Local');
    return path.join(local, 'Theseus');
}

function settingsPath() {
    return process.env.THESEUS_SETTINGS || path.join(theseusFolder(), 'settings.json');
}

function readHoldout() {
    try {
        // A settings file saved with a byte order mark is still a valid one (verify part 1, consider).
        const settings = JSON.parse(fs.readFileSync(settingsPath(), 'utf8').replace(/^﻿/, ''));
        const chosen = typeof settings.holdoutFolder === 'string' ? folderPath(settings.holdoutFolder) : '';
        const folder = chosen || defaultHoldouts();
        const isDefault = !chosen;
        const remote = typeof settings.holdoutRemote === 'string' ? remotePath(settings.holdoutRemote) : '';
        return { folder, remote, isDefault, listed: listedFolders() };
    } catch {
        return { folder: defaultHoldouts(), remote: '', isDefault: true, listed: listedFolders() };
    }
}

// A folder setting as the folder it names, or '' when it names none: a relative path, a whole drive, or a rooted path
// with no drive, which would refuse unrelated work everywhere or guard whichever drive the guard runs on (verifier-card
// review S3; re-verify C1). A Git Bash form is the folder it stands for.
function folderPath(typed) {
    const written = windowsPaths(typed.trim().replace(/\\/g, '/'));
    if (!/^([a-z]:\/|\/\/[^/])/i.test(written) && !(POSIX && /^\/[^/]/.test(written))) {
        return '';
    }
    const resolved = path.win32.resolve(written);
    return resolved === path.win32.parse(resolved).root ? '' : resolved;
}

// A remote setting as host/owner/repo, whichever form was pasted: https://, ssh://, user@, host:owner, a trailing .git
// (verify part 1 attempt 2, C13).
// A remote that is a folder on disk is kept as typed: its drive's colon is not a host's (review of this change, S4).
function remotePath(typed) {
    const trimmed = typed.trim();
    if (isLocalPath(trimmed)) {
        return trimmed.replace(/[\\/]+$/, '').replace(/\.git$/i, '');
    }
    return trimmed.replace(/^[a-z][a-z0-9+.-]*:\/\//i, '').replace(/^[^@/]+@/, '').replace(/:(?!\d)/, '/')
        .replace(/\/+$/, '').replace(/\.git$/i, '');
}

function isLocalPath(text) {
    return /^([a-z]:[\\/]|[\\/]{2}[^\\/]|\/(mnt\/)?[a-z]\/)/i.test(text);
}

// The owner/repo part of a remote, which gh and the hosts' APIs take alone; '' for a remote too short to have one, or a
// folder on disk.
function ownerAndRepo(remote) {
    if (isLocalPath(remote)) {
        return '';
    }
    const parts = remote.split('/').filter(part => part.length > 0);
    return parts.length >= 3 ? parts.slice(-2).join('/') : '';
}

// The holdout folders in force, one per project, as Theseus last wrote them (1.1e AC 6e): chosen in Options, found beside
// a project's home repo, or Theseus's own. Each is protected, so the board and the guard agree. None listed, or a file
// that does not read, leaves the settings' folder or the default alone, as before.
function listedFolders() {
    try {
        const listed = JSON.parse(fs.readFileSync(path.join(theseusFolder(), 'holdout-folders.json'), 'utf8').replace(/^\uFEFF/, ''));
        // Each entry is judged on its own, so one malformed entry cannot drop the rest (review of PR 7, finding 8).
        return (Array.isArray(listed.folders) ? listed.folders : [])
            .filter(entry => entry && typeof entry === 'object')
            .map(entry => typeof entry.folder === 'string' ? folderPath(entry.folder) : '')
            .filter(folder => folder);
    } catch {
        return [];
    }
}

// The <repo>-holdout folder a path is in, by convention, or '' (1.1e AC 6e).
function conventionalFolder(target) {
    const parts = path.win32.resolve(target).split('\\');
    const index = parts.findIndex(part => /-holdout$/i.test(part));
    return index < 0 ? '' : parts.slice(0, index + 1).join('\\');
}

// Where the default holdout folders are, one per project, when the owner chose none (1.1e AC 6c).
function defaultHoldouts() {
    return path.win32.join(theseusFolder(), 'holdouts');
}

// Git Bash writes C:\x as /c/x (verify part 1, S2), and WSL as /mnt/c/x (attempt 2, C12).
function windowsPaths(text) {
    return text.replace(/(^|[\s'"=(<>|;&])\/(?:mnt\/)?([a-z])(?=\/|$)/gi, '$1$2:');
}

// A Bash-escaped space ("My\ Holdout") is a space, not a folder break (attempt 2, C12).
function normalise(value) {
    return windowsPaths(value.replace(/\\ /g, ' ').replace(/\\/g, '/')).replace(/\/+/g, '/').replace(/\/$/, '').toLowerCase();
}

const NAME_CHARACTER = /[a-z0-9_-]/;

// Whether text names the location, or anything inside it, as a path, a URL or an SSH remote (host:owner/repo). A
// name only counts whole: "holdout" names neither "holdout-notes" nor "notexample.com/owner/holdout", while
// "holdout.git" is the same repo (verify part 1, S3).
function names(text, location, alone = false) {
    if (!location) {
        return false;
    }
    const needle = normalise(location);
    const written = normalise(text);
    for (const haystack of [written, written.replace(/:/g, '/')]) {
        let index = haystack.indexOf(needle);
        while (index !== -1) {
            const before = index === 0 ? '' : haystack.charAt(index - 1);
            const rest = haystack.slice(index + needle.length);
            // Alone, owner/repo after a host is that host's repo, which the whole remote decides; gh api's repos/ and a
            // raw-file host are not another repo's host.
            const wholeBefore = before === '' || !(NAME_CHARACTER.test(before) || before === '.'
                || (alone && (before === '/' || before === ':')
                    && !/(^|[^a-z0-9_.-])(repos|raw\.githubusercontent\.com)\/$/.test(haystack.slice(0, index))));
            const wholeAfter = rest === '' || !(NAME_CHARACTER.test(rest.charAt(0)) || rest.charAt(0) === '.')
                || /^\.git(?![a-z0-9_.-])/.test(rest);
            if (wholeBefore && wholeAfter) {
                return true;
            }
            index = haystack.indexOf(needle, index + 1);
        }
    }
    return false;
}

// A path as the tool will use it: relative to the session's working directory, with any .. resolved, so that
// "results\..\scenarios" is not under results and "scenarios\x" typed inside the holdout is in it (M1, S4).
function resolve(value, cwd) {
    const written = windowsPaths(value.replace(/\\/g, '/'));
    return path.win32.isAbsolute(written) || !cwd ? path.win32.resolve(written) : path.win32.resolve(cwd, written);
}

// A shell command with the variables a session would use to reach Theseus's data folder or its home written out, in
// Bash, PowerShell and cmd forms, and a leading ~ (attempt 2, C6).
function expandVariables(command) {
    const known = name => process.env[name] || (name === 'HOME' ? process.env.USERPROFILE : '') || '';
    return command
        .replace(/\$\{env:(LOCALAPPDATA|USERPROFILE|HOME|APPDATA)\}/gi, (_, name) => known(name.toUpperCase()))
        .replace(/\$env:(LOCALAPPDATA|USERPROFILE|HOME|APPDATA)\b/gi, (_, name) => known(name.toUpperCase()))
        .replace(/\$\{?(LOCALAPPDATA|USERPROFILE|HOME|APPDATA)\}?(?![a-z0-9_])/gi, (_, name) => known(name.toUpperCase()))
        .replace(/%(LOCALAPPDATA|USERPROFILE|HOME|APPDATA)%/gi, (_, name) => known(name.toUpperCase()))
        .replace(/(^|[\s'"=(<>|;&])~(?=[\\/]|$|\s)/g, (_, lead) => lead + known('HOME'));
}

// The words of a shell command, as the shell would split them: quoted text kept whole, in Bash an escaped space kept in
// its word (in PowerShell a backslash before a space ends a folder, as tab completion writes it), and operators and
// redirections as breaks (attempt 2, C11).
function shellWords(command, bash) {
    const words = [];
    let word = '';
    let quote = '';
    for (let index = 0; index < command.length; index++) {
        const character = command.charAt(index);
        if (quote) {
            if (character === quote) {
                quote = '';
            } else {
                word += character;
            }
        } else if (character === '"' || character === '\'') {
            quote = character;
        } else if (bash && character === '\\' && command.charAt(index + 1) === ' ') {
            word += ' ';
            index++;
        } else if (/[\s;&|<>()=,`]/.test(character)) {
            if (word) {
                words.push(word);
            }
            word = '';
        } else {
            word += character;
        }
    }
    if (word) {
        words.push(word);
    }
    return words.filter(candidate => !candidate.includes('$') && !/^[a-z][a-z0-9+.-]*:\/\//i.test(candidate));
}

const CHANGES_FOLDER = /^(cd|chdir|pushd|set-location|sl|push-location)$/i;

// The paths a shell command's words reach, read from where each runs: a cd earlier in the command moves the rest
// (review of this change, S3). Only a word written as a path counts (it has a separator or a file extension, starts with
// . or ~, or is the folder a cd goes to), so a bare word that happens to be a holdout's name, as in git commit -m hold, is not one (S1).
// A PowerShell parameter written -Name:value is its value (C4).
function shellPaths(command, bash, cwd) {
    const paths = [];
    let here = cwd;
    let moving = false;
    for (const typed of shellWords(command, bash)) {
        const word = typed.replace(/^-[a-z]+:(?=.)/i, '');
        if (CHANGES_FOLDER.test(word)) {
            moving = true;
            continue;
        }
        if (moving && word.startsWith('-')) {
            continue;
        }
        if (moving || /[\\/]/.test(word) || /^[.~]/.test(word) || /\.[a-z0-9]+$/i.test(word)) {
            const reached = resolve(word, here);
            paths.push(reached);
            if (moving) {
                here = reached;
            }
        }
        moving = false;
    }
    return paths;
}

// Whether a resolved path is the folder or inside it. "results old" is not inside "results".
function under(target, folder) {
    if (!target || !folder) {
        return false;
    }
    const inside = normalise(path.win32.resolve(target));
    const container = normalise(path.win32.resolve(folder));
    return inside === container || inside.startsWith(container + '/');
}

function deny(reason) {
    process.stdout.write(JSON.stringify({
        hookSpecificOutput: {
            hookEventName: 'PreToolUse',
            permissionDecision: 'deny',
            permissionDecisionReason: reason,
        },
    }));
    process.exit(0);
}

let raw = '';
process.stdin.on('data', chunk => { raw += chunk; });
process.stdin.on('end', () => {
    const holdout = readHoldout();

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
    if (SHELLS.has(tool) && typeof input.command === 'string') {
        input = { ...input, command: expandVariables(input.command) };
    }

    // Theseus's own settings decide the holdout, and its board decides a card's role, so no session writes them with the
    // file tools (verifier-card review S5); a holdout is always in force since the default (1.1e AC 6c). Shell commands
    // are not policed here: a tripwire, not a wall.
    if (WRITING.has(tool)) {
        const target = input.file_path || input.notebook_path || '';
        const own = theseusFolder();
        if (target && [path.win32.join(own, 'settings.json'), path.win32.join(own, 'board.json'),
            path.win32.join(own, 'holdout-folders.json')]
            .some(file => normalise(resolve(target, cwd)) === normalise(file))) {
            deny('Theseus\'s settings, board and holdout folders are changed from Theseus, not by a session.');
        }
        // The installed guard and the hook that runs it: a session that rewrote either would switch the guard off.
        // Theseus installs them (Options, Holdouts); the rest of the Claude Code folder stays writable (Nigel,
        // 2026-11-22: guard and hooks only).
        const config = process.env.CLAUDE_CONFIG_DIR || path.join(process.env.USERPROFILE || os.homedir(), '.claude');
        const written = target ? resolve(target, cwd) : '';
        if (written && (normalise(written) === normalise(path.win32.join(config, 'settings.json'))
            || (under(written, config) && /^holdout-guard\.js(\.previous)?$/i.test(path.win32.basename(written))))) {
            deny('The holdout guard and the Claude Code hook that runs it are installed from Theseus (Tools > Options, '
                + 'Holdouts), not by a session.');
        }
    }

    // An author writes the scenarios, so the holdout itself is open to it; the files above are not (review of the
    // install, S4).
    if (process.env.THESEUS_HOLDOUT_AUTHOR === '1') {
        process.exit(0);
    }

    // Where the tool acts: its path fields resolved against the working directory, and the working directory itself
    // for a search given no path. The session's working directory is not something its own commands name, which is
    // how a shell command inside the holdout went unchecked (verify part 1, M1).
    const located = PATH_FIELDS.map(field => input[field]).filter(value => typeof value === 'string' && value)
        .map(value => resolve(value, cwd));
    if (SEARCHING_FROM_CWD.has(tool) && !input.path && cwd) {
        located.push(cwd);
    }
    // A shell command's words, and a Glob pattern, read as paths from where they run: a relative route is otherwise
    // invisible, since it names no holdout (attempt 2, C11).
    const reached = SHELLS.has(tool) && typeof input.command === 'string'
        ? shellPaths(input.command, tool === 'Bash', cwd)
        : tool === 'Glob' && typeof input.pattern === 'string'
            ? [resolve(input.pattern, input.path ? resolve(input.path, cwd) : cwd)]
            : [];
    located.push(...reached);

    const texts = [...PATH_FIELDS, ...TEXT_FIELDS].map(field => input[field]).filter(value => typeof value === 'string');
    // A folder named <repo>-holdout is a holdout by convention wherever it is, so one beside a repo is protected even when
    // a session names it relative to its own folder, or before Theseus has listed it (review of PR 7, findings 5 and 6).
    const conventional = /(^|[\\/\s'"=(<>|;&:])[^\\/\s'"=(<>|;&:]*-holdout(?=$|[\\/\s'"=(<>|;&:])/i;
    const folders = [holdout.folder, ...holdout.listed];
    const touches = located.some(where => folders.some(folder => under(where, folder)) || conventional.test(normalise(where)))
        || texts.some(text => folders.some(folder => names(text, folder)) || names(text, holdout.remote)
            || names(text, ownerAndRepo(holdout.remote), true) || conventional.test(text));
    // A chosen holdout has one results folder; the default has one per project, holdouts\<project>\results (review of
    // 1.1e PR 2b, finding 1), since each project's verifier writes its report beside its own scenarios.
    function inResults(target) {
        // A listed folder has its own results folder (1.1e AC 6e); the deepest that holds the target is its holdout.
        const listed = holdout.listed.filter(folder => under(target, folder)).sort((a, b) => b.length - a.length)[0]
            || conventionalFolder(target);
        if (listed) {
            const results = path.win32.join(listed, 'results');
            return under(target, results) && normalise(target) !== normalise(results);
        }
        if (!holdout.isDefault) {
            const results = path.win32.join(holdout.folder, 'results');
            return under(target, results) && normalise(target) !== normalise(results);
        }
        if (!under(target, holdout.folder)) {
            return false;
        }
        const parts = normalise(target).slice(normalise(holdout.folder).length).split('/').filter(part => part.length > 0);
        return parts.length >= 3 && parts[1] === 'results';
    }
    const verifier = process.env.THESEUS_ROLE === 'verifier';

    // No shell command runs inside the holdout, for any role but author: there is no telling what it would change.
    // A verifier reads the scenarios with the file tools and runs its builds in the code it checks.
    // Theseus's own files are not changed by a shell command either: deleting the holdout list would drop its folders
    // from the guard until Theseus wrote it again (review of PR 7, finding 7).
    const ownFiles = ['settings.json', 'board.json'].map(file => path.win32.join(theseusFolder(), file));
    if (SHELLS.has(tool) && (texts.some(text => /holdout-folders\.json/i.test(text)
        || ownFiles.some(file => names(text, file)))
        || reached.some(word => ownFiles.some(file => normalise(word) === normalise(file))))) {
        deny('Theseus\'s settings, board and holdout folders are changed from Theseus, not by a session.');
    }

    if (SHELLS.has(tool) && cwd && (folders.some(folder => under(cwd, folder)) || conventional.test(normalise(cwd)))) {
        deny('No shell command runs inside the holdout folder. Read the scenarios with the file tools, and run builds '
            + 'and tests from the code\'s own folder.');
    }

    if (!verifier) {
        if (touches) {
            deny('This touches the holdout scenarios. Implementing sessions must not read or change them: they are '
                + 'the independent check on your work. Do not try another route to the same files. If a scenario '
                + 'needs changing or explaining, tell the owner.');
        }
        process.exit(0);
    }

    if (WRITING.has(tool)) {
        const target = input.file_path || input.notebook_path || '';
        if (!target || !inResults(resolve(target, cwd))) {
            deny('A verifier writes only its report, in the holdout\'s results folder. It changes no scenario and '
                + 'nothing in the code it checks.');
        }
        process.exit(0);
    }

    if (touches && !READING.has(tool)) {
        deny('A verifier reads the holdout scenarios with the file tools, and does not run shell commands on them.');
    }

    process.exit(0);
});
