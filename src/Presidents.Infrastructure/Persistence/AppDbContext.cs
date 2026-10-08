using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Presidents.Domain.Common;
using Presidents.Domain.Entities;
using Presidents.Infrastructure.Identity;

namespace Presidents.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<President> Presidents => Set<President>();
    public DbSet<Presidency> Presidencies => Set<Presidency>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<HistoricalEvent> HistoricalEvents => Set<HistoricalEvent>();
    public DbSet<Law> Laws => Set<Law>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<SourceRecord> Sources => Set<SourceRecord>();
    public DbSet<FactSource> FactSources => Set<FactSource>();
    public DbSet<SourcedStatement> Statements => Set<SourcedStatement>();
    public DbSet<SourceDivergence> Divergences => Set<SourceDivergence>();
    public DbSet<Indicator> Indicators => Set<Indicator>();
    public DbSet<PresidentIndicator> PresidentIndicators => Set<PresidentIndicator>();
    public DbSet<IndicatorObservation> IndicatorObservations => Set<IndicatorObservation>();
    public DbSet<ContentCategoryLink> CategoryLinks => Set<ContentCategoryLink>();
    public DbSet<IngestionRecord> IngestionRecords => Set<IngestionRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Touch();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void Touch()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Defina ConnectionStrings__DefaultConnection antes de gerar migrations.");

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        return new AppDbContext(options);
    }
}
