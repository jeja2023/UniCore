import React from "react";
import { Button } from "../../components/base/Button";
import { ListPageTemplate } from "../../components/patterns/ListPageTemplate";
import { GOVERNANCE_UI_TEXT, JOINER_DISPLAY_LABELS, OPERATOR_DISPLAY_LABELS } from "./constants";
import type { DataScopeMetadata, DataScopeTemplate } from "./types";
import { formatDataScopeFieldOptionLabel } from "./utils";

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
        <div className="u-display-field" style={{ display: "grid", gap: 4 }}>
          <div className="u-display-field__title">{GOVERNANCE_UI_TEXT.BASICS.FIELDS_LABEL}</div>
          <div>{metadata?.fields.map((f) => formatDataScopeFieldOptionLabel(f)).join("；") || "—"}</div>
        </div>
        <div className="u-display-field" style={{ display: "grid", gap: 4 }}>
          <div className="u-display-field__title">{GOVERNANCE_UI_TEXT.BASICS.OPERATORS_LABEL}</div>
          <div>{metadata?.operators.map((op) => OPERATOR_DISPLAY_LABELS[op] ?? op).join("，") || "—"}</div>
        </div>
        <div className="u-display-field" style={{ display: "grid", gap: 4 }}>
          <div className="u-display-field__title">{GOVERNANCE_UI_TEXT.BASICS.JOINERS_LABEL}</div>
          <div>{metadata?.joiners.map((j) => JOINER_DISPLAY_LABELS[j] ?? j).join("，") || "—"}</div>
        </div>
        <div>
          <div className="u-display-field__title">{GOVERNANCE_UI_TEXT.BASICS.TEMPLATES_LABEL}</div>
          <ul style={{ margin: 0, paddingLeft: 18, display: "grid", gap: 6 }}>
            {templates.map((t) => (
              <li key={t.code} className="u-display-field" style={{ listStyle: "disc", marginLeft: -4 }}>
                <div>
                  <b>{t.name}</b>：{t.expression}
                </div>
                <div>
                  <Button onClick={() => onApplyTemplate(t.expression)} disabled={loading}>
                    {GOVERNANCE_UI_TEXT.BASICS.APPLY_BUTTON}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </ListPageTemplate>
  );
}
