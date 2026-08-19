#!/usr/bin/env python3
from pathlib import Path
import shutil
import sys

ROOT = Path.cwd()

FILES = {
    "live_controller": ROOT / "apps/api/Controllers/LiveScoringController.cs",
    "live_service": ROOT / "backend/Infrastructure/SportsPlatform.Infrastructure/Services/LiveScoringService.cs",
    "schema": ROOT / "backend/Modules/SportsPlatform.Modules.Sports/Services/ScoringSchemaCatalog.cs",
    "canonical": ROOT / "backend/Modules/SportsPlatform.Modules.Contests/Entities/CanonicalContestStatus.cs",
    "results": ROOT / "apps/api/Controllers/ResultsController.cs",
}

for label, path in FILES.items():
    if not path.exists():
        sys.exit(f"[ERROR] Missing expected file ({label}): {path}")

changed_files = set()

def backup(path: Path):
    backup_path = path.with_suffix(path.suffix + ".bak-backend-integration-fixes")
    if not backup_path.exists():
        shutil.copy2(path, backup_path)
        print(f"[BACKUP] {backup_path}")

def write(path: Path, text: str):
    backup(path)
    path.write_text(text, encoding="utf-8")
    changed_files.add(path)
    print(f"[UPDATED] {path}")

def replace_once(text: str, old: str, new: str, description: str):
    if new in text:
        print(f"[SKIP] {description}: already applied")
        return text, False
    count = text.count(old)
    if count != 1:
        raise RuntimeError(
            f"{description}: expected exactly one matching block, found {count}."
        )
    return text.replace(old, new, 1), True

# 1) LiveScoringController.cs
path = FILES["live_controller"]
text = path.read_text(encoding="utf-8")
original = text

old = '''            var effectiveRequest = request with
            {
                SegmentId = request.SegmentId ?? session?.ActiveSegmentId ?? defaultSegmentId,
                ClockSeconds = request.ClockSeconds ??
                    (schema?.SupportsClock == true ? session?.CurrentClockSeconds : null)
            };'''
new = '''            var effectiveRequest = request with
            {
                SegmentId = request.SegmentId ?? session?.ActiveSegmentId ?? defaultSegmentId,
                ClockSeconds = request.ClockSeconds ??
                    (schema?.SupportsClock == true ? session?.CurrentClockSeconds : null),
                DeviceTimestamp = request.DeviceTimestamp ?? DateTime.UtcNow
            };'''
text, _ = replace_once(text, old, new, "effective timestamp fallback")

old = '''                    contestId,
                    result.Event.Id,
                    result.Event.ParticipantId,
                    result.Event.MetricName,
                    result.Event.Value,
                    result.Event.DisplayValue,
                    $"{result.Correction.Kind}Approved",'''
new = '''                    contestId,
                    result.Event.EventId,
                    result.Event.ParticipantId,
                    result.Event.MetricName,
                    result.Event.Value,
                    result.Event.DisplayValue,
                    $"{result.Correction.Kind}Approved",'''
text, _ = replace_once(text, old, new, "correction broadcast public EventId")

old = '''            var scores = await _liveScoringService.GetContestScoresAsync(contestId);
            return Ok(scores);'''
new = '''            var scores = await _liveScoringService.GetContestScoresAsync(contestId);
            return Ok(scores.Select(score => new
            {
                Id = score.EventId,
                InternalEventId = score.Id,
                score.TenantId,
                score.ContestId,
                score.EventId,
                score.EventType,
                score.ParticipantId,
                score.SecondaryParticipantId,
                score.PrimaryPlayerId,
                score.SecondaryPlayerId,
                score.SegmentId,
                score.ClockSeconds,
                score.MetricName,
                score.Value,
                score.DisplayValue,
                score.Sequence,
                score.TimelineSequence,
                score.ScorerId,
                score.AssistId,
                score.Metadata,
                score.DeviceId,
                score.IdempotencyKey,
                score.RulesetVersionId,
                score.DeviceTimestamp,
                score.ServerTimestamp,
                score.ScoredAt,
                score.CreatedAt
            }));'''
text, _ = replace_once(text, old, new, "public score projection")

validation_anchor = '''        if (request.ClockSeconds is < 0)
            return "Clock seconds cannot be negative.";
        try
        {
            EventFramework.ValidatePayload(definition, new EventPayload('''

validation_replacement = '''        if (request.ClockSeconds is < 0)
            return "Clock seconds cannot be negative.";

        if (definition.Validation.MinimumValue is null
            && definition.Validation.MaximumValue is null)
        {
            var schemaAction = SchemaFor(contest)?.Actions.FirstOrDefault(action =>
                !string.IsNullOrWhiteSpace(action.EventType)
                && action.EventType.Equals(
                    definition.Code,
                    StringComparison.OrdinalIgnoreCase)
                && action.Metric.Equals(
                    request.MetricName,
                    StringComparison.OrdinalIgnoreCase));

            if (schemaAction is not null)
            {
                if (schemaAction.InputType == "number")
                {
                    if (schemaAction.Minimum is not null
                        && request.Value < schemaAction.Minimum)
                        return $"{schemaAction.Label} must be at least {schemaAction.Minimum}.";
                    if (schemaAction.Maximum is not null
                        && request.Value > schemaAction.Maximum)
                        return $"{schemaAction.Label} must not exceed {schemaAction.Maximum}.";
                }
                else if (request.Value != schemaAction.DefaultValue)
                {
                    return $"Value {request.Value} is not allowed for event '{definition.Code}'.";
                }
            }
        }

        try
        {
            EventFramework.ValidatePayload(definition, new EventPayload('''

if validation_replacement not in text:
    count = text.count(validation_anchor)
    if count != 1:
        raise RuntimeError(
            "legacy configured-event value validation: "
            f"expected one anchor, found {count}."
        )
    text = text.replace(validation_anchor, validation_replacement, 1)
else:
    print("[SKIP] legacy configured-event value validation: already applied")

if text != original:
    write(path, text)
else:
    print(f"[UNCHANGED] {path}")

# 2) LiveScoringService.cs
path = FILES["live_service"]
text = path.read_text(encoding="utf-8")
original = text

old = '''        return new(true, "Correction submitted for approval.",
            ToView(request, proposal, "Pending", null), score);'''
new = '''        return new(true, "Correction submitted for approval.",
            ToView(request, score.EventId, proposal, "Pending", null), score);'''
text, _ = replace_once(text, old, new, "pending correction public EventId")

old = '''        return new(true, approve ? "Correction approved." : "Correction rejected.",
            ToView(request, proposal, approve ? "Approved" : "Rejected", resolution), score);'''
new = '''        return new(true, approve ? "Correction approved." : "Correction rejected.",
            ToView(
                request,
                score.EventId,
                proposal,
                approve ? "Approved" : "Rejected",
                resolution),
            score);'''
text, _ = replace_once(text, old, new, "reviewed correction public EventId")

old = '''        var updates = await _context.LiveScoreUpdates
            .Where(item => scoreIds.Contains(item.LiveScoreId) &&
                (item.UpdateType == CorrectionRequested ||
                 item.UpdateType == CorrectionApproved ||
                 item.UpdateType == CorrectionRejected))
            .OrderByDescending(item => item.UpdatedAt)
            .ToArrayAsync(cancellationToken);
        var resolutions = updates'''
new = '''        var scoreEventIds = await _context.LiveScores
            .Where(item => item.ContestId == contestId)
            .ToDictionaryAsync(item => item.Id, item => item.EventId, cancellationToken);
        var updates = await _context.LiveScoreUpdates
            .Where(item => scoreIds.Contains(item.LiveScoreId) &&
                (item.UpdateType == CorrectionRequested ||
                 item.UpdateType == CorrectionApproved ||
                 item.UpdateType == CorrectionRejected))
            .OrderByDescending(item => item.UpdatedAt)
            .ToArrayAsync(cancellationToken);
        var resolutions = updates'''
text, _ = replace_once(text, old, new, "correction EventId map")

old = '''                return ToView(request, proposal, status, review);'''
new = '''                var publicEventId = scoreEventIds.TryGetValue(
                    request.LiveScoreId,
                    out var eventId)
                    ? eventId
                    : request.LiveScoreId;
                return ToView(request, publicEventId, proposal, status, review);'''
text, _ = replace_once(text, old, new, "correction list public EventId")

old = '''    private static LiveScoreCorrectionView ToView(
        LiveScoreUpdate request,
        LiveScoreCorrectionProposal proposal,
        string status,
        LiveScoreUpdate? review) => new(
            request.Id,
            request.LiveScoreId,
            proposal.Kind,'''
new = '''    private static LiveScoreCorrectionView ToView(
        LiveScoreUpdate request,
        Guid publicEventId,
        LiveScoreCorrectionProposal proposal,
        string status,
        LiveScoreUpdate? review) => new(
            request.Id,
            publicEventId,
            proposal.Kind,'''
text, _ = replace_once(text, old, new, "ToView public identity")

if text != original:
    write(path, text)
else:
    print(f"[UNCHANGED] {path}")

# 3) ScoringSchemaCatalog.cs
path = FILES["schema"]
text = path.read_text(encoding="utf-8")
original = text

football_start = text.find("if (sportId == Football)")
basketball_start = text.find("if (sportId == Basketball)")
if football_start < 0 or basketball_start < 0 or basketball_start <= football_start:
    raise RuntimeError("Could not isolate the football scoring schema block.")

football_block = text[football_start:basketball_start]
if '"substitution-in"' in football_block:
    print("[SKIP] football substitution-in compatibility action: already present")
elif 'Fixed("substitution",' in football_block:
    football_block = football_block.replace(
        'Fixed("substitution",',
        'Fixed("substitution-in",',
        1
    )
    text = text[:football_start] + football_block + text[basketball_start:]
else:
    raise RuntimeError(
        "Football schema contains neither substitution-in nor expected substitution action."
    )

if text != original:
    write(path, text)
else:
    print(f"[UNCHANGED] {path}")

# 4) CanonicalContestStatus.cs
path = FILES["canonical"]
text = path.read_text(encoding="utf-8")
original = text

old = '''        ContestStatus.PendingApproval or ContestStatus.Approved or ContestStatus.Corrected
            or ContestStatus.Published => CanonicalContestStatus.Ended,

        ContestStatus.Cancelled => CanonicalContestStatus.Cancelled,'''
new = '''        ContestStatus.PendingApproval or ContestStatus.Approved or ContestStatus.Corrected
            => CanonicalContestStatus.Ended,
        ContestStatus.Published => CanonicalContestStatus.Closed,

        ContestStatus.Cancelled => CanonicalContestStatus.Cancelled,'''
text, _ = replace_once(text, old, new, "Published -> Closed canonical mapping")

old = '''            "PENDING_APPROVAL" or "APPROVED" or "CORRECTED" or "PUBLISHED"
                => CanonicalContestStatus.Ended,
            "CANCELLED" or "CANCELED" => CanonicalContestStatus.Cancelled,'''
new = '''            "PENDING_APPROVAL" or "APPROVED" or "CORRECTED"
                => CanonicalContestStatus.Ended,
            "PUBLISHED" => CanonicalContestStatus.Closed,
            "CANCELLED" or "CANCELED" => CanonicalContestStatus.Cancelled,'''
text, _ = replace_once(text, old, new, "provider Published -> Closed mapping")

if text != original:
    write(path, text)
else:
    print(f"[UNCHANGED] {path}")

# 5) ResultsController.cs
path = FILES["results"]
text = path.read_text(encoding="utf-8")
original = text

old = '''            var approvedSeriesGameIds = await GetApprovedContestIdsAsync(gameIds);
            if (bracketMatch.IsAggregateTie)'''
new = '''            var approvedSeriesGameIds = await GetApprovedContestIdsAsync(gameIds);
            approvedSeriesGameIds.UnionWith(seriesGames
                .Where(game => game.Status is ContestStatus.Approved
                    or ContestStatus.Published)
                .Select(game => game.Id));
            if (bracketMatch.IsAggregateTie)'''
text, _ = replace_once(text, old, new, "series approved-game compatibility")

old = '''                             && game.Status is ContestStatus.Draft
                                 or ContestStatus.Scheduled
                                 or ContestStatus.Ready))'''
new = '''                             && game.Status is ContestStatus.Draft
                                 or ContestStatus.Scheduled
                                 or ContestStatus.CheckInOpen
                                 or ContestStatus.Ready))'''
text, _ = replace_once(text, old, new, "cancel unneeded check-in-open series games")

if text != original:
    write(path, text)
else:
    print(f"[UNCHANGED] {path}")

print()
print("Backend integration fixes applied.")
print("Changed files:")
for item in sorted(changed_files):
    print(f"  - {item.relative_to(ROOT)}")
print()
print("Next: run the focused integration tests, then the full integration suite.")
