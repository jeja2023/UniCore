import type { DataScopeRule } from "./types";

export const DEFAULT_ROLE_CODE = "admin";

export const SCOPE_OPTIONS = ["Self", "Department", "Tenant", "Custom"] as const;

export const DEFAULT_DATA_SCOPE_RULE: DataScopeRule = {
  field: "tenant_id",
  operator: "=",
  value: "{current_tenant_id}",
  joinWithPrevious: "AND",
  openGroupCount: 0,
  closeGroupCount: 0,
};

export const GOVERNANCE_PAGE_TITLE = "数据权限表达式治理";

export const GOVERNANCE_SUCCESS_MESSAGES = {
  LOAD_BASICS: "已加载元数据与模板。",
  LOAD_ROLE: "已读取角色数据权限。",
  SAVE_ROLE: "角色数据权限已保存。",
  VALIDATE_AND_PARSE: "表达式校验通过并完成解析。",
  COMPOSE_BY_RULES: "已按规则拼装表达式。",
  REFRESH_HISTORY: "已刷新历史版本。",
  QUERY_DIFF: "已完成版本差异比对。",
  ROLLBACK: "已回滚角色数据权限。",
} as const;

export const GOVERNANCE_UI_TEXT = {
  PROCESSING: "处理中...",
  BASICS: {
    TITLE: "1) 基础元数据与模板",
    LOAD_BUTTON: "加载治理元数据",
    APPLY_BUTTON: "套用",
    FIELDS_LABEL: "字段",
    OPERATORS_LABEL: "操作符",
    JOINERS_LABEL: "连接符",
    TEMPLATES_LABEL: "模板",
  },
  ROLE_SCOPE: {
    TITLE: "2) 角色范围配置",
    ROLE_CODE_LABEL: "角色编码：",
    SCOPE_LABEL: "范围：",
    EXPECTED_REVISION_LABEL: "期望版本：",
    LOAD_ROLE_BUTTON: "读取角色",
    SAVE_ROLE_BUTTON: "保存角色范围",
    CUSTOM_EXPRESSION_LABEL: "customExpression：",
    VALIDATE_PARSE_BUTTON: "校验并解析表达式",
    CURRENT_DETAIL_LABEL: "当前详情：",
    PARSE_RESULT_LABEL: "解析结果：",
    PARSE_ERROR_PREFIX: "error:",
    PARSE_TOKENS_LABEL: "tokens:",
  },
  RULE_COMPOSER: {
    TITLE: "3) 可视化规则拼装",
    ADD_RULE_BUTTON: "新增规则",
    COMPOSE_BUTTON: "规则拼装为表达式",
  },
  HISTORY: {
    TITLE: "4) 版本历史、差异与回滚",
    REFRESH_BUTTON: "刷新历史",
    FROM_LABEL: "from:",
    TO_LABEL: "to:",
    QUERY_DIFF_BUTTON: "查询差异",
    ROLLBACK_VERSION_LABEL: "回滚版本:",
    ROLLBACK_BUTTON: "回滚",
  },
} as const;
