using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>Shadow properties added to every table by <see cref="ModuleDbContext"/> (snake_case in the database).</summary>
public static class AuditColumns
{
    public const string CreatedAt = "CreatedAt";
    public const string CreatedBy = "CreatedBy";
    public const string UpdatedAt = "UpdatedAt";
    public const string UpdatedBy = "UpdatedBy";

    /// <summary>The aggregate's row version (PostgreSQL <c>xmin</c>), exposed as <c>version</c> in DTOs.</summary>
    public const string Version = "Version";

    private const string _appendOnlyAnnotation = "SportsPlatform:AppendOnly";

    /// <summary>Rows are never updated, so the table gets no <c>updated_at</c>/<c>updated_by</c> columns.</summary>
    public static EntityTypeBuilder<TEntity> IsAppendOnly<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasAnnotation(_appendOnlyAnnotation, true);
        return builder;
    }

    /// <summary>Whether the entity was marked <see cref="IsAppendOnly{TEntity}"/> (the grants test checks its table).</summary>
    public static bool IsAppendOnly(this IReadOnlyEntityType entityType) =>
        entityType.FindAnnotation(_appendOnlyAnnotation)?.Value is true;
}
