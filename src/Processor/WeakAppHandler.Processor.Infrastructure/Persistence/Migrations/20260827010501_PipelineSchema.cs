using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WeakAppHandler.Processor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PipelineSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingest_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    http_status = table.Column<int>(type: "integer", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    reading_count = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingest_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_messages",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processed_messages", x => x.message_id);
                });

            migrationBuilder.CreateTable(
                name: "readings_hourly",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    meter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bucket_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    value_avg = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    value_min = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    value_max = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    value_sum = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    reading_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_readings_hourly", x => x.id);
                    table.ForeignKey(
                        name: "fk_readings_hourly_meters_meter_id",
                        column: x => x.meter_id,
                        principalTable: "meters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_readings_hourly_metrics_metric_code",
                        column: x => x.metric_code,
                        principalTable: "metrics",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_readings_batch_id",
                table: "readings",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_ingest_batches_fetched_at",
                table: "ingest_batches",
                column: "fetched_at");

            migrationBuilder.CreateIndex(
                name: "ix_readings_hourly_metric_code",
                table: "readings_hourly",
                column: "metric_code");

            migrationBuilder.CreateIndex(
                name: "ux_readings_hourly_meter_id_metric_code_bucket_start",
                table: "readings_hourly",
                columns: new[] { "meter_id", "metric_code", "bucket_start" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_readings_ingest_batches_batch_id",
                table: "readings",
                column: "batch_id",
                principalTable: "ingest_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_readings_ingest_batches_batch_id",
                table: "readings");

            migrationBuilder.DropTable(
                name: "ingest_batches");

            migrationBuilder.DropTable(
                name: "processed_messages");

            migrationBuilder.DropTable(
                name: "readings_hourly");

            migrationBuilder.DropIndex(
                name: "ix_readings_batch_id",
                table: "readings");
        }
    }
}
