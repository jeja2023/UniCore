using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HttpMethod",
                table: "AuditEvents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestPath",
                table: "AuditEvents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "AuditEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "AuditEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HttpMethod",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "RequestPath",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "AuditEvents");
        }
    }
}
