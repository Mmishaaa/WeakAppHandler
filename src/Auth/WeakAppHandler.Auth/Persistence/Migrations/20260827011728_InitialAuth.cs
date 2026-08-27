using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WeakAppHandler.Auth.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_clients",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    client_secret_hash = table.Column<string>(type: "text", nullable: false),
                    scopes = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_clients", x => x.client_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "service_clients",
                columns: new[] { "client_id", "client_secret_hash", "scopes" },
                values: new object[] { "gateway-ingestor", "100000.Gis8TV5vcIGSo7TF1uf4CQ==.OrPC+BZUxOWUbmqLAkWrWjWV2ywBF8W4wIUbz/zCkzQ=", new[] { "ingestion:admin" } });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "created_at", "display_name", "email", "password_hash", "role" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Demo Viewer", "viewer@weakapphandler.local", "100000.TD8eepstTl9qe4ydDh8qOw==.Ms1151VCfO2qspjD7h8rp39VEvwnA1l7XGS8Qw4oNOM=", "viewer" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Demo Admin", "admin@weakapphandler.local", "100000.n459bFtKOSgXBl9OPSwbCg==.B4IlZ6DiiU2KQhviWUUJeclTAHRtOcb+nO7IKu8KkOI=", "admin" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_clients");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
