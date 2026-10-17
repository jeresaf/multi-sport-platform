---
paths:
  - "docs/**"
  - "README*.md"
  - "CLAUDE.md"
---

# Documentation rules

- `docs/reference/` is frozen background (earlier plans). Never edit it; where it conflicts with ADRs or
  architecture docs, the ADRs win.
- ADRs: create with `/new-adr` (status Proposed). Never rewrite an Accepted ADR; supersede it with a new one and
  change only the old one's status line.
- Slice specs: you may tick checkboxes and add an "Implementation notes" section at the end. Changing scope or
  acceptance criteria needs the owner's agreement (record it in PROGRESS.md "Deviations").
- `docs/build/PROGRESS.md`: update at the end of every task (Now, Next up, Log row; deviations; tech debt).
- `docs/build/open-questions.md`: add questions you can't resolve; never answer owner questions yourself.
- `docs/generated/` and `contracts/` are written by the CLI only.
- Keep CLAUDE.md under 200 lines and accurate (especially the Commands section). Put path-specific guidance in
  `.claude/rules/`, procedures in `.claude/skills/`.
- Write plainly: short sentences, concrete examples, no marketing language.
