import React from "react";
import { Button } from "../../components/base/Button";
import { ListPageTemplate } from "../../components/patterns/ListPageTemplate";
import { GOVERNANCE_UI_TEXT } from "./constants";
import type { DataScopeMetadata, DataScopeTemplate } from "./types";

type BasicsTemplateSectionProps = {
  loading: boolean;
  metadata: DataScopeMetadata | null;
  templates: DataScopeTemplate[];
  onLoad: () => void;
  onApplyTemplate: (expression: string) => void;
};

export function BasicsTemplateSection({
  loading,
  metadata,
  templates,
  onLoad,
  onApplyTemplate,
}: BasicsTemplateSectionProps) {
  return (
    <ListPageTemplate
      title={GOVERNANCE_UI_TEXT.BASICS.TITLE}
      toolbar={
        <div style={{ display: "flex", gap: 8 }}>
          <Button onClick={onLoad} variant="primary" disabled={loading}>
            {GOVERNANCE_UI_TEXT.BASICS.LOAD_BUTTON}
          </Button>
        </div>
      }
    >
      <div style={{ display: "grid", gap: 8 }}>
        <div>{GOVERNANCE_UI_TEXT.BASICS.FIELDS_LABEL}：{metadata?.fields.map((f) => `${f.code}(${f.type})`).join(", ") || "-"}</div>
        <div>{GOVERNANCE_UI_TEXT.BASICS.OPERATORS_LABEL}：{metadata?.operators.join(", ") || "-"}</div>
        <div>{GOVERNANCE_UI_TEXT.BASICS.JOINERS_LABEL}：{metadata?.joiners.join(", ") || "-"}</div>
        <div>
          {GOVERNANCE_UI_TEXT.BASICS.TEMPLATES_LABEL}：
          <ul>
            {templates.map((t) => (
              <li key={t.code}>
                <b>{t.name}</b>：{t.expression}{" "}
                <Button onClick={() => onApplyTemplate(t.expression)} disabled={loading}>
                  {GOVERNANCE_UI_TEXT.BASICS.APPLY_BUTTON}
                </Button>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </ListPageTemplate>
  );
}
