using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Participants.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class BulkImportJobConfiguration : IEntityTypeConfiguration<BulkImportJob>
{
    public void Configure(EntityTypeBuilder<BulkImportJob> builder)
    {
        builder.ToTable("BulkImportJobs"); builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.Kind).IsRequired().HasMaxLength(30);
        builder.Property(item => item.FileName).IsRequired().HasMaxLength(255);
        builder.Property(item => item.Status).HasConversion<int>();
        builder.Property(item => item.DuplicateHandling).HasConversion<int>();
        builder.Property(item => item.InputJson).IsRequired().HasColumnType("jsonb");
        builder.Property(item => item.ErrorsJson).IsRequired().HasColumnType("jsonb");
        builder.HasIndex(item => new { item.TenantId, item.CreatedAt });
    }
}
