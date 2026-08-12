using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Identity.Entities;

public class Role : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }
    public Guid? TenantId { get; private set; }

    private readonly List<RolePermission> _permissions = new();
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    private Role() { }

    public Role(Guid id, string name, string? description, bool isSystemRole = false, Guid? tenantId = null) : base(id)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        IsSystemRole = isSystemRole;
        TenantId = tenantId;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddPermission(string permission)
    {
        var normalized = SystemPermissions.Normalize(permission);
        if (!_permissions.Any(p => p.Permission == normalized))
        {
            _permissions.Add(new RolePermission(Guid.NewGuid(), Id, normalized));
        }
    }

    public void RemovePermission(string permission)
    {
        var perm = _permissions.FirstOrDefault(p => p.Permission == permission);
        if (perm != null)
        {
            _permissions.Remove(perm);
        }
    }

    public bool HasPermission(string permission)
    {
        return _permissions.Any(p => p.Permission == permission);
    }

    public void MarkAsSystemRole()
    {
        IsSystemRole = true;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length is < 2 or > 100)
            throw new ArgumentException("Role names must contain between 2 and 100 characters.", nameof(name));
        return normalized;
    }

    private static string? NormalizeDescription(string? description)
    {
        var normalized = description?.Trim();
        if (normalized?.Length > 500)
            throw new ArgumentException("Role descriptions cannot exceed 500 characters.", nameof(description));
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}

public static class SystemRoles
{
    public const string PlatformOwner = "Platform Owner";
    public const string PlatformSupport = "Platform Support";
    public const string TenantOwner = "Tenant Owner";
    public const string CompetitionAdministrator = "Competition Administrator";
    public const string RegistrationOfficer = "Registration Officer";
    public const string FixtureManager = "Fixture Manager";
    public const string HeadOfficial = "Head Official";
    public const string Scorer = "Scorer";
    public const string ResultApprover = "Result Approver";
    public const string MediaManager = "Media Manager";
    public const string Viewer = "Viewer";
}
