using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Identity.Entities;

public class RolePermission : Entity<Guid>
{
    public Guid RoleId { get; private set; }
    public string Permission { get; private set; } = string.Empty;

    private RolePermission() { }

    public RolePermission(Guid id, Guid roleId, string permission) : base(id)
    {
        RoleId = roleId;
        Permission = permission;
        CreatedAt = DateTime.UtcNow;
    }
}