using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserManagementAPI.Infrastructure.Migrations
{
    /// <summary>
    /// Maps PostgreSQL's xmin system column as the users table's concurrency
    /// token (ADR 0021). The column already exists on every row, so there is
    /// nothing to create: EF Core generates an AddColumn for it, which
    /// PostgreSQL would refuse, and it is removed here. The migration exists so
    /// the model snapshot records the token, as AddRefreshTokens does for
    /// refresh_tokens.
    /// </summary>
    public partial class AddUserVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
