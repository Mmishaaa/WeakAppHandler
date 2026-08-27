using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WeakAppHandler.Processor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    location = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    meter_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "metrics",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    meter_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    value_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    display_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_metrics", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "meter_current_state",
                columns: table => new
                {
                    meter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    value_numeric = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    value_bool = table.Column<bool>(type: "boolean", nullable: true),
                    previous_value_numeric = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    previous_value_bool = table.Column<bool>(type: "boolean", nullable: true),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meter_current_state", x => new { x.meter_id, x.metric_code });
                    table.ForeignKey(
                        name: "fk_meter_current_state_meters_meter_id",
                        column: x => x.meter_id,
                        principalTable: "meters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meter_current_state_metrics_metric_code",
                        column: x => x.metric_code,
                        principalTable: "metrics",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "readings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    meter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    value_numeric = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    value_bool = table.Column<bool>(type: "boolean", nullable: true),
                    is_changed = table.Column<bool>(type: "boolean", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_readings", x => x.id);
                    table.ForeignKey(
                        name: "fk_readings_meters_meter_id",
                        column: x => x.meter_id,
                        principalTable: "meters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_readings_metrics_metric_code",
                        column: x => x.metric_code,
                        principalTable: "metrics",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "metrics",
                columns: new[] { "code", "display_name", "meter_type", "unit", "value_kind" },
                values: new object[,]
                {
                    { "co2", "CO2", "air_quality", "ppm", "Numeric" },
                    { "energy", "Energy", "energy", "kWh", "Numeric" },
                    { "humidity", "Humidity", "air_quality", "%", "Numeric" },
                    { "motion_detected", "Motion Detected", "motion", "—", "Boolean" },
                    { "pm25", "PM2.5", "air_quality", "µg/m³", "Numeric" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_meter_current_state_metric_code",
                table: "meter_current_state",
                column: "metric_code");

            migrationBuilder.CreateIndex(
                name: "ix_meters_location_meter_type",
                table: "meters",
                columns: new[] { "location", "meter_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_readings_meter_id_metric_code_observed_at",
                table: "readings",
                columns: new[] { "meter_id", "metric_code", "observed_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_readings_metric_code",
                table: "readings",
                column: "metric_code");

            migrationBuilder.CreateIndex(
                name: "ix_readings_observed_at_brin",
                table: "readings",
                column: "observed_at")
                .Annotation("Npgsql:IndexMethod", "brin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meter_current_state");

            migrationBuilder.DropTable(
                name: "readings");

            migrationBuilder.DropTable(
                name: "meters");

            migrationBuilder.DropTable(
                name: "metrics");
        }
    }
}
