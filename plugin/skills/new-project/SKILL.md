---
name: new-project
description: Start a new agentic factory project from the method's starter: a home folder with project.md and work/, and a primary branch in each repo it changes.
argument-hint: "[project title]"
disable-model-invocation: true
---

# Start a project

A project is a **home folder** holding `project.md` and `work/<slug>/` for each workstream's records. It sits outside
every code repo. The format is `${CLAUDE_PLUGIN_ROOT}/method/project.md`; read it first.

On Windows with the Theseus board, **File > New project…** does this, and the board reads the file. Use this command
when there is no board.

## Ask first, and never assume a layout

Ask the user, in one question each, for what `$ARGUMENTS` does not already give:

1. The project's **title**, and one paragraph: the **objective**, the outcome the whole project exists for.
2. Its **home folder**. Suggest `<projects folder>/<project-slug>`, beside the user's other projects, but use whatever
   they say. Never create a folder they have not named or agreed.
3. The **repos** it changes, each with its host, owner and name, and where the user's clone of each is.
4. The **primary branch** name. Suggest `<user>/<project-slug>`.
5. The **acceptance criteria** of the whole project: observable facts, evidence rather than dates. Two or three to
   start with is enough.

## Then do it

1. Create the home folder and `work/` inside it.
2. Copy `${CLAUDE_PLUGIN_ROOT}/method/starter/project.md` to `<home>/project.md` and fill it in: the title, the
   `**Primary branch:**` line, the objective, the acceptance criteria, and the Repos table (autonomy `L0` for every
   repo unless the user says otherwise). Keep the `**Kit:**` line as the starter has it. Leave Phase 0 and its first
   objective for the user to name, or ask them now.
3. Add a dated row to the Decisions table: the project started, by the user.
4. Offer to make the **projects folder** (the folder that holds the home folders) a local git repository with no remote,
   so every version of every spec is kept. Only with a yes: `git init`, then commit `project.md`.
5. For each repo, offer to create the primary branch off its trunk and push it: `git -C <clone> switch -c <primary>
   origin/<trunk>`, then `git -C <clone> push -u origin <primary>`. Each push is the user's yes.
6. Run `/agentic-factory:check <home>` and show the result.

Finish by saying what was made, with full paths, and that `/agentic-factory:new-workstream` starts the first piece of
work.
