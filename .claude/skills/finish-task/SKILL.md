---
name: finish-task
description: Finish the current task — verify everything, self-review the diff with a fresh subagent, update progress docs, push and open a pull request for the reviewer. Never merges.
argument-hint: "(no arguments)"
disable-model-invocation: true
allowed-tools: Read Grep Glob Bash(git status *) Bash(git diff *) Bash(git log *) Bash(git add *) Bash(git commit *) Bash(git push -u origin *) Bash(gh pr create *) Bash(gh pr view *) Bash(dotnet build *) Bash(dotnet test *) Bash(dotnet format *) Bash(dotnet run *) Bash(pnpm *)
---

Finish the current task on this branch.

1. **Acceptance check.** Re-read the task's acceptance criteria in its slice spec. For each one, note the evidence
   (test name, command output, screenshot). Anything unmet → implement it or ask the owner; don't open a PR with
   unmet criteria unless the owner agrees (record as a deviation).
2. **Verify** using the commands in CLAUDE.md:
   - backend: build with warnings as errors, all tests (unit, integration, architecture), format check;
   - web (if touched): lint, typecheck, unit tests, build; Playwright for affected flows;
   - regenerate catalogues, OpenAPI and the web API client with the project tools, and make sure nothing stale
     remains (`git status` clean after regeneration, or the regenerated files are committed).
   Fix root causes. Never skip, weaken or delete tests.
3. **Self-review.** Run the `pr-prereviewer` subagent on `git diff main...HEAD`, giving it the spec path and task
   ID. Fix real BLOCKER/MAJOR findings; note any you disagree with for the PR description.
4. **Docs.** Update `docs/build/PROGRESS.md` (Now → next task, Next up, Log row, deviations, tech debt), the
   roadmap status if the slice is complete, any architecture/domain/UX docs whose behaviour changed, and the
   CLAUDE.md Commands section if commands changed.
5. **Commit and push.** Conventional Commits with module scopes; `git push -u origin <branch>`.
6. **Open the PR** with `gh pr create --title "[SNN-TN] <title>" --body-file <file>`, filling in
   `.github/pull_request_template.md`: summary, acceptance checklist with evidence, actions/errors/events,
   endpoints, migrations (+RLS), dependencies with licences, deviations, screenshots for UI, follow-ups.
   If `gh` isn't available, print the full PR body for the owner.
7. **Report** to the owner: PR link, one-paragraph summary, decisions needed, and the next step:
   "Ask the reviewer: *Review PR #N in <owner>/<repo>*."
8. Do **not** merge. Suggest `/clear` before the next task.
