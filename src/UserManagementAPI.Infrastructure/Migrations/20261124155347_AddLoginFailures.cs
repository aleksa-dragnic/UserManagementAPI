using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserManagementAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "login_failures",
                columns: table => new
                {
                    email_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    last_failure_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    blocked_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_login_failures", x => x.email_hash);
                });

            migrationBuilder.CreateIndex(
                name: "ix_login_failures_last_failure_at_utc",
                table: "login_failures",
                column: "last_failure_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_failures");
        }
    }
}
