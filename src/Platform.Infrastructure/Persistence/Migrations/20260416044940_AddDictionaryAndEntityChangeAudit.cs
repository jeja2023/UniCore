using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDictionaryAndEntityChangeAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntityChanges",
                columns: table => new
                {
                    AuditEntityChangeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Actor = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangesJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntityChanges", x => x.AuditEntityChangeId);
                });

            migrationBuilder.CreateTable(
                name: "DictionaryItems",
                columns: table => new
                {
                    DictionaryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DictionaryCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DictionaryItems", x => x.DictionaryItemId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntityChanges_TenantId_EntityName_EntityId_OccurredAt",
                table: "AuditEntityChanges",
                columns: new[] { "TenantId", "EntityName", "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DictionaryItems_TenantId_DictionaryCode_ItemCode",
                table: "DictionaryItems",
                columns: new[] { "TenantId", "DictionaryCode", "ItemCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DictionaryItems_TenantId_DictionaryCode_Sort",
                table: "DictionaryItems",
                columns: new[] { "TenantId", "DictionaryCode", "Sort" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntityChanges");

            migrationBuilder.DropTable(
                name: "DictionaryItems");
        }
    }
}
