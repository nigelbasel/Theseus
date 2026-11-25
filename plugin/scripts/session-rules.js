// SessionStart hook: puts the method's standing rules and its decide, record or park ladder into the session, read
// from method/README.md so the rules are written once. Plain stdout is added to the session's context; Claude Code caps
// it at 10,000 characters, which the suite checks.

const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..', '..');

function section(text, heading) {
    const start = text.indexOf(`\n## ${heading}`);
    if (start < 0) {
        return '';
    }
    const next = text.indexOf('\n## ', start + 4);
    return text.slice(start + 1, next < 0 ? text.length : next).trim();
}

function rules() {
    const readme = fs.readFileSync(path.join(root, 'method', 'README.md'), 'utf8').replace(/\r\n/g, '\n');
    const version = (readme.match(/\*\*Version:\*\* `([^`]+)`/) || [])[1] || 'unknown';

    return [
        `# The agentic factory method (${version}), from the agentic-factory plugin`,
        '',
        'These rules hold in this session. The stage skills (/agentic-factory:research to review) say what each stage',
        'produces and what must be true before moving on.',
        '',
        'Without the Theseus tools (no mcp__theseus__ tools in this session):',
        '- record_decision: a dated row in the project file\'s Decisions table, by "<workstream> (default, for review)".',
        '- ask_owner, set_waiting (park): the question, options and your recommendation in work/<slug>/questions.md, and',
        '  "Waiting: owner: <what>" under the criterion it holds; carry on with what does not depend on it.',
        '- report_back: write work/<slug>/REPORT.md (/agentic-factory:report), then stop.',
        '- tick_criterion and the other project.md tools: edit by hand, in the shape method/project.md gives.',
        '- accept_spec: the owner accepts in the session; record their words and the date as a Decisions row first.',
        '- a verifier card: /agentic-factory:verifier gives the command to start one in a fresh session.',
        '',
        section(readme, 'Rules that hold everywhere'),
        '',
        section(readme, 'Working unattended: decide, record, or park'),
        '',
    ].join('\n');
}

try {
    process.stdout.write(rules());
} catch (error) {
    process.stdout.write(`The agentic-factory plugin could not read its method files (${error.message}). `
        + 'Run /agentic-factory:check, and reinstall the plugin if files are missing.\n');
}
