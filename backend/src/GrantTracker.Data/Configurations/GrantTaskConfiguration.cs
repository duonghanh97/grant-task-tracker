using GrantTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrantTracker.Data.Configurations;

internal sealed class GrantTaskConfiguration : IEntityTypeConfiguration<GrantTask>
{
    public void Configure(EntityTypeBuilder<GrantTask> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).HasMaxLength(GrantTask.TitleMaxLength).IsRequired();
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(10);

        // Supports the overdue query: CompletedDate IS NULL AND DueDate < today.
        builder.HasIndex(t => new { t.CompletedDate, t.DueDate });

        builder.Ignore(t => t.IsCompleted);
    }
}
