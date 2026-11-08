using GrantTracker.Data.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrantTracker.Data.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(r => r.Key);

        builder.Property(r => r.Key).HasMaxLength(IdempotencyRecord.KeyMaxLength);
        builder.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();

        builder.HasOne(r => r.CreatedTask)
            .WithMany()
            .HasForeignKey(r => r.CreatedTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.CreatedAtUtc);
    }
}
