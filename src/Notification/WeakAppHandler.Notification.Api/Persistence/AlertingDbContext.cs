using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Notification.Api.Domain;

namespace WeakAppHandler.Notification.Api.Persistence;

/// <summary>
/// The Notification service's own context, owning the alerting tables and nothing else (PRD §4.2,
/// §8: `notification_rw` owns only these). It shares a physical database with the Processor's
/// CoreDbContext and the Auth service's AuthDbContext but never references their tables, and (via
/// <see cref="MigrationsHistoryTableName"/>) each keeps its own migration history table too, so the
/// three migration histories stay independent of one another.
/// </summary>
public sealed class AlertingDbContext(DbContextOptions<AlertingDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Distinct from EF Core's shared "__EFMigrationsHistory" default - see the class doc comment.
    /// </summary>
    public const string MigrationsHistoryTableName = "__ef_migrations_history_notification";

    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<AlertRuleState> AlertRuleStates => Set<AlertRuleState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlertingDbContext).Assembly);
    }
}
