namespace SportsPlatform.Modules.Results.Entities;

public enum ResultStatus
{
    None,
    Draft,
    Submitted,
    UnderReview,
    Approved,
    Corrected,
    Disputed,
    Rejected
}

public static class ResultStatusExtensions
{
    public static ResultStatus ToCanonical(this ContestResultStatus status) => status switch
    {
        ContestResultStatus.Pending => ResultStatus.None,
        ContestResultStatus.Draft => ResultStatus.Draft,
        ContestResultStatus.Submitted => ResultStatus.Submitted,
        ContestResultStatus.UnderReview => ResultStatus.UnderReview,
        ContestResultStatus.Approved or ContestResultStatus.Published
            or ContestResultStatus.Official => ResultStatus.Approved,
        ContestResultStatus.Corrected => ResultStatus.Corrected,
        ContestResultStatus.Rejected => ResultStatus.Rejected,
        _ => ResultStatus.None
    };

    public static string ToCode(this ResultStatus status) => status switch
    {
        ResultStatus.None => "NONE",
        ResultStatus.Draft => "DRAFT",
        ResultStatus.Submitted => "SUBMITTED",
        ResultStatus.UnderReview => "UNDER_REVIEW",
        ResultStatus.Approved => "APPROVED",
        ResultStatus.Corrected => "CORRECTED",
        ResultStatus.Disputed => "DISPUTED",
        ResultStatus.Rejected => "REJECTED",
        _ => "NONE"
    };
}
