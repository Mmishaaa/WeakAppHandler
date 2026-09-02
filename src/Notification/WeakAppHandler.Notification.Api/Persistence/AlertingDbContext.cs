using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Notification.Api.Domain;

namespace WeakAppHandler.Notification.Api.Persistence;

/// <summary>
/// The Notification service's own context, owning the alerting tables and nothing else (PRD §4.2,
/// §8: `notification_rw` owns only these). It shares a physical database with the Processor's
/// CoreDbContext and the Auth service's AuthDbContext but never references their tables, so the
/// three migration histories stay independent of one another.
/// </summary>
public sealed class AlertingDbContext(DbContextOptions<AlertingDbContext> options) : DbContext(options)
{
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<AlertRuleState> AlertRuleStates => Set<AlertRuleState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlertingDbContext).Assembly);
    }
}
