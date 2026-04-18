using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    public partial class AddAuditExportRetryAndDlq : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "AuditExportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "AuditExportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "AuditExportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAt",
                table: "AuditExportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeadLettered",
                table: "AuditExportJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AuditExportJobs_Status_DeadLettered_NextAttemptAt",
                table: "AuditExportJobs",
                columns: new[] { "Status", "DeadLettered", "NextAttemptAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditExportJobs_Status_DeadLettered_NextAttemptAt",
                table: "AuditExportJobs");

            migrationBuilder.DropColumn(name: "RetryCount", table: "AuditExportJobs");
            migrationBuilder.DropColumn(name: "MaxRetries", table: "AuditExportJobs");
            migrationBuilder.DropColumn(name: "NextAttemptAt", table: "AuditExportJobs");
            migrationBuilder.DropColumn(name: "LastAttemptAt", table: "AuditExportJobs");
            migrationBuilder.DropColumn(name: "DeadLettered", table: "AuditExportJobs");
        }
    }
}

