using GrantTracker.Domain.Grants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrantTracker.Data.Configurations;

internal sealed class GrantConfiguration : IEntityTypeConfiguration<Grant>
{
    public void Configure(EntityTypeBuilder<Grant> builder)
    {
        builder.ToTable("Grants");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.ReferenceNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(g => g.ReferenceNumber).IsUnique();

        builder.Property(g => g.OrganisationName).HasMaxLength(200).IsRequired();
        builder.Property(g => g.Status).HasConversion<string>().HasMaxLength(10);

        // Every change to the grant or its tasks assigns a new Version, so a
        // stale write fails with DbUpdateConcurrencyException (OP-2).
        builder.Property(g => g.Version).IsConcurrencyToken();

        builder.HasMany(g => g.Tasks)
            .WithOne(t => t.Grant)
            .HasForeignKey(t => t.GrantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(g => g.Tasks)
            .HasField("_tasks")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(g => g.PendingTaskCount);
    }
}
