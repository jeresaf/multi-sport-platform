# Reference material (frozen)

These are the earlier planning documents, kept unchanged so intent and detail are never lost:

| File | What it is |
|---|---|
| `first-summary.md` | Consolidated background from the first attempt (vision, contest model, statuses, per-sport phases and events, UI patterns, seed and test strategy) |
| `update-notes.md` | The owner's breakdown into managers/systems and open questions |
| `chatgpt-pack/` | The greenfield implementation pack: 43 system specifications, 10 control documents, roadmap, templates |
| `first-attempt/` | Selected documents from the prototype that used to live in this repository (use-case catalogue, its ADRs, operations runbooks, design tokens); see its README |

## How to use them

- **Background, not instructions.** Read them for intent, field lists, edge cases and ideas.
- **Where they conflict with `docs/decisions/` (ADRs) or `docs/architecture/`, those win.** The mapping from every
  pack spec to its module, and the list of intentional deviations, is in `docs/architecture/modules.md`.
- The review explaining what changed and why: `docs/background/review-of-previous-plans.md`.
- Never edit files here (a permission rule and a hook enforce this).
