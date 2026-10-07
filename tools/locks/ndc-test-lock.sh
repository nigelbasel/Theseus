#!/usr/bin/env bash
# Machine-wide locks, held by Theseus (Nigel, 2026-10-06). This script is the client for anything that
# runs as a command: container-backed test suites, compose builds and whole-solution builds take the
# "tests" lock through it. Sessions that hold a lock across many commands (the dev stack, llama) use the
# lock_acquire / lock_release tools on the Theseus MCP server instead.
#
# Source of truth: C:\dev\git\Theseus\tools\locks\ndc-test-lock.sh, installed to ~/.claude/bin by the
# Theseus installer. Edit it there, not in ~/.claude/bin.
#
# Usage:
#   ndc-test-lock.sh run "<who>" "<label>" [--lock <name>] -- <command...>
#       Take the lock, run the command, release when it exits however it exits. Refuses at once if the
#       lock is busy, unless NDC_TEST_LOCK_WAIT=<seconds> lets it queue that long.
#   ndc-test-lock.sh wait "<who>" "<label>" [--lock <name>] [--wait <seconds>]
#       Queue until the lock is granted, then print a token. Waiting is separate from running, so a long
#       wait does not eat the time limit of the background task that runs the suite.
#   ndc-test-lock.sh run --token <token> -- <command...>
#       Run a command under a lock granted by "wait". Start it within 2 minutes of the grant.
#   ndc-test-lock.sh status
#   ndc-test-lock.sh release <token>
#       Let go of a lock, or leave its queue. The token is printed as soon as a request is queued.
#   ndc-test-lock.sh leave "<who>" "<label>" [--lock <name>]
#       Leave a queue without the token, from the directory you queued from. Never releases a held lock.
#
# Exit codes: 0 success (or the command's own code under "run"), 1 not granted within the wait,
# 2 usage, 3 not Git Bash, 4 the ticket ended (cancelled by Nigel, dropped, or released), 5 the grant
# lapsed before "run --token" took it up.

set -u

case "$(uname -s)" in
  MINGW*|MSYS*) ;;
  *)
    echo "REFUSED: ndc-test-lock.sh must run under Git Bash, not $(uname -s)." >&2
    echo "From PowerShell use: & \"C:\Program Files\Git\bin\bash.exe\" \"\$HOME/.claude/bin/ndc-test-lock.sh\" ..." >&2
    exit 3
    ;;
esac

URL="${THESEUS_LOCKS_URL:-http://localhost:47813/locks}"
POLL_SECONDS=10
RESPONSE=""

json() {
  node -e 'process.stdout.write(JSON.stringify(Object.fromEntries(process.argv.slice(1).map(a => { const i = a.indexOf("="); return [a.slice(0, i), a.slice(i + 1)]; }).filter(([, v]) => v !== ""))))' "$@" 2>/dev/null
}

field() {
  printf '%s' "$RESPONSE" | node -e 'let s = ""; process.stdin.on("data", c => s += c).on("end", () => { try { const v = JSON.parse(s)[process.argv[1]]; process.stdout.write(v == null ? "" : String(v)); } catch { } })' "$1" 2>/dev/null
}

call() {
  local verb="$1" body="$2" code
  RESPONSE=$(curl -s -m 10 -o - -w '\n%{http_code}' -X POST -H 'Content-Type: application/json' --data-binary "$body" "$URL/$verb" 2>/dev/null)
  code="${RESPONSE##*$'\n'}"
  RESPONSE="${RESPONSE%$'\n'*}"
  case "$code" in
    200|400) [ "$(field status)" != "" ] ;;
    *) return 1 ;;
  esac
}

stray_container_report() {
  local rows
  rows=$(timeout 10 docker ps --filter "label=org.testcontainers" --format '{{.Names}} ({{.Image}}, {{.Status}})' 2>/dev/null)
  if [ -n "$rows" ]; then
    echo "NOTE: Testcontainers containers are already running on this host:"
    echo "$rows" | sed 's/^/  /'
  fi
}

# Calls Theseus, waiting for it while it is not running, for at most the seconds left in the budget.
call_waiting() {
  local verb="$1" body="$2" budget="$3" announced=0
  while ! call "$verb" "$body"; do
    if [ "$budget" -le 0 ]; then
      echo "THESEUS NOT REACHABLE at $URL - it holds the machine's locks, so nothing can be granted until it is running." >&2
      echo "Tell the user; do not run the command without the lock." >&2
      return 1
    fi
    if [ "$announced" -eq 0 ]; then
      echo "Theseus is not running (or has no lock service), so the lock cannot be granted. Waiting for it - tell the user." >&2
      announced=1
    fi
    sleep 15
    budget=$((budget - 15))
  done
  WAIT_LEFT="$budget"
}

# A run started by the old directory-based script before the switch to Theseus still holds this, and
# overlapping it is the failure the lock exists to prevent. Nothing creates it any more.
LEGACY_LOCK_DIR="${NDC_TEST_LOCK_DIR:-/c/tmp/ndc-test.lock}"

legacy_holder_alive() {
  local pid
  [ -d "$LEGACY_LOCK_DIR" ] || return 1
  pid=$(sed -n 's/^pid: *//p' "$LEGACY_LOCK_DIR/holder" 2>/dev/null)
  if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
    return 0
  fi
  rm -rf "$LEGACY_LOCK_DIR" 2>/dev/null
  return 1
}

wait_for_legacy_holder() {
  local budget="$1"
  while legacy_holder_alive; do
    if [ "$budget" -le 0 ]; then
      echo "LOCK BUSY - a run started before the switch to Theseus still holds the old lock:" >&2
      sed 's/^/  /' "$LEGACY_LOCK_DIR/holder" >&2
      return 1
    fi
    sleep "$POLL_SECONDS"
    budget=$((budget - POLL_SECONDS))
  done
}

# Queues for a lock until granted or the budget runs out. Leaves TOKEN set either way.
queue() {
  local lock="$1" who="$2" label="$3" budget="$4" last="" status detail
  local body
  if [ "$lock" = "tests" ]; then
    wait_for_legacy_holder "$budget" || return 1
  fi
  body=$(json lock="$lock" who="$who" purpose="$label" mode=process cwd="$(pwd -W 2>/dev/null || pwd)" reason="${NDC_TEST_LOCK_REASON:-}")
  WAIT_LEFT="$budget"
  call_waiting request "$body" "$WAIT_LEFT" || return 1
  TOKEN=$(field token)
  if [ "$(field status)" = "waiting" ] && [ "$budget" -gt 0 ]; then
    echo "QUEUED for the $lock lock. Token: $TOKEN"
    echo "To leave the queue: ndc-test-lock.sh release $TOKEN   (or: ndc-test-lock.sh leave \"$who\" \"$label\")"
  fi
  # Interrupting a wait gives the place up, so a session that stops waiting does not leave a ticket to
  # be granted and then lapse in front of everyone behind it. A wait that simply runs out keeps it.
  trap 'call release "$(json token="$TOKEN")" >/dev/null; exit 130' INT
  trap 'call release "$(json token="$TOKEN")" >/dev/null; exit 143' TERM
  while true; do
    status=$(field status)
    detail=$(field detail)
    case "$status" in
      granted) trap - INT TERM; return 0 ;;
      waiting) ;;
      ended) echo "LOCK TICKET ENDED: $detail" >&2; return 4 ;;
      *) echo "LOCK REFUSED: $detail" >&2; return 2 ;;
    esac
    if [ "$detail" != "$last" ]; then
      echo "$detail Tell the user you are waiting; never ask the holder to give way."
      last="$detail"
    fi
    if [ "$WAIT_LEFT" -le 0 ]; then
      trap - INT TERM
      return 1
    fi
    sleep "$POLL_SECONDS"
    WAIT_LEFT=$((WAIT_LEFT - POLL_SECONDS))
    call_waiting check "$(json token="$TOKEN")" "$WAIT_LEFT" || return 1
  done
}

attach_and_run() {
  local winpid
  winpid=$(cat "/proc/$$/winpid" 2>/dev/null)
  if ! call attach "{\"token\":\"$TOKEN\",\"pid\":${winpid:-0}}" || [ "$(field status)" != "granted" ]; then
    echo "LOCK NOT TAKEN UP: $(field detail)" >&2
    exit 5
  fi
  trap 'call release "$(json token="$TOKEN")" >/dev/null' EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM
  echo "LOCK HELD ($(field lock)) for this command; it is released when the command exits."
  stray_container_report
  "$@"
  exit $?
}

lock_name="tests"

take_lock_option() {
  if [ "${1:-}" = "--lock" ]; then
    lock_name="${2:-tests}"
    return 0
  fi
  return 1
}

trap "" PIPE

case "${1:-status}" in
  run)
    shift
    if [ "${1:-}" = "--token" ]; then
      TOKEN="${2:-}"
      shift 2 2>/dev/null || true
      [ "${1:-}" = "--" ] && shift
      if [ -z "$TOKEN" ] || [ "$#" -eq 0 ]; then
        echo "usage: ndc-test-lock.sh run --token <token> -- <command...>" >&2
        exit 2
      fi
      call_waiting check "$(json token="$TOKEN")" 0 || exit 1
      case "$(field status)" in
        granted) attach_and_run "$@" ;;
        waiting) echo "NOT YET GRANTED: $(field detail) Use 'wait' first." >&2; exit 1 ;;
        *) echo "LOCK TICKET ENDED: $(field detail)" >&2; exit 5 ;;
      esac
    fi
    who="${1:-unknown}"; label="${2:-unspecified}"
    shift 2 2>/dev/null || true
    take_lock_option "$@" && shift 2
    [ "${1:-}" = "--" ] && shift
    if [ "$#" -eq 0 ]; then
      echo "usage: ndc-test-lock.sh run \"<who>\" \"<label>\" [--lock <name>] -- <command...>" >&2
      exit 2
    fi
    queue "$lock_name" "$who" "$label" "${NDC_TEST_LOCK_WAIT:-0}"
    result=$?
    if [ "$result" -ne 0 ]; then
      if [ "$result" -eq 1 ] && [ -n "${TOKEN:-}" ] && [ "${NDC_TEST_LOCK_WAIT:-0}" -le 0 ]; then
        call release "$(json token="$TOKEN")" >/dev/null
        echo "LOCK BUSY. Queue for it: NDC_TEST_LOCK_WAIT=<seconds> on run, or 'wait' then 'run --token' for a long wait." >&2
      fi
      exit "$result"
    fi
    attach_and_run "$@"
    ;;
  wait)
    shift
    who="${1:-unknown}"; label="${2:-unspecified}"
    shift 2 2>/dev/null || true
    budget="${NDC_TEST_LOCK_WAIT:-5400}"
    while [ "$#" -gt 0 ]; do
      case "$1" in
        --lock) lock_name="${2:-tests}"; shift 2 ;;
        --wait) budget="${2:-5400}"; shift 2 ;;
        *) shift ;;
      esac
    done
    queue "$lock_name" "$who" "$label" "$budget"
    result=$?
    if [ "$result" -eq 0 ]; then
      echo "LOCK GRANTED. Token: $TOKEN"
      echo "Start the command within 2 minutes, in a fresh background call:"
      echo "  ndc-test-lock.sh run --token $TOKEN -- <command...>"
    elif [ "$result" -eq 1 ] && [ -n "${TOKEN:-}" ]; then
      echo "STILL WAITING when this wait ran out. Run the same 'wait' again within 5 minutes to keep your place."
    fi
    exit "$result"
    ;;
  status)
    listing=$(curl -s -m 10 "$URL/" 2>/dev/null)
    if [ -z "$listing" ]; then
      echo "THESEUS NOT REACHABLE at $URL - no lock can be granted until it is running."
    else
      printf '%s' "$listing" | node -e '
        let s = ""; process.stdin.on("data", c => s += c).on("end", () => {
          const now = Date.now(), ago = t => { const m = Math.floor((now - Date.parse(t)) / 60000); return m < 60 ? `${m} min` : `${Math.floor(m / 60)}h${String(m % 60).padStart(2, "0")}`; };
          for (const lock of JSON.parse(s).locks) {
            console.log(lock.holder ? `${lock.name}: HELD by ${lock.holder.who} for ${lock.holder.purpose}, ${ago(lock.holder.granted)}` : `${lock.name}: free`);
            lock.waiting.forEach((w, i) => console.log(`  ${i + 1}. ${w.who} for ${w.purpose}, waiting ${ago(w.requested)}${w.reason ? ` (${w.reason})` : ""}`));
          }
        });' 2>/dev/null
    fi
    stray_container_report
    ;;
  release)
    TOKEN="${2:-}"
    if [ -z "$TOKEN" ]; then
      echo "usage: ndc-test-lock.sh release <token>   (only the token's holder can release it)" >&2
      exit 2
    fi
    call release "$(json token="$TOKEN")" && echo "$(field detail)"
    ;;
  leave)
    shift
    who="${1:-}"; label="${2:-}"
    shift 2 2>/dev/null || true
    take_lock_option "$@" && shift 2
    if [ -z "$who" ] || [ -z "$label" ]; then
      echo "usage: ndc-test-lock.sh leave \"<who>\" \"<label>\" [--lock <name>]   (run it from the same directory you queued from)" >&2
      exit 2
    fi
    call leave "$(json lock="$lock_name" who="$who" purpose="$label" cwd="$(pwd -W 2>/dev/null || pwd)")" || { echo "THESEUS NOT REACHABLE at $URL" >&2; exit 1; }
    echo "$(field detail)"
    [ "$(field status)" = "ended" ]
    ;;
  acquire)
    echo "acquire is gone. For a long wait use 'wait' then 'run --token'; to hold a lock across many commands use the lock_acquire tool on the Theseus MCP server." >&2
    exit 2
    ;;
  *)
    echo "usage: ndc-test-lock.sh {run|wait|status|release|leave}" >&2
    exit 2
    ;;
esac
