# Architecture decision records

ADRs are immutable decision history. A later decision supersedes an accepted ADR
instead of silently rewriting it.

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-modular-monolith.md) | Deploy the backend as a modular monolith | Accepted |
| [0002](0002-universal-contest-model.md) | Use one universal contest aggregate | Accepted |
| [0003](0003-shared-schema-tenancy.md) | Use explicit shared-schema tenant scoping | Accepted |
| [0004](0004-durable-ordered-offline-replay.md) | Persist and replay mobile commands in order | Accepted |
| [0005](0005-signalr-after-persistence.md) | Publish SignalR updates after persistence | Accepted |
| [0006](0006-versioned-sport-rulesets.md) | Pin competitions and contests to ruleset versions | Accepted |
| [0007](0007-append-only-domain-audit.md) | Preserve business audit history append-only | Accepted |

New ADRs use the next four-digit number and contain Context, Decision,
Consequences, and Alternatives sections.

