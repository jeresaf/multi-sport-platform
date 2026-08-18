namespace SportsPlatform.Modules.Results.Entities;

/// <summary>
/// Stable API-facing result workflow summary for a contest. This is separate
/// from whether the contest itself is scheduled, live, interrupted, or ended.
/// </summary>
public static class ContestResultWorkflowProjection
{
    public static string Summarize(IEnumerable<ContestResultStatus> statuses)
    {
        var values = statuses.Distinct().ToHashSet();
        if (values.Count == 0 || values.SetEquals(new[] { ContestResultStatus.Pending }))
            return "NONE";

        if (values.Contains(ContestResultStatus.Corrected)) return "CORRECTED";
        if (values.Contains(ContestResultStatus.UnderReview)) return "UNDER_REVIEW";
        if (values.Contains(ContestResultStatus.Submitted)) return "SUBMITTED";
        if (values.Contains(ContestResultStatus.Rejected)) return "REJECTED";
        if (values.Contains(ContestResultStatus.Draft)) return "DRAFT";
        if (values.All(status => status == ContestResultStatus.Official)) return "OFFICIAL";
        if (values.All(status => status is ContestResultStatus.Published or ContestResultStatus.Official))
            return "PUBLISHED";
        if (values.All(status => status is ContestResultStatus.Approved
                or ContestResultStatus.Published
                or ContestResultStatus.Official))
            return "APPROVED";

        return "PENDING";
    }
}
