import React from "react";
import { Button } from "../../components/base/Button";
import { ListPageTemplate } from "../../components/patterns/ListPageTemplate";
import { useTheme } from "../../design/theme/ThemeProvider";
import { createGlassControlVars } from "../../styles/glass";
import { GOVERNANCE_UI_TEXT, SCOPE_OPTION_LABELS } from "./constants";
import { formatDataScopeTokenKind, nowIsoText } from "./utils";
import type { ParseResult, ScopeDetail } from "./types";

type RoleScopeSectionProps = {
  loading: boolean;
  role: string;
  scope: string;
  expectedRevision: string;
  customExpression: string;
  detail: ScopeDetail | null;
  parseResult: ParseResult | null;
  scopeOptions: readonly string[];
  onRoleChange: (value: string) => void;
  onScopeChange: (value: string) => void;
  onExpectedRevisionChange: (value: string) => void;
  onCustomExpressionChange: (value: string) => void;
  onLoadRole: () => void;
  onSaveRole: () => void;
  onValidateAndParse: () => void;
};

export function RoleScopeSection({
  loading,
  role,
  scope,
  expectedRevision,
  customExpression,
  detail,
  parseResult,
  scopeOptions,
  onRoleChange,
  onScopeChange,
  onExpectedRevisionChange,
  onCustomExpressionChange,
  onLoadRole,
  onSaveRole,
  onValidateAndParse,
}: RoleScopeSectionProps) {
  const { tokens } = useTheme();
  const glassControlStyle = createGlassControlVars(tokens);

  return (
    <ListPageTemplate
      title={GOVERNANCE_UI_TEXT.ROLE_SCOPE.TITLE}
      toolbar={
        <div style={{ display: "grid", gap: 8, gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", alignItems: "end" }}>
          <label style={{ fontSize: 12 }}>
            {GOVERNANCE_UI_TEXT.ROLE_SCOPE.ROLE_CODE_LABEL}
            <input
              value={role}
              onChange={(e) => onRoleChange(e.target.value)}
              className="glass-control"
              style={{ ...glassControlStyle, marginTop: 4, width: "100%", padding: "7px 10px" }}
            />
          </label>
          <label style={{ fontSize: 12 }}>
            {GOVERNANCE_UI_TEXT.ROLE_SCOPE.SCOPE_LABEL}
            <select
              value={scope}
              onChange={(e) => onScopeChange(e.target.value)}
              className="glass-control"
              style={{ ...glassControlStyle, marginTop: 4, width: "100%", padding: "7px 10px" }}
            >
              {scopeOptions.map((item) => (
                <option key={item} value={item}>
                  {SCOPE_OPTION_LABELS[item as keyof typeof SCOPE_OPTION_LABELS] ?? item}
                </option>
              ))}
            </select>
          </label>
          <label style={{ fontSize: 12 }}>
            {GOVERNANCE_UI_TEXT.ROLE_SCOPE.EXPECTED_REVISION_LABEL}
            <input
              value={expectedRevision}
              onChange={(e) => onExpectedRevisionChange(e.target.value)}
              className="glass-control"
              style={{ ...glassControlStyle, marginTop: 4, width: "100%", padding: "7px 10px" }}
            />
          </label>
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end", flexWrap: "wrap" }}>
            <Button onClick={onLoadRole} disabled={loading}>
              {GOVERNANCE_UI_TEXT.ROLE_SCOPE.LOAD_ROLE_BUTTON}
            </Button>
            <Button onClick={onSaveRole} variant="primary" disabled={loading}>
              {GOVERNANCE_UI_TEXT.ROLE_SCOPE.SAVE_ROLE_BUTTON}
            </Button>
          </div>
        </div>
      }
    >
      <div style={{ display: "grid", gap: 8 }}>
        <label>
          {GOVERNANCE_UI_TEXT.ROLE_SCOPE.CUSTOM_EXPRESSION_LABEL}
          <textarea
            value={customExpression}
            onChange={(e) => onCustomExpressionChange(e.target.value)}
            rows={4}
            className="glass-control"
            style={{ ...glassControlStyle, display: "block", width: "100%", marginTop: 4, padding: "8px 10px" }}
          />
        </label>
        <div style={{ display: "flex", gap: 8 }}>
          <Button onClick={onValidateAndParse} disabled={loading}>
            {GOVERNANCE_UI_TEXT.ROLE_SCOPE.VALIDATE_PARSE_BUTTON}
          </Button>
        </div>
        <div className="u-display-field">
          <div className="u-display-field__title">{GOVERNANCE_UI_TEXT.ROLE_SCOPE.CURRENT_DETAIL_LABEL}</div>
          {detail ? (
            <div style={{ lineHeight: 1.6 }}>
              角色编码：{detail.roleCode}；数据范围：
              {SCOPE_OPTION_LABELS[detail.scope as keyof typeof SCOPE_OPTION_LABELS] ?? detail.scope}（
              {detail.scope}）；版本号：{detail.revision}；更新时间：{nowIsoText(detail.updatedAt)}
            </div>
          ) : (
            "—"
          )}
        </div>
        <div className="u-display-field">
          <div className="u-display-field__title">{GOVERNANCE_UI_TEXT.ROLE_SCOPE.PARSE_RESULT_LABEL}</div>
          {parseResult ? (
            <div style={{ lineHeight: 1.6 }}>
              <div>是否有效：{String(parseResult.isValid)}</div>
              {parseResult.errorMessage ? (
                <div>
                  {GOVERNANCE_UI_TEXT.ROLE_SCOPE.PARSE_ERROR_PREFIX} {parseResult.errorMessage}
                </div>
              ) : null}
              <div>
                {GOVERNANCE_UI_TEXT.ROLE_SCOPE.PARSE_TOKENS_LABEL}
                <ul>
                  {parseResult.tokens.map((t, idx) => (
                    <li key={`${t.value}-${idx}`}>
                      {t.value}（{formatDataScopeTokenKind(t.kind)}）
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          ) : (
            "—"
          )}
        </div>
      </div>
    </ListPageTemplate>
  );
}
