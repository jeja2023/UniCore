using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChineseComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "Users",
                comment: "用户表");

            migrationBuilder.AlterTable(
                name: "UserRoles",
                comment: "用户角色关联表");

            migrationBuilder.AlterTable(
                name: "TenantSettings",
                comment: "租户配置表");

            migrationBuilder.AlterTable(
                name: "Tenants",
                comment: "租户表");

            migrationBuilder.AlterTable(
                name: "StoredFileObjects",
                comment: "文件存储对象表");

            migrationBuilder.AlterTable(
                name: "ScheduledJobs",
                comment: "调度任务表");

            migrationBuilder.AlterTable(
                name: "Roles",
                comment: "角色表");

            migrationBuilder.AlterTable(
                name: "RolePermissions",
                comment: "角色权限关联表");

            migrationBuilder.AlterTable(
                name: "RoleDataScopes",
                comment: "角色数据范围表");

            migrationBuilder.AlterTable(
                name: "RoleDataScopeHistories",
                comment: "角色数据范围历史表");

            migrationBuilder.AlterTable(
                name: "RefreshTokens",
                comment: "刷新令牌表");

            migrationBuilder.AlterTable(
                name: "NotificationTemplateVersions",
                comment: "通知模板版本表");

            migrationBuilder.AlterTable(
                name: "NotificationTemplates",
                comment: "通知模板表");

            migrationBuilder.AlterTable(
                name: "NotificationMessages",
                comment: "通知消息表");

            migrationBuilder.AlterTable(
                name: "ExternalIdentityLinks",
                comment: "外部身份绑定表");

            migrationBuilder.AlterTable(
                name: "DictionaryItems",
                comment: "字典项表");

            migrationBuilder.AlterTable(
                name: "AuditExportJobs",
                comment: "审计导出任务表");

            migrationBuilder.AlterTable(
                name: "AuditEvents",
                comment: "审计事件表");

            migrationBuilder.AlterTable(
                name: "AuditEntityChanges",
                comment: "实体变更审计表");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "用户名",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "text",
                nullable: false,
                comment: "密码哈希",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "Users",
                type: "boolean",
                nullable: false,
                comment: "是否启用",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "text",
                nullable: false,
                comment: "显示名称",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DepartmentCode",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "部门编码",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Users",
                type: "uuid",
                nullable: false,
                comment: "用户标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "UserRoles",
                type: "uuid",
                nullable: false,
                comment: "角色标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "UserRoles",
                type: "uuid",
                nullable: false,
                comment: "用户标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "TenantSettings",
                type: "timestamp with time zone",
                nullable: false,
                comment: "更新时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "TenantSettings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "SettingValue",
                table: "TenantSettings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                comment: "配置值",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "SettingKey",
                table: "TenantSettings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "配置键",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantSettingId",
                table: "TenantSettings",
                type: "uuid",
                nullable: false,
                comment: "租户配置标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantName",
                table: "Tenants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "租户名称",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                comment: "是否启用",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: false,
                comment: "创建时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "Tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "StoredFileObjects",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StoredAt",
                table: "StoredFileObjects",
                type: "timestamp with time zone",
                nullable: false,
                comment: "入库时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "StoredFileObjects",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "提供方",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "ObjectKey",
                table: "StoredFileObjects",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                comment: "对象键",
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<long>(
                name: "Length",
                table: "StoredFileObjects",
                type: "bigint",
                nullable: false,
                comment: "文件大小",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "StoredFileObjects",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                comment: "文件名",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "FileId",
                table: "StoredFileObjects",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "文件业务标识",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "StoredFileObjects",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "内容类型",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Bucket",
                table: "StoredFileObjects",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "存储桶",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<Guid>(
                name: "StoredFileObjectId",
                table: "StoredFileObjects",
                type: "uuid",
                nullable: false,
                comment: "文件对象标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "ScheduledJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ScheduledJobs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "状态",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RunAt",
                table: "ScheduledJobs",
                type: "timestamp with time zone",
                nullable: false,
                comment: "计划执行时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "RetryCount",
                table: "ScheduledJobs",
                type: "integer",
                nullable: false,
                comment: "重试次数",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Payload",
                table: "ScheduledJobs",
                type: "text",
                nullable: false,
                comment: "负载数据",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "MaxRetries",
                table: "ScheduledJobs",
                type: "integer",
                nullable: false,
                comment: "最大重试次数",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "JobType",
                table: "ScheduledJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "任务类型",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "FinishedAt",
                table: "ScheduledJobs",
                type: "timestamp with time zone",
                nullable: true,
                comment: "完成时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Error",
                table: "ScheduledJobs",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                comment: "错误信息",
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "ScheduledJobs",
                type: "timestamp with time zone",
                nullable: false,
                comment: "创建时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduledJobId",
                table: "ScheduledJobs",
                type: "uuid",
                nullable: false,
                comment: "调度任务标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "Roles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                table: "Roles",
                type: "text",
                nullable: false,
                comment: "角色名称",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "RoleCode",
                table: "Roles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "角色编码",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "Roles",
                type: "uuid",
                nullable: false,
                comment: "角色标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionCode",
                table: "RolePermissions",
                type: "text",
                nullable: false,
                comment: "权限编码",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "RolePermissions",
                type: "uuid",
                nullable: false,
                comment: "角色标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "RoleDataScopes",
                type: "timestamp with time zone",
                nullable: false,
                comment: "更新时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                table: "RoleDataScopes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "范围",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<int>(
                name: "Revision",
                table: "RoleDataScopes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "修订号",
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "CustomExpression",
                table: "RoleDataScopes",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                comment: "自定义表达式",
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "RoleDataScopes",
                type: "uuid",
                nullable: false,
                comment: "角色标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "RoleDataScopeHistories",
                type: "integer",
                nullable: false,
                comment: "版本号",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                table: "RoleDataScopeHistories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "范围",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "RoleDataScopeHistories",
                type: "uuid",
                nullable: false,
                comment: "角色标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "CustomExpression",
                table: "RoleDataScopeHistories",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                comment: "自定义表达式",
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChangedBy",
                table: "RoleDataScopeHistories",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "变更人",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ChangedAt",
                table: "RoleDataScopeHistories",
                type: "timestamp with time zone",
                nullable: false,
                comment: "变更时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleDataScopeHistoryId",
                table: "RoleDataScopeHistories",
                type: "uuid",
                nullable: false,
                comment: "角色数据范围历史标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                comment: "用户标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "令牌值",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<bool>(
                name: "Revoked",
                table: "RefreshTokens",
                type: "boolean",
                nullable: false,
                comment: "是否已撤销",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: false,
                comment: "过期时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "RefreshTokenId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                comment: "刷新令牌标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "NotificationTemplateVersions",
                type: "integer",
                nullable: false,
                comment: "版本号",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "NotificationTemplateVersions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "标题",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "NotificationTemplateVersions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "TemplateCode",
                table: "NotificationTemplateVersions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "模板编码",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<Guid>(
                name: "NotificationTemplateId",
                table: "NotificationTemplateVersions",
                type: "uuid",
                nullable: false,
                comment: "通知模板标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "NotificationTemplateVersions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                comment: "内容",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "ChangedBy",
                table: "NotificationTemplateVersions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "变更人",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ChangedAt",
                table: "NotificationTemplateVersions",
                type: "timestamp with time zone",
                nullable: false,
                comment: "变更时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "NotificationTemplateVersionId",
                table: "NotificationTemplateVersions",
                type: "uuid",
                nullable: false,
                comment: "通知模板版本标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "NotificationTemplates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "UpdatedBy字段",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "NotificationTemplates",
                type: "timestamp with time zone",
                nullable: false,
                comment: "更新时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "NotificationTemplates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "TemplateCode",
                table: "NotificationTemplates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "模板编码",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "NotificationTemplates",
                type: "boolean",
                nullable: false,
                comment: "是否启用",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<int>(
                name: "CurrentVersion",
                table: "NotificationTemplates",
                type: "integer",
                nullable: false,
                comment: "当前版本",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "NotificationTemplateId",
                table: "NotificationTemplates",
                type: "uuid",
                nullable: false,
                comment: "通知模板标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "NotificationMessages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "标题",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "NotificationMessages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "NotificationMessages",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "状态",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SentAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: true,
                comment: "发送时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RetryCount",
                table: "NotificationMessages",
                type: "integer",
                nullable: false,
                comment: "重试次数",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Receiver",
                table: "NotificationMessages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                comment: "接收方",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Error",
                table: "NotificationMessages",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                comment: "错误信息",
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: false,
                comment: "创建时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "NotificationMessages",
                type: "text",
                nullable: false,
                comment: "内容",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "NotificationMessages",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "渠道",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<Guid>(
                name: "NotificationMessageId",
                table: "NotificationMessages",
                type: "uuid",
                nullable: false,
                comment: "通知消息标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "ExternalIdentityLinks",
                type: "uuid",
                nullable: false,
                comment: "用户标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "ExternalIdentityLinks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "ExternalIdentityLinks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "提供方",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LinkedAt",
                table: "ExternalIdentityLinks",
                type: "timestamp with time zone",
                nullable: false,
                comment: "绑定时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalUserId",
                table: "ExternalIdentityLinks",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "外部用户标识",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<Guid>(
                name: "ExternalIdentityLinkId",
                table: "ExternalIdentityLinks",
                type: "uuid",
                nullable: false,
                comment: "外部身份绑定标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "DictionaryItems",
                type: "timestamp with time zone",
                nullable: false,
                comment: "更新时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "DictionaryItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<int>(
                name: "Sort",
                table: "DictionaryItems",
                type: "integer",
                nullable: false,
                comment: "排序值",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ItemName",
                table: "DictionaryItems",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                comment: "项名称",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ItemCode",
                table: "DictionaryItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "项编码",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "DictionaryItems",
                type: "boolean",
                nullable: false,
                comment: "是否启用",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "DictionaryCode",
                table: "DictionaryItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "字典编码",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<Guid>(
                name: "DictionaryItemId",
                table: "DictionaryItems",
                type: "uuid",
                nullable: false,
                comment: "字典项标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "AuditExportJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AuditExportJobs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "状态",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Error",
                table: "AuditExportJobs",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                comment: "错误信息",
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CsvContent",
                table: "AuditExportJobs",
                type: "text",
                nullable: true,
                comment: "CSV内容",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "AuditExportJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "创建人",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "AuditExportJobs",
                type: "timestamp with time zone",
                nullable: false,
                comment: "创建时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "AuditExportJobs",
                type: "timestamp with time zone",
                nullable: true,
                comment: "完成时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Completed",
                table: "AuditExportJobs",
                type: "boolean",
                nullable: false,
                comment: "是否完成",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<Guid>(
                name: "JobId",
                table: "AuditExportJobs",
                type: "uuid",
                nullable: false,
                comment: "任务标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                table: "AuditEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                comment: "链路追踪标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "AuditEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<int>(
                name: "StatusCode",
                table: "AuditEvents",
                type: "integer",
                nullable: true,
                comment: "状态码",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestPath",
                table: "AuditEvents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                comment: "请求路径",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "OccurredAt",
                table: "AuditEvents",
                type: "timestamp with time zone",
                nullable: false,
                comment: "发生时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "AuditEvents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "级别",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "HttpMethod",
                table: "AuditEvents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                comment: "请求方法",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventCode",
                table: "AuditEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "事件编码",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "AuditEvents",
                type: "text",
                nullable: false,
                comment: "描述",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Actor",
                table: "AuditEvents",
                type: "text",
                nullable: false,
                comment: "操作人",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "AuditEventId",
                table: "AuditEvents",
                type: "uuid",
                nullable: false,
                comment: "审计事件标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "AuditEntityChanges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "OccurredAt",
                table: "AuditEntityChanges",
                type: "timestamp with time zone",
                nullable: false,
                comment: "发生时间",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "EntityName",
                table: "AuditEntityChanges",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "实体名称",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "AuditEntityChanges",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                comment: "实体标识",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ChangesJson",
                table: "AuditEntityChanges",
                type: "text",
                nullable: false,
                comment: "变更内容JSON",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Actor",
                table: "AuditEntityChanges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "操作人",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<Guid>(
                name: "AuditEntityChangeId",
                table: "AuditEntityChanges",
                type: "uuid",
                nullable: false,
                comment: "实体变更审计标识",
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "Users",
                oldComment: "用户表");

            migrationBuilder.AlterTable(
                name: "UserRoles",
                oldComment: "用户角色关联表");

            migrationBuilder.AlterTable(
                name: "TenantSettings",
                oldComment: "租户配置表");

            migrationBuilder.AlterTable(
                name: "Tenants",
                oldComment: "租户表");

            migrationBuilder.AlterTable(
                name: "StoredFileObjects",
                oldComment: "文件存储对象表");

            migrationBuilder.AlterTable(
                name: "ScheduledJobs",
                oldComment: "调度任务表");

            migrationBuilder.AlterTable(
                name: "Roles",
                oldComment: "角色表");

            migrationBuilder.AlterTable(
                name: "RolePermissions",
                oldComment: "角色权限关联表");

            migrationBuilder.AlterTable(
                name: "RoleDataScopes",
                oldComment: "角色数据范围表");

            migrationBuilder.AlterTable(
                name: "RoleDataScopeHistories",
                oldComment: "角色数据范围历史表");

            migrationBuilder.AlterTable(
                name: "RefreshTokens",
                oldComment: "刷新令牌表");

            migrationBuilder.AlterTable(
                name: "NotificationTemplateVersions",
                oldComment: "通知模板版本表");

            migrationBuilder.AlterTable(
                name: "NotificationTemplates",
                oldComment: "通知模板表");

            migrationBuilder.AlterTable(
                name: "NotificationMessages",
                oldComment: "通知消息表");

            migrationBuilder.AlterTable(
                name: "ExternalIdentityLinks",
                oldComment: "外部身份绑定表");

            migrationBuilder.AlterTable(
                name: "DictionaryItems",
                oldComment: "字典项表");

            migrationBuilder.AlterTable(
                name: "AuditExportJobs",
                oldComment: "审计导出任务表");

            migrationBuilder.AlterTable(
                name: "AuditEvents",
                oldComment: "审计事件表");

            migrationBuilder.AlterTable(
                name: "AuditEntityChanges",
                oldComment: "实体变更审计表");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "用户名");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "密码哈希");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "Users",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "是否启用");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "显示名称");

            migrationBuilder.AlterColumn<string>(
                name: "DepartmentCode",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "部门编码");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Users",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "用户标识");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "UserRoles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "角色标识");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "UserRoles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "用户标识");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "TenantSettings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "更新时间");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "TenantSettings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "SettingValue",
                table: "TenantSettings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldComment: "配置值");

            migrationBuilder.AlterColumn<string>(
                name: "SettingKey",
                table: "TenantSettings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "配置键");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantSettingId",
                table: "TenantSettings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "租户配置标识");

            migrationBuilder.AlterColumn<string>(
                name: "TenantName",
                table: "Tenants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "租户名称");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "是否启用");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "创建时间");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "Tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "StoredFileObjects",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StoredAt",
                table: "StoredFileObjects",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "入库时间");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "StoredFileObjects",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "提供方");

            migrationBuilder.AlterColumn<string>(
                name: "ObjectKey",
                table: "StoredFileObjects",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldComment: "对象键");

            migrationBuilder.AlterColumn<long>(
                name: "Length",
                table: "StoredFileObjects",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "文件大小");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "StoredFileObjects",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldComment: "文件名");

            migrationBuilder.AlterColumn<string>(
                name: "FileId",
                table: "StoredFileObjects",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "文件业务标识");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "StoredFileObjects",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "内容类型");

            migrationBuilder.AlterColumn<string>(
                name: "Bucket",
                table: "StoredFileObjects",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "存储桶");

            migrationBuilder.AlterColumn<Guid>(
                name: "StoredFileObjectId",
                table: "StoredFileObjects",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "文件对象标识");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "ScheduledJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ScheduledJobs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "状态");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RunAt",
                table: "ScheduledJobs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "计划执行时间");

            migrationBuilder.AlterColumn<int>(
                name: "RetryCount",
                table: "ScheduledJobs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "重试次数");

            migrationBuilder.AlterColumn<string>(
                name: "Payload",
                table: "ScheduledJobs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "负载数据");

            migrationBuilder.AlterColumn<int>(
                name: "MaxRetries",
                table: "ScheduledJobs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "最大重试次数");

            migrationBuilder.AlterColumn<string>(
                name: "JobType",
                table: "ScheduledJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "任务类型");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "FinishedAt",
                table: "ScheduledJobs",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "完成时间");

            migrationBuilder.AlterColumn<string>(
                name: "Error",
                table: "ScheduledJobs",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true,
                oldComment: "错误信息");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "ScheduledJobs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "创建时间");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduledJobId",
                table: "ScheduledJobs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "调度任务标识");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "Roles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                table: "Roles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "角色名称");

            migrationBuilder.AlterColumn<string>(
                name: "RoleCode",
                table: "Roles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "角色编码");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "Roles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "角色标识");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionCode",
                table: "RolePermissions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "权限编码");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "RolePermissions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "角色标识");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "RoleDataScopes",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "更新时间");

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                table: "RoleDataScopes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "范围");

            migrationBuilder.AlterColumn<int>(
                name: "Revision",
                table: "RoleDataScopes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0,
                oldComment: "修订号");

            migrationBuilder.AlterColumn<string>(
                name: "CustomExpression",
                table: "RoleDataScopes",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true,
                oldComment: "自定义表达式");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "RoleDataScopes",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "角色标识");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "RoleDataScopeHistories",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "版本号");

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                table: "RoleDataScopeHistories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "范围");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "RoleDataScopeHistories",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "角色标识");

            migrationBuilder.AlterColumn<string>(
                name: "CustomExpression",
                table: "RoleDataScopeHistories",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true,
                oldComment: "自定义表达式");

            migrationBuilder.AlterColumn<string>(
                name: "ChangedBy",
                table: "RoleDataScopeHistories",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "变更人");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ChangedAt",
                table: "RoleDataScopeHistories",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "变更时间");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleDataScopeHistoryId",
                table: "RoleDataScopeHistories",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "角色数据范围历史标识");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "用户标识");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "令牌值");

            migrationBuilder.AlterColumn<bool>(
                name: "Revoked",
                table: "RefreshTokens",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "是否已撤销");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "过期时间");

            migrationBuilder.AlterColumn<Guid>(
                name: "RefreshTokenId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "刷新令牌标识");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "NotificationTemplateVersions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "版本号");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "NotificationTemplateVersions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "标题");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "NotificationTemplateVersions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "TemplateCode",
                table: "NotificationTemplateVersions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "模板编码");

            migrationBuilder.AlterColumn<Guid>(
                name: "NotificationTemplateId",
                table: "NotificationTemplateVersions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "通知模板标识");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "NotificationTemplateVersions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldComment: "内容");

            migrationBuilder.AlterColumn<string>(
                name: "ChangedBy",
                table: "NotificationTemplateVersions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "变更人");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ChangedAt",
                table: "NotificationTemplateVersions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "变更时间");

            migrationBuilder.AlterColumn<Guid>(
                name: "NotificationTemplateVersionId",
                table: "NotificationTemplateVersions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "通知模板版本标识");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "NotificationTemplates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "UpdatedBy字段");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "NotificationTemplates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "更新时间");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "NotificationTemplates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "TemplateCode",
                table: "NotificationTemplates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "模板编码");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "NotificationTemplates",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "是否启用");

            migrationBuilder.AlterColumn<int>(
                name: "CurrentVersion",
                table: "NotificationTemplates",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "当前版本");

            migrationBuilder.AlterColumn<Guid>(
                name: "NotificationTemplateId",
                table: "NotificationTemplates",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "通知模板标识");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "NotificationMessages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "标题");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "NotificationMessages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "NotificationMessages",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "状态");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SentAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "发送时间");

            migrationBuilder.AlterColumn<int>(
                name: "RetryCount",
                table: "NotificationMessages",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "重试次数");

            migrationBuilder.AlterColumn<string>(
                name: "Receiver",
                table: "NotificationMessages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "接收方");

            migrationBuilder.AlterColumn<string>(
                name: "Error",
                table: "NotificationMessages",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true,
                oldComment: "错误信息");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "创建时间");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "NotificationMessages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "内容");

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "NotificationMessages",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "渠道");

            migrationBuilder.AlterColumn<Guid>(
                name: "NotificationMessageId",
                table: "NotificationMessages",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "通知消息标识");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "ExternalIdentityLinks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "用户标识");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "ExternalIdentityLinks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "ExternalIdentityLinks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "提供方");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LinkedAt",
                table: "ExternalIdentityLinks",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "绑定时间");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalUserId",
                table: "ExternalIdentityLinks",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "外部用户标识");

            migrationBuilder.AlterColumn<Guid>(
                name: "ExternalIdentityLinkId",
                table: "ExternalIdentityLinks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "外部身份绑定标识");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "DictionaryItems",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "更新时间");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "DictionaryItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<int>(
                name: "Sort",
                table: "DictionaryItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "排序值");

            migrationBuilder.AlterColumn<string>(
                name: "ItemName",
                table: "DictionaryItems",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldComment: "项名称");

            migrationBuilder.AlterColumn<string>(
                name: "ItemCode",
                table: "DictionaryItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "项编码");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "DictionaryItems",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "是否启用");

            migrationBuilder.AlterColumn<string>(
                name: "DictionaryCode",
                table: "DictionaryItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "字典编码");

            migrationBuilder.AlterColumn<Guid>(
                name: "DictionaryItemId",
                table: "DictionaryItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "字典项标识");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "AuditExportJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AuditExportJobs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "状态");

            migrationBuilder.AlterColumn<string>(
                name: "Error",
                table: "AuditExportJobs",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true,
                oldComment: "错误信息");

            migrationBuilder.AlterColumn<string>(
                name: "CsvContent",
                table: "AuditExportJobs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "CSV内容");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "AuditExportJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "创建人");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "AuditExportJobs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "创建时间");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "AuditExportJobs",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "完成时间");

            migrationBuilder.AlterColumn<bool>(
                name: "Completed",
                table: "AuditExportJobs",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "是否完成");

            migrationBuilder.AlterColumn<Guid>(
                name: "JobId",
                table: "AuditExportJobs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "任务标识");

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                table: "AuditEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "链路追踪标识");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "AuditEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<int>(
                name: "StatusCode",
                table: "AuditEvents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "状态码");

            migrationBuilder.AlterColumn<string>(
                name: "RequestPath",
                table: "AuditEvents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "请求路径");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "OccurredAt",
                table: "AuditEvents",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "发生时间");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "AuditEvents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "级别");

            migrationBuilder.AlterColumn<string>(
                name: "HttpMethod",
                table: "AuditEvents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true,
                oldComment: "请求方法");

            migrationBuilder.AlterColumn<string>(
                name: "EventCode",
                table: "AuditEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "事件编码");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "AuditEvents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "描述");

            migrationBuilder.AlterColumn<string>(
                name: "Actor",
                table: "AuditEvents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "操作人");

            migrationBuilder.AlterColumn<Guid>(
                name: "AuditEventId",
                table: "AuditEvents",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "审计事件标识");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "AuditEntityChanges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "OccurredAt",
                table: "AuditEntityChanges",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "发生时间");

            migrationBuilder.AlterColumn<string>(
                name: "EntityName",
                table: "AuditEntityChanges",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "实体名称");

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "AuditEntityChanges",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "实体标识");

            migrationBuilder.AlterColumn<string>(
                name: "ChangesJson",
                table: "AuditEntityChanges",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "变更内容JSON");

            migrationBuilder.AlterColumn<string>(
                name: "Actor",
                table: "AuditEntityChanges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "操作人");

            migrationBuilder.AlterColumn<Guid>(
                name: "AuditEntityChangeId",
                table: "AuditEntityChanges",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "实体变更审计标识");
        }
    }
}
