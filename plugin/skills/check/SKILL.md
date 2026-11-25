---
name: check
description: Report whether the agentic factory method is in place on this machine, what is installed and what is missing, and optionally check a project's home folder.
argument-hint: "[project home folder]"
disable-model-invocation: true
allowed-tools: Bash(node:*)
---

# Check the method is in place

Run this, with the project home folder if one was given (`$ARGUMENTS`), and show the user its output as it is:

```bash
node "${CLAUDE_PLUGIN_ROOT}/plugin/scripts/check.js" "$ARGUMENTS"
```

Then, for each line that is not `ok`, say in one line what to do about it:

- **Claude Code** older than 2.1.295: `claude update`. Older versions let a tool call through when a guard cannot run.
- **Node** or **git** missing: install it; the getting-started guide in the Theseus repo's `docs/` says how.
- **Plugin installed** missing: the install steps are in the getting-started guide.
- **Guards** failing: reinstall the plugin, and report the failing line to whoever maintains it.
- **Project file** lines: fix the line by hand, in the shape `${CLAUDE_PLUGIN_ROOT}/method/project.md` gives.

If the command itself is refused, or reports that `node` cannot be found, Node is missing or not on the PATH. Every
tool call is then refused by the guards, on purpose: install Node and start a new session.
