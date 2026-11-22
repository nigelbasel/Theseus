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
//   THESEUS_HOLDOUT_AUTHOR=1   may do anything.
//
// For every role but author, no shell command runs with its working directory inside the holdout, and no file tool
// writes Theseus's own settings or board.
//
// A tripwire for accidents by cooperating sessions, like the lock guard: it reads the fields a tool acts on (paths,
// commands, URLs, patterns) and the working directory, not file contents, and shell commands in the code repos are not
// policed. A search rooted above the holdout (a parent folder) is not refused, so it can return scenario lines: a
// known limit (verifier-card review S2).

const fs = require('fs');
const path = require('path');

const PATH_FIELDS = ['file_path', 'path', 'notebook_path'];
const TEXT_FIELDS = ['command', 'url', 'glob', 'pattern'];
const WRITING = new Set(['Write', 'Edit', 'MultiEdit', 'NotebookEdit']);
const READING = new Set(['Read', 'Grep', 'Glob', 'LS']);
const SEARCHING_FROM_CWD = new Set(['Grep', 'Glob']);
const SHELLS = new Set(['Bash', 'PowerShell']);

// Theseus's own data folder, where its settings and board live.
function theseusFolder() {
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
        const typed = typeof settings.holdoutFolder === 'string' ? settings.holdoutFolder.trim() : '';
        // A folder that is not an absolute path, or is a whole drive, would refuse unrelated work everywhere: it
        // counts as unset (verifier-card review S3).
        const resolved = typed && path.win32.isAbsolute(typed) ? path.win32.resolve(typed) : '';
        const chosen = resolved && resolved !== path.win32.parse(resolved).root ? resolved : '';
        const folder = chosen || defaultHoldouts();
        const isDefault = !chosen;
        const remote = typeof settings.holdoutRemote === 'string' ? settings.holdoutRemote.trim() : '';
        return { folder, remote, isDefault, listed: listedFolders() };
    } catch {
        return { folder: defaultHoldouts(), remote: '', isDefault: true, listed: listedFolders() };
    }
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
            .map(entry => typeof entry.folder === 'string' ? entry.folder.trim() : '')
            .filter(folder => folder && path.win32.isAbsolute(folder) && path.win32.resolve(folder) !== path.win32.parse(path.win32.resolve(folder)).root)
            .map(folder => path.win32.resolve(folder));
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

// Git Bash writes C:\x as /c/x (verify part 1, S2).
function windowsPaths(text) {
    return text.replace(/(^|[\s'"=(<>|;&])\/([a-z])\//gi, '$1$2:/');
}

function normalise(value) {
    return windowsPaths(value.replace(/\\/g, '/')).replace(/\/+/g, '/').replace(/\/$/, '').toLowerCase();
}

const NAME_CHARACTER = /[a-z0-9_-]/;

// Whether text names the location, or anything inside it, as a path, a URL or an SSH remote (host:owner/repo). A
// name only counts whole: "holdout" names neither "holdout-notes" nor "notexample.com/owner/holdout", while
// "holdout.git" is the same repo (verify part 1, S3).
function names(text, location) {
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
            const wholeBefore = before === '' || !(NAME_CHARACTER.test(before) || before === '.');
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
    if (process.env.THESEUS_HOLDOUT_AUTHOR === '1') {
        process.exit(0);
    }

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
    }

    // Where the tool acts: its path fields resolved against the working directory, and the working directory itself
    // for a search given no path. The session's working directory is not something its own commands name, which is
    // how a shell command inside the holdout went unchecked (verify part 1, M1).
    const located = PATH_FIELDS.map(field => input[field]).filter(value => typeof value === 'string' && value)
        .map(value => resolve(value, cwd));
    if (SEARCHING_FROM_CWD.has(tool) && !input.path && cwd) {
        located.push(cwd);
    }

    const texts = [...PATH_FIELDS, ...TEXT_FIELDS].map(field => input[field]).filter(value => typeof value === 'string');
    // A folder named <repo>-holdout is a holdout by convention wherever it is, so one beside a repo is protected even when
    // a session names it relative to its own folder, or before Theseus has listed it (review of PR 7, findings 5 and 6).
    const conventional = /(^|[\\/\s'"=(<>|;&:])[^\\/\s'"=(<>|;&:]*-holdout(?=$|[\\/\s'"=(<>|;&:])/i;
    const folders = [holdout.folder, ...holdout.listed];
    const touches = located.some(where => folders.some(folder => under(where, folder)) || conventional.test(normalise(where)))
        || texts.some(text => folders.some(folder => names(text, folder)) || names(text, holdout.remote) || conventional.test(text));
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
    if (SHELLS.has(tool) && texts.some(text => /holdout-folders\.json/i.test(text)
        || ['settings.json', 'board.json'].some(file => names(text, path.win32.join(theseusFolder(), file))))) {
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
