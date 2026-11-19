// PreToolUse guard for every tool: keeps the holdout scenarios independent of the work they judge.
//
// The holdout folder (and, optionally, its remote address) comes from Theseus's settings, never from this file, so
// the repo names nobody's machine (method rule P19). With no holdout set, it allows everything.
//
// By role, from the session's environment, which its own commands cannot change (verifier-card, Nigel 2026-11-19):
//   no role                    a tool naming the holdout is refused;
//   THESEUS_ROLE=verifier      may read, search and list the holdout, and write only in its results folder; may not
//                              write anywhere else, nor run a shell command naming the holdout;
//   THESEUS_HOLDOUT_AUTHOR=1   may do anything.
//
// A tripwire for accidents by cooperating sessions, like the lock guard: it reads the fields a tool acts on (paths,
// commands, URLs, patterns), not file contents, and shell commands in the code repos are not policed.

const fs = require('fs');
const path = require('path');

const FIELDS = ['file_path', 'path', 'notebook_path', 'command', 'url', 'glob', 'pattern'];
const WRITING = new Set(['Write', 'Edit', 'MultiEdit', 'NotebookEdit']);
const READING = new Set(['Read', 'Grep', 'Glob', 'LS']);

function settingsPath() {
    if (process.env.THESEUS_SETTINGS) {
        return process.env.THESEUS_SETTINGS;
    }
    const local = process.env.LOCALAPPDATA || path.join(process.env.USERPROFILE || '', 'AppData', 'Local');
    return path.join(local, 'Theseus', 'settings.json');
}

function readHoldout() {
    try {
        const settings = JSON.parse(fs.readFileSync(settingsPath(), 'utf8'));
        const folder = typeof settings.holdoutFolder === 'string' ? settings.holdoutFolder.trim() : '';
        const remote = typeof settings.holdoutRemote === 'string' ? settings.holdoutRemote.trim() : '';
        return { folder, remote };
    } catch {
        return { folder: '', remote: '' };
    }
}

function normalise(value) {
    return value.replace(/\\/g, '/').replace(/\/+/g, '/').replace(/\/$/, '').toLowerCase();
}

// Whether text names the location: the location itself, or anything inside it. "holdout" does not name
// "holdout-notes".
function names(text, location) {
    if (!location) {
        return false;
    }
    const haystack = normalise(text);
    const needle = normalise(location);
    let index = haystack.indexOf(needle);
    while (index !== -1) {
        const after = haystack.charAt(index + needle.length);
        if (after === '' || after === '/' || !/[a-z0-9_.-]/.test(after)) {
            return true;
        }
        index = haystack.indexOf(needle, index + 1);
    }
    return false;
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
    if (process.env.THESEUS_HOLDOUT_AUTHOR === '1') {
        process.exit(0);
    }

    const holdout = readHoldout();
    if (!holdout.folder && !holdout.remote) {
        process.exit(0);
    }

    let tool = '';
    let input = {};
    try {
        const parsed = JSON.parse(raw);
        tool = parsed.tool_name || '';
        input = parsed.tool_input || {};
    } catch {
        process.exit(0);
    }

    const values = FIELDS.map(field => input[field]).filter(value => typeof value === 'string');
    const touches = values.some(value => names(value, holdout.folder) || names(value, holdout.remote));
    const results = holdout.folder ? path.join(holdout.folder, 'results') : '';
    const verifier = process.env.THESEUS_ROLE === 'verifier';

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
        if (!names(target, results)) {
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
