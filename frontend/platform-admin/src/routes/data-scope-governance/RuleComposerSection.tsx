import React from "react";
import { Button } from "../../components/base/Button";
import { ListPageTemplate } from "../../components/patterns/ListPageTemplate";
import { GOVERNANCE_UI_TEXT } from "./constants";
import { RulesTable } from "./RulesTable";
import type { DataScopeMetadata, DataScopeRule } from "./types";

type RuleComposerSectionProps = {
  loading: boolean;
  canCompose: boolean;
  rules: DataScopeRule[];
  metadata: DataScopeMetadata | null;
  onAddRule: () => void;
  onCompose: () => void;
  onUpdateRule: (index: number, patch: Partial<DataScopeRule>) => void;
  onRemoveRule: (index: number) => void;
};

export function RuleComposerSection({
  loading,
  canCompose,
  rules,
  metadata,
  onAddRule,
  onCompose,
  onUpdateRule,
  onRemoveRule,
}: RuleComposerSectionProps) {
  return (
    <ListPageTemplate
      title={GOVERNANCE_UI_TEXT.RULE_COMPOSER.TITLE}
      toolbar={
        <div style={{ display: "flex", gap: 8 }}>
          <Button onClick={onAddRule} disabled={loading}>
            {GOVERNANCE_UI_TEXT.RULE_COMPOSER.ADD_RULE_BUTTON}
          </Button>
          <Button onClick={onCompose} variant="primary" disabled={!canCompose || loading}>
            {GOVERNANCE_UI_TEXT.RULE_COMPOSER.COMPOSE_BUTTON}
          </Button>
        </div>
      }
    >
      <RulesTable rules={rules} metadata={metadata} loading={loading} onUpdateRule={onUpdateRule} onRemoveRule={onRemoveRule} />
    </ListPageTemplate>
  );
}
