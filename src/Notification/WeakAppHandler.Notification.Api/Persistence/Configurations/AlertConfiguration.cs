using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Notification.Api.Domain;
using WeakAppHandler.Notification.Api.Persistence.Converters;

namespace WeakAppHandler.Notification.Api.Persistence.Configurations;

public sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts", table =>
        {
            table.HasCheckConstraint(
                "ck_alerts_resolution_complete",
                "(resolved_at IS NULL) = (status = 'active')");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Location).HasMaxLength(64).IsRequired();
        builder.Property(a => a.MeterType).HasMaxLength(32).IsRequired();
        builder.Property(a => a.MetricCode).HasMaxLength(32).IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<AlertStatusConverter>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(a => a.Severity)
            .HasConversion<AlertSeverityConverter>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(a => a.TriggeredValueNumeric).HasPrecision(12, 4);
        builder.Property(a => a.ResolvedValueNumeric).HasPrecision(12, 4);

        // The rule is owned by this same context, so this FK is fine; meter_id deliberately has
        // none, because `meters` belongs to the Processor (see the Alert doc comment). Restrict
        // rather than Cascade: deleting a rule must not silently erase the alert history raised
        // under it - a rule that has fired is disabled through is_enabled, not deleted.
        builder.HasOne<AlertRule>()
            .WithMany()
            .HasForeignKey(a => a.RuleId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        // PRD §7.1's partial index on active alerts, made UNIQUE: §6.6 allows at most one active
        // alert per (rule, meter, metric), and enforcing that in the database means a duplicate
        // delivery or a concurrent consumer cannot produce the second one.
        builder.HasIndex(a => new { a.RuleId, a.MeterId, a.MetricCode })
            .IsUnique()
            .HasFilter("status = 'active'")
            .HasDatabaseName("ux_alerts_active_rule_meter_metric");

        // The alert feed is reverse-chronological (§6.8).
        builder.HasIndex(a => a.TriggeredAt)
            .IsDescending()
            .HasDatabaseName("ix_alerts_triggered_at");
    }
}
