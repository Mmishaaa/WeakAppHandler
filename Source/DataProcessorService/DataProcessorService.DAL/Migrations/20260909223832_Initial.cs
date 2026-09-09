using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataProcessorService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Location = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MeterType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMessages", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "Readings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeterId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValueNumeric = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    ValueBool = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Readings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Readings_Meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "Meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Meters_Location_MeterType",
                table: "Meters",
                columns: new[] { "Location", "MeterType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Readings_MeterId_MetricCode_ObservedAt",
                table: "Readings",
                columns: new[] { "MeterId", "MetricCode", "ObservedAt" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedMessages");

            migrationBuilder.DropTable(
                name: "Readings");

            migrationBuilder.DropTable(
                name: "Meters");
        }
    }
}
