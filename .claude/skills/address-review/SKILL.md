---
name: address-review
description: Address the reviewer's findings on a pull request — fetch or read the review, fix or answer each finding, verify, push, and reply on the PR.
argument-hint: "<PR number> (or paste the review text after the command)"
disable-model-invocation: true
allowed-tools: Read Grep Glob Bash(git status *) Bash(git diff *) Bash(git log *) Bash(git switch *) Bash(git pull *) Bash(git add *) Bash(git commit *) Bash(git push -u origin *) Bash(gh pr view *) Bash(gh pr diff *) Bash(gh pr checks *) Bash(gh pr comment *) Bash(dotnet build *) Bash(dotnet test *) Bash(dotnet format *) Bash(pnpm *)
---

Address the review for: $ARGUMENTS

1. **Get the findings.**
   - If a PR number was given: `gh pr view <N> --comments` (and, if inline comments exist, ask permission to run
     `gh api repos/{owner}/{repo}/pulls/<N>/comments`). Collect the reviewer's findings (R1, R2, …) with severity.
   - If review text was pasted, use it.
2. **Switch** to the PR branch and pull the latest commits.
3. **Triage each finding:** *Fix* · *Won't fix* (reason — the owner decides) · *Question*. BLOCKER and MAJOR
   findings must be fixed or escalated to the owner with AskUserQuestion. Make no unrelated changes.
4. **Fix with tests** (add or adjust tests that would have caught the issue). Run the relevant tests, then the
   full verification from `/finish-task` step 2 before pushing.
5. **Commit** logically (`fix(<scope>): address review R1 — …`) and push.
6. **Reply on the PR** with one comment listing every finding and its resolution with commit SHAs
   (`gh pr comment <N> --body-file <file>`). Never skip a finding silently.
7. Update the PROGRESS.md log row if anything notable changed.
8. Tell the owner: "Ready for re-review — ask the reviewer: *Re-review PR #N*."
