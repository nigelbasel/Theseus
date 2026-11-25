// Reports whether the agentic factory method is in place on this machine: what is installed and what is missing.
// Run by /agentic-factory:check, or directly: node <plugin>/plugin/scripts/check.js [project home folder]
// Exit code 0 when nothing is missing, 1 otherwise. Warnings do not fail it.

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const root = path.resolve(__dirname, '..', '..');
const results = [];

function report(state, what, detail) {
    results.push({ state, what, detail });
}

function run(command, args) {
    const result = spawnSync(command, args, { encoding: 'utf8', shell: process.platform === 'win32', timeout: 20000 });
    return result.status === 0 ? `${result.stdout || ''}`.trim() : null;
}

function atLeast(version, minimum) {
    const have = version.split('.').map(Number);
    const need = minimum.split('.').map(Number);
    for (let index = 0; index < need.length; index++) {
        if ((have[index] || 0) !== need[index]) {
            return (have[index] || 0) > need[index];
        }
    }
    return true;
}

function checkTools() {
    const node = process.versions.node;
    report(atLeast(node, '18.0.0') ? 'ok' : 'warn', 'Node', `${node}${atLeast(node, '18.0.0') ? '' : ' (18 or later recommended)'}`);

    const git = run('git', ['--version']);
    const gitVersion = git && (git.match(/(\d+\.\d+\.\d+)/) || [])[1];
    report(gitVersion ? 'ok' : 'missing', 'git', gitVersion || 'not found on PATH');

    const claude = run('claude', ['--version']);
    const claudeVersion = claude && (claude.match(/(\d+\.\d+\.\d+)/) || [])[1];
    if (!claudeVersion) {
        report('missing', 'Claude Code', 'claude not found on PATH');
    } else if (!atLeast(claudeVersion, '2.1.295')) {
        report('missing', 'Claude Code', `${claudeVersion}: 2.1.295 or later is needed, or a guard that cannot run lets the tool through. Run claude update`);
    } else {
        report('ok', 'Claude Code', claudeVersion);
        checkInstalled();
    }
}

// Whether this Claude Code configuration has the plugin installed and enabled, so its hooks run in every session.
function checkInstalled() {
    const listed = run('claude', ['plugin', 'list', '--json']);
    let plugins = [];
    try {
        const parsed = JSON.parse(listed || '[]');
        plugins = Array.isArray(parsed) ? parsed : (parsed.plugins || parsed.installed || []);
    } catch {
        plugins = [];
    }
    const ours = plugins.find(plugin => /^agentic-factory(@|$)/.test(`${plugin.id || plugin.name || ''}`));
    if (!ours) {
        report('missing', 'Plugin installed', 'agentic-factory is not in claude plugin list: see the getting-started guide');
    } else if (ours.enabled === false) {
        report('missing', 'Plugin enabled', `${ours.id || ours.name} is installed but disabled: claude plugin enable agentic-factory`);
    } else {
        report('ok', 'Plugin installed and enabled', `${ours.id || ours.name}${ours.version ? ` ${ours.version}` : ''}`);
    }
}

function checkFiles() {
    const files = [
        ['method/README.md', 'The method and its standing rules'],
        ['method/project.md', 'The project file format'],
        ['method/pipelines/feature.yaml', 'The feature pipeline'],
        ['method/pipelines/bug.yaml', 'The bug pipeline'],
        ['method/pipelines/freeform.yaml', 'The freeform pipeline'],
        ['method/starter/project.md', 'The starter project file'],
        ['tools/guards/trunk-guard.js', 'The trunk guard'],
        ['tools/holdout/holdout-guard.js', 'The holdout guard'],
    ];
    for (const [file, what] of files) {
        const there = fs.existsSync(path.join(root, file));
        report(there ? 'ok' : 'missing', what, there ? file : `${file} is not in the plugin: reinstall it`);
    }
    const readme = fs.existsSync(path.join(root, 'method/README.md')) ? fs.readFileSync(path.join(root, 'method/README.md'), 'utf8') : '';
    const version = (readme.match(/\*\*Version:\*\* `([^`]+)`/) || [])[1];
    if (version) {
        report('ok', 'Method version', version);
    }
}

function decision(guard, input, env) {
    const result = spawnSync(process.execPath, [path.join(root, guard)],
        { input: JSON.stringify(input), encoding: 'utf8', timeout: 20000, env: { ...process.env, ...env } });
    if (result.status !== 0) {
        return `exit ${result.status}`;
    }
    const match = `${result.stdout}`.match(/"permissionDecision":"([a-z]+)"/);
    return match ? match[1] : 'allow';
}

function checkGuards() {
    const here = process.cwd();
    const trunk = [
        ['deny', { tool_name: 'Bash', tool_input: { command: 'git push origin main' }, cwd: here }, 'a push to main is refused'],
        ['deny', { tool_name: 'Bash', tool_input: { command: 'gh pr merge 1 --squash' }, cwd: here }, 'merging a pull request is refused'],
        ['ask', { tool_name: 'Bash', tool_input: { command: 'git branch -D old' }, cwd: here }, 'deleting a branch asks the owner'],
        ['allow', { tool_name: 'Bash', tool_input: { command: 'git push origin you/a-workstream' }, cwd: here }, 'a workstream push passes'],
    ];
    const holdout = path.join(os.tmpdir(), 'app-holdout', 'scenarios', 'a.md');
    const code = path.join(os.tmpdir(), 'app', 'README.md');
    const holdouts = [
        ['deny', { tool_name: 'Read', tool_input: { file_path: holdout }, cwd: os.tmpdir() }, 'reading a <repo>-holdout folder is refused'],
        ['allow', { tool_name: 'Read', tool_input: { file_path: code }, cwd: os.tmpdir() }, 'reading code passes'],
    ];
    const failed = [];
    for (const [want, input, what] of trunk) {
        const got = decision('tools/guards/trunk-guard.js', input, {});
        if (got !== want) {
            failed.push(`trunk guard: ${what} (got ${got})`);
        }
    }
    for (const [want, input, what] of holdouts) {
        const got = decision('tools/holdout/holdout-guard.js', input, { THESEUS_ROLE: '', THESEUS_HOLDOUT_AUTHOR: '' });
        if (got !== want) {
            failed.push(`holdout guard: ${what} (got ${got})`);
        }
    }
    report(failed.length === 0 ? 'ok' : 'missing', 'Guards decide as they should',
        failed.length === 0 ? `${trunk.length + holdouts.length} sample calls` : failed.join('; '));
}

function checkTheseus() {
    if (process.platform !== 'win32') {
        report('info', 'Theseus board', 'not on this OS: gates by discipline, verifiers started by hand (see the getting-started guide)');
        return;
    }
    const local = process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local');
    const installed = fs.existsSync(path.join(local, 'Theseus'));
    report('info', 'Theseus board', installed ? 'found: sessions it starts get its tools as well'
        : 'not found: optional on Windows, from the latest release');
}

function checkProject(folder) {
    const file = path.join(folder, 'project.md');
    if (!fs.existsSync(file)) {
        report('missing', 'Project file', `${file} not found`);
        return;
    }
    const text = fs.readFileSync(file, 'utf8');
    report(/^\*\*Kit:\*\* \S+/m.test(text) ? 'ok' : 'missing', 'Project file: Kit line', (text.match(/^\*\*Kit:\*\* (\S+)/m) || [])[1] || 'no **Kit:** line');
    report(/^\*\*Primary branch:\*\* \S+/m.test(text) ? 'ok' : 'missing', 'Project file: primary branch',
        (text.match(/^\*\*Primary branch:\*\* (\S+)/m) || [])[1] || 'no **Primary branch:** line');
    report(/^### Objective \d+\.\d+: /m.test(text) ? 'ok' : 'warn', 'Project file: objectives',
        `${(text.match(/^### Objective \d+\.\d+: /gm) || []).length} found`);
    const tracked = run('git', ['-C', folder, 'rev-parse', '--is-inside-work-tree']) === 'true';
    report(tracked ? 'ok' : 'warn', 'Project home in git', tracked ? 'yes' : 'not a git repository: every spec version is kept only if it is');
}

checkTools();
checkFiles();
checkGuards();
checkTheseus();
if (process.argv[2]) {
    checkProject(path.resolve(process.argv[2]));
}

const width = Math.max(...results.map(result => result.what.length));
process.stdout.write(`Agentic factory check, plugin at ${root}\n\n`);
for (const result of results) {
    process.stdout.write(`${result.state.padEnd(8)}${result.what.padEnd(width + 2)}${result.detail}\n`);
}
const missing = results.filter(result => result.state === 'missing').length;
process.stdout.write(`\n${missing === 0 ? 'Everything the method needs is in place.' : `${missing} missing.`}\n`);
process.exitCode = missing === 0 ? 0 : 1;
