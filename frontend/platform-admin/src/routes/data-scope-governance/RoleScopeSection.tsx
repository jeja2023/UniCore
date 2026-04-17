import React from "react";
import { Button } from "../../components/base/Button";
import { ListPageTemplate } from "../../components/patterns/ListPageTemplate";
import { GOVERNANCE_UI_TEXT } from "./constants";
import { nowIsoText } from "./utils";
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
  return (
    <ListPageTemplate
      title={GOVERNANCE_UI_TEXT.ROLE_SCOPE.TITLE}
      toolbar={
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <label>
            {GOVERNANCE_UI_TEXT.ROLE_SCOPE.ROLE_CODE_LABEL}
            <input value={role} onChange={(e) => onRoleChange(e.target.value)} style={{ marginLeft: 6 }} />
          </label>
          <label>
            {GOVERNANCE_UI_TEXT.ROLE_SCOPE.SCOPE_LABEL}
            <select value={scope} onChange={(e) => onScopeChange(e.target.value)} style={{ marginLeft: 6 }}>
              {scopeOptions.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </select>
          </label>
          <label>
            {GOVERNANCE_UI_TEXT.ROLE_SCOPE.EXPECTED_REVISION_LABEL}
            <input value={expectedRevision} onChange={(e) => onExpectedRevisionChange(e.target.value)} style={{ marginLeft: 6, width: 90 }} />
          </label>
          <Button onClick={onLoadRole} disabled={loading}>
            {GOVERNANCE_UI_TEXT.ROLE_SCOPE.LOAD_ROLE_BUTTON}
          </Button>
          <Button onClick={onSaveRole} variant="primary" disabled={loading}>
            {GOVERNANCE_UI_TEXT.ROLE_SCOPE.SAVE_ROLE_BUTTON}
          </Button>
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
            style={{ display: "block", width: "100%", marginTop: 6 }}
          />
        </label>
        <div style={{ display: "flex", gap: 8 }}>
          <Button onClick={onValidateAndParse} disabled={loading}>
            {GOVERNANCE_UI_TEXT.ROLE_SCOPE.VALIDATE_PARSE_BUTTON}
          </Button>
        </div>
        <div>
          {GOVERNANCE_UI_TEXT.ROLE_SCOPE.CURRENT_DETAIL_LABEL}
          {detail ? (
            <div>
              role={detail.roleCode}, scope={detail.scope}, revision={detail.revision}, updatedAt={nowIsoText(detail.updatedAt)}
            </div>
          ) : (
            "-"
          )}
        </div>
        <div>
          {GOVERNANCE_UI_TEXT.ROLE_SCOPE.PARSE_RESULT_LABEL}
          {parseResult ? (
            <div>
              <div>isValid: {String(parseResult.isValid)}</div>
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
                      {t.value} ({t.kind})
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          ) : (
            "-"
          )}
        </div>
      </div>
    </ListPageTemplate>
  );
}
