#!/usr/bin/env python3
from pathlib import Path
import shutil
import sys

ROOT = Path.cwd()

canonical = ROOT / "backend/Modules/SportsPlatform.Modules.Contests/Entities/CanonicalContestStatus.cs"
public_controller = ROOT / "apps/api/Controllers/PublicController.cs"
scoring = ROOT / "apps/admin-web/src/components/scoring/connected-sport-scoring.tsx"

for path in (canonical, public_controller, scoring):
    if not path.exists():
        sys.exit(f"[ERROR] Missing expected file: {path}")

def backup(path: Path):
    bak = path.with_suffix(path.suffix + ".bak-ci-followup")
    if not bak.exists():
        shutil.copy2(path, bak)
        print(f"[BACKUP] {bak}")

def save(path: Path, before: str, after: str):
    if before == after:
        print(f"[UNCHANGED] {path}")
        return
    backup(path)
    path.write_text(after, encoding="utf-8")
    print(f"[UPDATED] {path}")

# 1) Restore global Published -> ENDED contract.
before = canonical.read_text(encoding="utf-8")
text = before

old = '''        ContestStatus.PendingApproval or ContestStatus.Approved or ContestStatus.Corrected
            => CanonicalContestStatus.Ended,
        ContestStatus.Published => CanonicalContestStatus.Closed,

        ContestStatus.Cancelled => CanonicalContestStatus.Cancelled,'''
new = '''        ContestStatus.PendingApproval or ContestStatus.Approved or ContestStatus.Corrected
            or ContestStatus.Published => CanonicalContestStatus.Ended,

        ContestStatus.Cancelled => CanonicalContestStatus.Cancelled,'''
if new not in text:
    if text.count(old) != 1:
        raise RuntimeError(f"ToCanonical mapping: expected 1 match, found {text.count(old)}")
    text = text.replace(old, new, 1)
    print("[PATCH] restore Published -> ENDED in ToCanonical")
else:
    print("[SKIP] ToCanonical mapping already restored")

old = '''            "PENDING_APPROVAL" or "APPROVED" or "CORRECTED"
                => CanonicalContestStatus.Ended,
            "PUBLISHED" => CanonicalContestStatus.Closed,
            "CANCELLED" or "CANCELED" => CanonicalContestStatus.Cancelled,'''
new = '''            "PENDING_APPROVAL" or "APPROVED" or "CORRECTED" or "PUBLISHED"
                => CanonicalContestStatus.Ended,
            "CANCELLED" or "CANCELED" => CanonicalContestStatus.Cancelled,'''
if new not in text:
    if text.count(old) != 1:
        raise RuntimeError(f"provider mapping: expected 1 match, found {text.count(old)}")
    text = text.replace(old, new, 1)
    print("[PATCH] restore provider PUBLISHED -> ENDED")
else:
    print("[SKIP] provider mapping already restored")

save(canonical, before, text)

# 2) Public fixtures project Published as CLOSED only at this presentation boundary.
before = public_controller.read_text(encoding="utf-8")
text = before

method_start = text.find('[HttpGet("fixtures")]')
if method_start < 0:
    raise RuntimeError("Could not find PublicController.GetFixtures.")
method_end = text.find('\n    [HttpGet(', method_start + 20)
if method_end < 0:
    method_end = len(text)

prefix = text[:method_start]
method = text[method_start:method_end]
suffix = text[method_end:]

old = '''                Status = contest.Status.ToString(),
                ContestStatus = contest.CanonicalStatus.ToCode(),
                contest.StartTime,'''
new = '''                Status = contest.Status.ToString(),
                ContestStatus = contest.Status == ContestStatus.Published
                    ? CanonicalContestStatus.Closed.ToCode()
                    : contest.CanonicalStatus.ToCode(),
                contest.StartTime,'''
if new not in method:
    if method.count(old) != 1:
        raise RuntimeError(
            f"fixtures projection: expected 1 match inside GetFixtures, found {method.count(old)}"
        )
    method = method.replace(old, new, 1)
    print("[PATCH] public fixtures Published -> CLOSED projection")
else:
    print("[SKIP] public fixtures projection already applied")

text = prefix + method + suffix
save(public_controller, before, text)

# 3) Remove ref-capturing refresh() from golf review handler.
before = scoring.read_text(encoding="utf-8")
text = before

function_start = text.find("  async function reviewGolfScorecard(")
if function_start < 0:
    raise RuntimeError("Could not find reviewGolfScorecard.")
function_end = text.find("\n  if (loading)", function_start)
if function_end < 0:
    raise RuntimeError("Could not find end of reviewGolfScorecard.")

prefix = text[:function_start]
function = text[function_start:function_end]
suffix = text[function_end:]

old = '''      setNotice(approve ? 'Scorecard approved and leaderboard updated.' : 'Scorecard rejected.');
      await refresh();'''
new = '''      setNotice(approve ? 'Scorecard approved and leaderboard updated.' : 'Scorecard rejected.');
      const [scoreData, scorecardData] = await Promise.all([
        adminApi<LiveScore[]>(
          `/api/livescoring/contests/${activeContest.id}/scores`,
        ),
        adminApi<GolfScorecardReview[]>(
          `/api/livescoring/contests/${activeContest.id}/golf-scorecards`,
        ),
      ]);
      setScores(scoreData);
      setGolfScorecards(scorecardData);'''
if new not in function:
    if function.count(old) != 1:
        raise RuntimeError(
            f"golf review refresh: expected 1 match inside reviewGolfScorecard, found {function.count(old)}"
        )
    function = function.replace(old, new, 1)
    print("[PATCH] golf review ref-safe refresh")
else:
    print("[SKIP] golf review ref-safe refresh already applied")

text = prefix + function + suffix
save(scoring, before, text)

print()
print("CI follow-up fixes applied.")
print("Run:")
print("  dotnet test tests/UnitTests/SportsPlatform.UnitTests.csproj --configuration Release")
print('  dotnet test tests/IntegrationTests/SportsPlatform.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~PublicShowcase_ExposesEverySportAndLifecycleScenario"')
print("  cd apps/admin-web && npm run lint")
