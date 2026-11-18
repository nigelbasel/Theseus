// PreToolUse guard for Bash and PowerShell: refuses container-backed test runs, compose builds and
// solution-wide builds that do not go through ~/.claude/bin/ndc-test-lock.sh. Many Claude sessions
// share this machine; two such runs at once starve each other and fail like real defects. A rule in
// CLAUDE.md only reaches sessions started after it was written; this hook reaches every session.
// Theseus.slnx is exempt: its suites start no containers and touch no shared stack.
// Override for one command by setting NDC_TEST_LOCK_BYPASS=1 in that command (for the lock's own holder).
//
// Source of truth: C:\dev\git\Theseus\tools\locks\ndc-test-lock-guard.js, installed to ~/.claude/bin by the
// Theseus installer. The locks themselves are held by Theseus at http://localhost:47813/locks/.

const http = require('http');

const LOCKS_URL = process.env.THESEUS_LOCKS_URL || 'http://localhost:47813/locks';

function listing(done) {
  const request = http.get(`${LOCKS_URL}/`, { timeout: 1500 }, response => {
    let body = '';
    response.on('data', chunk => { body += chunk; });
    response.on('end', () => {
      try {
        done(JSON.parse(body));
      } catch {
        done(null);
      }
    });
  });
  request.on('timeout', () => request.destroy());
  request.on('error', () => done(null));
}

function announceWaitIfHeldByAnother(command, finish) {
  const request = /ndc-test-lock\.sh"?\s+(run|wait)\s+"([^"]*)"\s+"([^"]*)"/.exec(command);
  if (!request) {
    finish();
    return;
  }
  const wanted = (/--lock\s+"?([a-z-]+)/.exec(command) || [])[1] || 'tests';
  listing(state => {
    const lock = state && (state.locks || []).find(candidate => candidate.name === wanted);
    if (!state) {
      process.stdout.write(JSON.stringify({
        systemMessage: `Theseus is not reachable, so the ${wanted} lock cannot be granted to "${request[3]}" until it is running.`,
      }));
    } else if (lock && lock.holder && lock.holder.who !== request[2]) {
      const waits = request[1] === 'wait' || /NDC_TEST_LOCK_WAIT=[1-9]\d*/.test(command);
      const queued = (lock.waiting || []).length;
      process.stdout.write(JSON.stringify({
        systemMessage:
          `${wanted} lock busy: this session wants it for "${request[3]}", but "${lock.holder.who}" holds it for ` +
          `"${lock.holder.purpose}" since ${lock.holder.granted}${queued ? `, with ${queued} already waiting` : ''}. ` +
          (waits ? 'This session is now queued for it.' : 'This session cannot start that run until the lock is free.'),
      }));
    }
    finish();
  });
}

// Whether the guard can be sure how a shell reads this command's quotes: every quote balanced, and none of the
// constructs where bash and PowerShell read quotes differently from each other or from a plain reading. When it
// cannot be sure it splits as if there were no quotes, which can refuse a correct run but never hide a command
// (guard-mentions, fix pass 3: fail closed when unsure).
function quotingIsSimple(command) {
  if (/\\["']|`|\$'|#|@['"]/.test(command)) {
    return false;
  }
  let quote = null;
  for (const character of command) {
    if (quote) {
      if (character === quote) {
        quote = null;
      }
    } else if (character === '"' || character === "'") {
      quote = character;
    }
  }
  return quote === null;
}

// Splits a command line at newlines, &&, ||, ; and | that are outside quotes. A separator inside quotes
// belongs to the argument it is in (a commit message, a label, the script given to bash -c), not to the line.
function splitOutsideQuotes(command) {
  const segments = [];
  let current = '';
  let quote = null;
  for (let index = 0; index < command.length; index++) {
    const character = command[index];
    if (quote) {
      if (character === '\\' && quote === '"' && index + 1 < command.length) {
        current += character + command[++index];
        continue;
      }
      if (character === quote) {
        quote = null;
      }
      current += character;
      continue;
    }
    if (character === '"' || character === "'") {
      quote = character;
      current += character;
      continue;
    }
    const pair = command.slice(index, index + 2);
    if (pair === '&&' || pair === '||') {
      segments.push(current);
      current = '';
      index++;
      continue;
    }
    if (character === ';' || character === '|' || character === '\n' || character === '\r') {
      segments.push(current);
      current = '';
      continue;
    }
    current += character;
  }
  segments.push(current);
  return segments;
}

// The program a command segment starts, and its first argument, after any PowerShell call operator and any
// VAR=value prefixes.
function programAndArgument(segment) {
  const text = segment.trim().replace(/^&\s*/, '').replace(/^(?:[A-Za-z_][A-Za-z0-9_]*=(?:"[^"]*"|'[^']*'|\S*)\s+)*/, '');
  const token = /^\s*(?:"([^"]*)"|'([^']*)'|(\S+))/;
  const first = token.exec(text);
  if (!first) {
    return { program: '', argument: '' };
  }
  const second = token.exec(text.slice(first[0].length)) || [];
  return { program: first[1] ?? first[2] ?? first[3], argument: second[1] ?? second[2] ?? second[3] ?? '' };
}

// Whether a segment executes the lock script: through bash or sh, or by calling the script itself.
function runsLockScript(segment) {
  const { program, argument } = programAndArgument(segment);
  const isScript = path => /(^|[\\/])ndc-test-lock\.sh$/.test(path);
  return isScript(program) || (/(^|[\\/])(bash|sh)(\.exe)?$/i.test(program) && isScript(argument));
}

function viaGitBash(segment) {
  return /Git[\\/](usr[\\/])?bin[\\/]bash(\.exe)?$/i.test(programAndArgument(segment).program);
}

let raw = '';
process.stdin.on('data', chunk => { raw += chunk; });
process.stdin.on('end', () => {
  let command = '';
  let toolName = '';
  try {
    const input = JSON.parse(raw);
    toolName = String(input.tool_name || '');
    command = String((input.tool_input || {}).command || '');
  } catch {
    process.exit(0);
  }

  const deny = reason => {
    process.stdout.write(JSON.stringify({
      hookSpecificOutput: {
        hookEventName: 'PreToolUse',
        permissionDecision: 'deny',
        permissionDecisionReason: reason,
      },
    }));
    process.exit(0);
  };

  // Only a segment that RUNS the lock script is a locked run. A command that merely mentions it (a commit
  // message, a grep, a git log) is checked like any other, and every other segment of a command that does run
  // it is still checked: mentioning the script must neither refuse a harmless command nor excuse an unlocked one.
  const segments = quotingIsSimple(command) ? splitOutsideQuotes(command) : command.split(/\r?\n|&&|\|\||;|\|/);
  const lockRuns = segments.filter(runsLockScript);
  const rest = segments.filter(segment => !runsLockScript(segment)).join('\n');

  if (lockRuns.length > 0 && toolName === 'PowerShell' && !lockRuns.every(viaGitBash)) {
    deny(
      'Blocked: in PowerShell, "bash" is WSL\'s bash, which sees a different home directory and process ' +
      'table, so the lock would not exclude anyone. Call Git Bash explicitly:\n' +
      '  & "C:\\Program Files\\Git\\bin\\bash.exe" "$HOME/.claude/bin/ndc-test-lock.sh" run "<your session name>" "<suite>" -- <command>');
  }

  if (/NDC_TEST_LOCK_BYPASS=1/.test(command)) {
    process.exit(0);
  }

  const lockedOrNothing = () => lockRuns.length > 0
    ? announceWaitIfHeldByAnother(command, () => process.exit(0))
    : process.exit(0);

  const containerSuite = /dotnet(\.exe)?\s+test\b[^\n;&|]*(Api\.Tests|Api\.[A-Za-z]+\.Tests|Persistence\.?Tests|IntegrationTests|Content\.Service\.UnitTests|\.slnf?\b)/i;
  const composeBuild = /docker(-compose|\s+compose)\b[^\n;&|]*(\sbuild\b|\sup\b[^\n;&|]*--build)/i;
  const solutionBuild = /dotnet(\.exe)?\s+build\b[^\n;&|]*\.slnf?\b/i;

  const withoutUnitOnlySuites = rest
    .replace(/Api\.Mcp\.Tests/gi, 'McpUnitSuite')
    .replace(/\bTheseus\.slnx\b/gi, 'TheseusSolution');

  let what = null;
  if (containerSuite.test(withoutUnitOnlySuites)) { what = 'a container-backed test suite (or a whole-solution test run)'; }
  else if (composeBuild.test(rest)) { what = 'a docker compose build'; }
  else if (solutionBuild.test(withoutUnitOnlySuites)) { what = 'a whole-solution build'; }

  if (!what) {
    lockedOrNothing();
    return;
  }

  const runner = toolName === 'PowerShell'
    ? '& "C:\\Program Files\\Git\\bin\\bash.exe" "$HOME/.claude/bin/ndc-test-lock.sh"'
    : 'bash ~/.claude/bin/ndc-test-lock.sh';

  deny(
    `Blocked: ${what} must run through the machine-wide lock, because other Claude sessions share ` +
    `this machine's Docker and CPU and concurrent runs fail like real defects.\n` +
    `Run it as:\n` +
    `  ${runner} run "<your session name>" "<suite or build>" -- <the same command>\n` +
    `Check who holds it: ${runner} status. If busy, queue: in the background, ${runner} wait "<your session name>" ` +
    `"<suite or build>", then run the command with ${runner} run --token <token> -- <the same command> in a fresh ` +
    `background call; tell Nigel you are waiting. If it is urgent, tell Nigel. ` +
    `Never ask the holder to stop or give way, and never kill a run to free the lock. Verify locally before pushing; do not offload verification to CI. ` +
    `Unit suites (*.UnitTests, *.Unit.Tests, Api.Mcp.Tests) are not blocked.`);
});
