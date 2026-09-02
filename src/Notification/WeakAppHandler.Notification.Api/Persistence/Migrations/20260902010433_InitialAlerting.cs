using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WeakAppHandler.Notification.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAlerting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    location = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    meter_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    metric_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "character varying(8)", maxLength: 8, nullable: false),
                    threshold_numeric = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    threshold_bool = table.Column<bool>(type: "boolean", nullable: true),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    hysteresis_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 5.00m),
                    cooldown_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_triggered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alert_rules", x => x.id);
                    table.CheckConstraint("ck_alert_rules_cooldown_seconds", "cooldown_seconds >= 0");
                    table.CheckConstraint("ck_alert_rules_hysteresis_percent", "hysteresis_percent >= 0 AND hysteresis_percent <= 100");
                    table.CheckConstraint("ck_alert_rules_single_threshold", "(threshold_numeric IS NULL) <> (threshold_bool IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "alert_rule_state",
                columns: table => new
                {
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    was_breaching = table.Column<bool>(type: "boolean", nullable: false),
                    last_triggered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alert_rule_state", x => new { x.rule_id, x.meter_id, x.metric_code });
                    table.ForeignKey(
                        name: "fk_alert_rule_state_alert_rules_rule_id",
                        column: x => x.rule_id,
                        principalTable: "alert_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    meter_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    metric_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    triggered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    triggered_value_numeric = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    triggered_value_bool = table.Column<bool>(type: "boolean", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_value_numeric = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    resolved_value_bool = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alerts", x => x.id);
                    table.CheckConstraint("ck_alerts_resolution_complete", "(resolved_at IS NULL) = (status = 'active')");
                    table.ForeignKey(
                        name: "fk_alerts_alert_rules_rule_id",
                        column: x => x.rule_id,
                        principalTable: "alert_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "alert_rules",
                columns: new[] { "id", "cooldown_seconds", "created_at", "hysteresis_percent", "is_enabled", "last_triggered_at", "location", "meter_type", "metric_code", "name", "operator", "severity", "threshold_bool", "threshold_numeric", "updated_at" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000001"), 300, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5.00m, true, null, null, null, "co2", "CO2 above 1000 ppm", "gt", "warning", null, 1000m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("a1000000-0000-0000-0000-000000000002"), 300, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5.00m, true, null, null, null, "co2", "CO2 above 1400 ppm", "gt", "critical", null, 1400m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("a1000000-0000-0000-0000-000000000003"), 300, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5.00m, true, null, null, null, "pm25", "PM2.5 above 35 ug/m3", "gt", "warning", null, 35m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("a1000000-0000-0000-0000-000000000004"), 300, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5.00m, true, null, null, null, "humidity", "Humidity above 70%", "gt", "info", null, 70m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            // hysteresis_percent is written explicitly here (hand-edited, and the only edit made to
            // this scaffolded file). HasData - unlike a runtime insert, which honours the sentinel
            // configured in AlertRuleConfiguration - omits any seed value equal to the CLR default
            // when the column has a store default, so this rule's deliberate 0 would have been left
            // to the column default and stored as 5.00. The model snapshot already records 0, so
            // writing it here is what keeps the database and the snapshot in agreement.
            migrationBuilder.InsertData(
                table: "alert_rules",
                columns: new[] { "id", "cooldown_seconds", "created_at", "hysteresis_percent", "is_enabled", "last_triggered_at", "location", "meter_type", "metric_code", "name", "operator", "severity", "threshold_bool", "threshold_numeric", "updated_at" },
                values: new object[] { new Guid("a1000000-0000-0000-0000-000000000005"), 300, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0.00m, true, null, "Garage", "motion", "motion_detected", "Motion detected in Garage", "eq", "warning", true, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_metric_code_is_enabled",
                table: "alert_rules",
                columns: new[] { "metric_code", "is_enabled" });

            migrationBuilder.CreateIndex(
                name: "ix_alerts_triggered_at",
                table: "alerts",
                column: "triggered_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ux_alerts_active_rule_meter_metric",
                table: "alerts",
                columns: new[] { "rule_id", "meter_id", "metric_code" },
                unique: true,
                filter: "status = 'active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_rule_state");

            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "alert_rules");
        }
    }
}
