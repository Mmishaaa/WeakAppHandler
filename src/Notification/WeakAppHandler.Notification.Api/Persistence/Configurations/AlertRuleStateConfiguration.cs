using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeakAppHandler.Notification.Api.Domain;

namespace WeakAppHandler.Notification.Api.Persistence.Configurations;

public sealed class AlertRuleStateConfiguration : IEntityTypeConfiguration<AlertRuleState>
{
    public void Configure(EntityTypeBuilder<AlertRuleState> builder)
    {
        builder.ToTable("alert_rule_state");

        // The composite key IS the cooldown scope: one row per rule per meter per metric, so a
        // breach in one room cannot start another room's cooldown.
        builder.HasKey(s => new { s.RuleId, s.MeterId, s.MetricCode });

        builder.Property(s => s.MetricCode).HasMaxLength(32).IsRequired();

        // Unlike alerts, this row carries no history worth keeping: it is derived evaluation state,
        // so it goes when the rule does.
        builder.HasOne<AlertRule>()
            .WithMany()
            .HasForeignKey(s => s.RuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
