using GrantTracker.Data.Idempotency;
using GrantTracker.Domain.Grants;
using GrantTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Grant> Grants => Set<Grant>();

    public DbSet<GrantTask> Tasks => Set<GrantTask>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
