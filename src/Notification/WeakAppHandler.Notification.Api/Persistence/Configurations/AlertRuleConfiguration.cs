using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Notification.Api.Domain;
using WeakAppHandler.Notification.Api.Persistence.Converters;

namespace WeakAppHandler.Notification.Api.Persistence.Configurations;

public sealed class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("alert_rules", table =>
        {
            // A rule compares against exactly one kind of threshold. Without this, a numeric rule
            // carrying a stray boolean threshold (or neither) reaches the rule engine as an
            // un-evaluatable rule, and the failure would surface as a missing alert - the hardest
            // kind of bug to notice in an alerting system.
            table.HasCheckConstraint(
                "ck_alert_rules_single_threshold",
                "(threshold_numeric IS NULL) <> (threshold_bool IS NULL)");

            // The same invariants TASK-030's request validation will enforce at the edge, kept here
            // too so a rule written by anything other than that endpoint cannot break the engine.
            table.HasCheckConstraint("ck_alert_rules_cooldown_seconds", "cooldown_seconds >= 0");
            table.HasCheckConstraint(
                "ck_alert_rules_hysteresis_percent",
                "hysteresis_percent >= 0 AND hysteresis_percent <= 100");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Location).HasMaxLength(64);
        builder.Property(r => r.MeterType).HasMaxLength(32);
        builder.Property(r => r.MetricCode).HasMaxLength(32).IsRequired();

        builder.Property(r => r.Operator)
            .HasConversion<AlertOperatorConverter>()
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(r => r.Severity)
            .HasConversion<AlertSeverityConverter>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.ThresholdNumeric).HasPrecision(12, 4);

        // PRD §7.1 gives these three columns database-level defaults, which is what a hand-written
        // INSERT relies on. The matching HasSentinel calls are not decoration: with a store default
        // configured, EF omits from its INSERT any value equal to the sentinel - by default the CLR
        // default - and lets the database fill it in. Left alone, `hysteresis = 0`, `cooldown = 0`
        // and `is_enabled = false` would each be silently stored as the default instead; the boolean
        // seed rule below sets hysteresis to 0 and would have come back as 5.00. Pointing the
        // sentinel at the default value itself makes the omission harmless (the database writes the
        // same number) while every other value, zero and false included, is sent explicitly.
        builder.Property(r => r.HysteresisPercent)
            .HasPrecision(5, 2)
            .HasDefaultValue(AlertRule.DefaultHysteresisPercent)
            .HasSentinel(AlertRule.DefaultHysteresisPercent);

        builder.Property(r => r.CooldownSeconds)
            .HasDefaultValue(AlertRule.DefaultCooldownSeconds)
            .HasSentinel(AlertRule.DefaultCooldownSeconds);

        builder.Property(r => r.IsEnabled)
            .HasDefaultValue(true)
            .HasSentinel(true);

        // Every ReadingStored event looks its rules up by metric code and only cares about enabled
        // ones, so that pair is the lookup the consumer runs once per stored metric.
        builder.HasIndex(r => new { r.MetricCode, r.IsEnabled })
            .HasDatabaseName("ix_alert_rules_metric_code_is_enabled");

        builder.HasData(AlertRuleSeedData.All);
    }
}
