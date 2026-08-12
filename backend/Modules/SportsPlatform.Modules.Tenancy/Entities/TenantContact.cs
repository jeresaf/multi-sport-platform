using System.Net.Mail;
using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Tenancy.Entities;

public class TenantContact : Entity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Role { get; private set; }
    public bool IsPrimary { get; private set; }
    public bool IsActive { get; private set; } = true;

    private TenantContact() { }

    public TenantContact(Guid id, Guid tenantId, string name, string email, string? phone, string? role, bool isPrimary)
        : base(id)
    {
        TenantId = tenantId;
        SetDetails(name, email, phone, role);
        IsPrimary = isPrimary;
        IsActive = true;
    }

    public void UpdateDetails(string name, string email, string? phone, string? role, bool isPrimary)
    {
        SetDetails(name, email, phone, role);
        IsPrimary = isPrimary;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    private void SetDetails(string name, string email, string? phone, string? role)
    {
        Name = Required(name, nameof(name), 200);
        Email = Required(email, nameof(email), 200).ToLowerInvariant();
        if (!MailAddress.TryCreate(Email, out _))
            throw new ArgumentException("A valid contact email is required.", nameof(email));
        Phone = Optional(phone, nameof(phone), 50);
        Role = Optional(role, nameof(role), 100);
    }

    private static string Required(string value, string parameter, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0) throw new ArgumentException("A value is required.", parameter);
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameter);
        return normalized;
    }

    private static string? Optional(string? value, string parameter, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameter);
        return normalized;
    }
}
