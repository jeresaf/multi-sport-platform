# First Vertical Slice Acceptance — Football League

The first end-to-end implementation target is a small football league because it exercises the largest useful portion of the architecture without requiring complex cricket-style scoring on day one.

## Scenario

Tenant:
```text
Uganda Sports Demo
```

Competition:
```text
Kampala Premier League
```

Edition:
```text
2027 Demo Season
```

Configuration:
```text
Sport Definition: Football Association published version
Format: Single Round Robin published version
Ruleset: Football League Demo published version
```

## Required Flow

1. Provision tenant.
2. Enable Football entitlement.
3. Create canonical people.
4. Create athlete profiles.
5. Create organisations/teams.
6. Create team memberships.
7. Create competition.
8. Create edition.
9. Open registration.
10. Register 4 teams.
11. Approve registrations.
12. Create/approve rosters.
13. Instantiate league stage.
14. Generate fixtures.
15. Assign venues.
16. Assign officials.
17. Publish schedule.
18. Create match squads.
19. Submit/approve lineups.
20. Pass readiness.
21. Start contest.
22. Start first half.
23. Record goal + scorer + assist.
24. Record yellow card.
25. Record substitution.
26. End first half.
27. Start second half.
28. Record second goal.
29. End contest.
30. Submit result.
31. Reconcile score/timeline.
32. Approve result.
33. Close contest.
34. Recalculate standings.
35. Public read model updates.
36. Public web shows:
    - fixture/result
    - score
    - timeline
    - lineups
    - competition table
37. Audit explorer can trace major admin/scorer actions.
38. Statistics show player goals/assists and team totals.

## Correction Scenario

39. Reopen approved result with authorized reason.
40. Correct an event/result.
41. Preserve old result version.
42. Re-approve.
43. Recalculate standings.
44. Public view reflects corrected official truth.
45. Audit and timeline history retain evidence of correction.

## Pass Condition

The vertical slice passes only if no step requires bypassing module boundaries or editing the database manually.
