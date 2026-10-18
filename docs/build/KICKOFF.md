# Kickoff — the first session in Claude Code (VS Code)

Do the human setup in README-FIRST.md first (prerequisites, prototype tagged, restart PR merged so the kit is on
`main`).

## First prompt (paste into the Claude Code panel)

```text
You are the builder for this repository. Before doing anything else:

1. Read CLAUDE.md, docs/build/workflow.md, docs/build/review-protocol.md and docs/build/roadmap.md.
2. Skim docs/architecture/modules.md and docs/architecture/rules.md so you know the boundaries.
3. Then run /next-task for S00-T1 (docs/build/slices/S00-repo-foundation.md).

Ask me with AskUserQuestion if anything in the docs is unclear or contradictory before planning.
Do not write code until I approve your plan.
```

## After that, every task

```text
/next-task            → review the plan, comment inline if needed, approve
(implementation runs)
/finish-task          → PR opened
```

Then, in the claude.ai **MSP** project, start a new chat:

```text
Review PR #<n> in jeresaf/multi-sport-platform.
```

Back in VS Code:

```text
/address-review <n>
```

When the reviewer approves, merge on GitHub with a merge commit, then in VS Code run `/clear` and `/next-task`.

## At the end of each slice

In the MSP project: `Slice review S0N` — the reviewer checks drift, updates the plan with you and writes the next
slice's detailed spec. Add it to `docs/build/slices/` (or merge the reviewer's docs PR).
