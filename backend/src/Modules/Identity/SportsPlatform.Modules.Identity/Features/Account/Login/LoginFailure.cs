namespace SportsPlatform.Modules.Identity.Features.Account.Login;

/// <summary>Why a sign-in was refused: in the audit entry's metadata only, never in the answer (S03 D15).</summary>
public enum LoginFailureReason
{
    UnknownEmail,
    WrongPassword,
    LockedOut,

    /// <summary>The email is not confirmed.</summary>
    NotAllowed,
}

/// <summary>The audit metadata of a refused sign-in. Never the email.</summary>
public sealed record LoginFailure(LoginFailureReason Reason);
