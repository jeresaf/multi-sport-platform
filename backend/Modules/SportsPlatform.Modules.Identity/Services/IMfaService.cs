using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Modules.Identity.Services;

public interface IMfaService
{
    string BeginEnrollment(User user);
    bool VerifyCode(User user, string? code);
}
