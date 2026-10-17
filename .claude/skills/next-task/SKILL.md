---
name: next-task
description: Start the next build task — read progress and the slice spec, prepare a clean branch, and present an implementation plan for the owner's approval before editing anything.
argument-hint: "[task id, e.g. S01-T3] (optional)"
disable-model-invocation: true
allowed-tools: Read Grep Glob Bash(git status *) Bash(git fetch *) Bash(git switch *) Bash(git pull *) Bash(git branch *) Bash(git log *)
---

Start the next task. Task requested: $ARGUMENTS (if empty, use the first item in "Next up").

1. **Find the task.** Read `docs/build/PROGRESS.md` (Now, Next up) and `docs/build/roadmap.md`. Open the slice
   spec in `docs/build/slices/`.
   - If the spec says **"Status: outline"**, stop and tell the owner: the reviewer must write the detailed spec
     for this slice first.
2. **Check readiness** against `docs/build/definition-of-done.md` (Ready section). If anything is missing or
   ambiguous, ask the owner with AskUserQuestion — don't guess.
3. **Prepare git.** Working tree must be clean. `git fetch`, switch to `main`, pull. If this task depends on an
   unmerged PR, stop and say so. Create the branch `s<NN>/t<N>-<short-slug>`.
4. **Read only what the task needs:** its spec section, the ADRs and architecture/domain docs it references, and
   the existing code in the modules it touches. Use a subagent for broad exploration so the main context stays
   small.
5. **Write the plan:**
   - goal and the acceptance criteria you will satisfy (copy them);
   - projects/files to create or change, per module;
   - data model and migrations (incl. RLS for tenant tables);
   - action codes, error codes, integration events to add;
   - tests mapped to each acceptance criterion (unit, engine scenarios, integration, isolation, authorization,
     Playwright);
   - docs to update; dependencies to add (with licences);
   - risks and open questions;
   - expected PR size — if it will exceed ~600 changed lines (excluding generated files/migrations/lockfiles),
     propose a split.
6. **Stop for approval.** Present the plan (Plan mode in VS Code lets the owner comment inline). Do not edit files
   until the owner approves.
7. After approval, set "Now" in `docs/build/PROGRESS.md` to this task as part of the first commit, then implement
   in small commits with tests.
