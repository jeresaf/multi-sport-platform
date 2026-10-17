# MSP build kit — start here (for you, the owner)

This folder becomes the new content of your repository, `jeresaf/multi-sport-platform`. It turns the earlier
plans into something Claude Code (the VS Code extension) can build from, one reviewed step at a time:

- **Claude Code in VS Code builds.** It reads `CLAUDE.md` and the `.claude/` folder automatically.
- **Claude in the claude.ai "MSP" project reviews.** It checks every pull request and writes the next specs.
- **You approve plans, answer questions and merge.**

Why the plan changed from the ChatGPT pack: `docs/background/review-of-previous-plans.md`.
Why we restart instead of building on the first attempt's code: `docs/background/first-attempt-code-review.md`.

## What's inside

```text
CLAUDE.md                  Instructions Claude Code loads every session (kept short on purpose)
.claude/settings.json      Permissions (what Claude may run without asking, what it may never do) + a guard hook
.claude/hooks/             guard-paths.mjs — blocks edits to frozen docs, accepted ADRs and merged migrations
.claude/rules/             Conventions that load only when Claude touches matching files (backend, web, DB, tests…)
.claude/skills/            /next-task  /finish-task  /address-review  /new-adr
.claude/agents/            pr-prereviewer — a fresh-context self-review before each PR
.github/                   Pull request template (CI workflow is added in task S00-T4)
docs/decisions/            16 architecture decisions (ADRs) — the highest source of truth
docs/architecture/         Modules, rules, tenancy, authorization, sport engine, events, API and data conventions
docs/domain/               State machines, formats, rulesets, sport definitions (football, rugby 15/7, basketball, cricket T20)
docs/product/              Vision, glossary, pilot customer (fictional), plans and pricing, public/admin UX, design system
docs/build/                Roadmap, slice specs, workflow, review protocol, done-criteria, progress log, open questions
docs/background/           The two reviews: earlier plans, and the first attempt's code
docs/reference/            Your original documents, the ChatGPT pack and selected prototype docs (frozen, background only)
```

## One-time setup (about 45 minutes)

1. **Install on your computer:** Git, GitHub CLI, .NET 10 SDK, Node.js 24 LTS (then run `corepack enable` for pnpm),
   Docker Desktop, and VS Code with the **Claude Code** extension. On Windows, install Git for Windows too.
2. **Sign the GitHub CLI in:** `gh auth login` (so Claude Code can open pull requests).
3. **Keep the prototype, then replace `main`'s contents with the kit** — in a fresh clone, through one pull
   request. Nothing is lost: the tag and branch keep every line of the first attempt. Use a terminal, because the
   kit contains hidden folders (`.claude`, `.github`) that file browsers can skip.

   macOS / Linux / Git Bash:
   ```bash
   git clone https://github.com/jeresaf/multi-sport-platform.git msp
   cd msp

   # 1) Keep the prototype forever
   git tag -a prototype-v0 -m "First attempt (prototype) before the restart"
   git push origin prototype-v0
   git branch prototype/v0
   git push -u origin prototype/v0

   # 2) Replace the contents of main with the kit, on a branch
   git switch -c restart/kit
   git rm -r -q .                                  # removes the prototype's files from this branch only
   unzip -q ~/Downloads/msp-claude-kit.zip         # creates ./msp-claude-kit
   cp -R msp-claude-kit/. . && rm -rf msp-claude-kit
   ls -a                                           # you should see CLAUDE.md, README-FIRST.md, .claude, .github, docs
   git add -A
   git commit -m "chore: restart with the build kit (prototype kept at tag prototype-v0)"
   git push -u origin restart/kit
   gh pr create --base main --head restart/kit --title "Restart: replace the prototype with the build kit" \
     --body "Prototype preserved at tag prototype-v0 and branch prototype/v0. See docs/background/first-attempt-code-review.md."
   ```

   Windows (PowerShell):
   ```powershell
   git clone https://github.com/jeresaf/multi-sport-platform.git msp
   cd msp
   git tag -a prototype-v0 -m "First attempt (prototype) before the restart"
   git push origin prototype-v0
   git branch prototype/v0
   git push -u origin prototype/v0
   git switch -c restart/kit
   git rm -r -q .
   Expand-Archive "$HOME\Downloads\msp-claude-kit.zip" .
   Copy-Item .\msp-claude-kit\* . -Recurse -Force
   Remove-Item .\msp-claude-kit -Recurse -Force
   git add -A
   git commit -m "chore: restart with the build kit (prototype kept at tag prototype-v0)"
   git push -u origin restart/kit
   gh pr create --base main --head restart/kit --title "Restart: replace the prototype with the build kit" --body "Prototype preserved at tag prototype-v0 and branch prototype/v0."
   ```

   Then open the pull request on GitHub and merge it. If the merge is blocked by required checks from the
   prototype's CI (its workflows are removed by this PR), remove those required checks first; you'll add the new
   ones after task S00-T4.
4. **Protect `main`** (Settings → Branches → add a rule for `main`): require a pull request before merging. After
   task S00-T4 adds CI, also require the CI checks to pass. Keep the repository private.
5. **Prototype passwords.** The first attempt had passwords written into its seed code and docs. If any
   environment of it is reachable from a network, change those passwords or shut it down.
6. **Connect GitHub for reviews:** in claude.ai, make sure GitHub is connected for the MSP project. The first time you
   ask for a review, the reviewer will request access to `jeresaf/multi-sport-platform`.
7. **Open the folder in VS Code**, open the Claude Code panel and sign in.
8. **Paste the first prompt** from `docs/build/KICKOFF.md`.

## Your daily loop

```text
VS Code:   /next-task  → read the plan, comment, approve → Claude implements and tests → /finish-task (opens PR)
claude.ai: "Review PR #12 in jeresaf/multi-sport-platform"  → the reviewer posts findings
VS Code:   /address-review 12                                → fixes pushed
claude.ai: "Re-review PR #12"                                → approved
GitHub:    Squash and merge → VS Code: /clear → /next-task
```

At the end of each slice, ask the reviewer: **"Slice review S0N"**. It checks for drift and writes the next slice's
detailed spec.

## What to say to the reviewer (claude.ai, MSP project)

| You want | Say |
|---|---|
| The restart PR checked | `Review the restart PR in jeresaf/multi-sport-platform` |
| A PR reviewed | `Review PR #12 in jeresaf/multi-sport-platform` |
| Fixes checked | `Re-review PR #12` |
| End-of-slice check + next spec | `Slice review S05` |
| A spec written early | `Write the detailed spec for S06` |
| To change a decision | `I want to change <decision> because …` (it becomes a new ADR) |
| No GitHub access | Paste `gh pr view 12` and `gh pr diff 12` output, or upload a zip of the branch |

## Tips that save time

- **Approve plans carefully.** It's cheaper to fix a plan than a pull request. In VS Code, Plan mode opens the plan as
  a document you can comment on.
- **One task per conversation.** Run `/clear` between tasks; a fresh session with a precise prompt beats a long one.
- **Keep PRs small.** If Claude proposes a big task, ask it to split.
- **Never accept "fixed CI by skipping tests".** The rules forbid it; reject it in review.
- **Permission modes:** recent Claude Code versions start in Auto mode, where a safety check reviews actions for you.
  Switch to Manual if you want to approve every command. Either way, `.claude/settings.json` blocks force-pushes,
  pushing to `main`, merging PRs, and editing frozen docs.
- **Mistakes are cheap to undo:** every message in Claude Code is a checkpoint you can rewind to (hover a message →
  rewind).

## Decisions

Decided on 2026-10-17 (details in `docs/build/open-questions.md` → Answered):
- **Q1** each customer gets its own public site (ADR-0010);
- **Q2** STANDARD and PRO plans with indicative prices (`docs/product/pricing-and-plans.md`);
- **Q4** hosting in Azure South Africa North, Johannesburg (ADR-0016);
- **Q5** a fictional pilot customer, Kampala Metro Sports Association, and the sport order football → rugby →
  basketball → others (`docs/product/pilot-customer.md`).

Still open — work proceeds on safe defaults until you decide:
- **Q10** product name and platform domain (needed before the staging environment, after M1);
- **Q12** confirm building in this repository (the steps above assume yes);
- **Q13** which company signs customer contracts and registers with Uganda's data protection office (before the
  first pilot stores real personal data);
- **Q14** test the indicative prices with about five prospects.

You can rename this file or replace it with a normal README once you're up and running.
