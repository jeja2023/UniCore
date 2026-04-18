import React from "react";
import { useTheme } from "../design/theme/ThemeProvider";
import { GovernanceStatusBar } from "./data-scope-governance/GovernanceStatusBar";
import { BasicsTemplateSection } from "./data-scope-governance/BasicsTemplateSection";
import { RoleScopeSection } from "./data-scope-governance/RoleScopeSection";
import { RuleComposerSection } from "./data-scope-governance/RuleComposerSection";
import { HistoryDiffRollbackSection } from "./data-scope-governance/HistoryDiffRollbackSection";
import { GOVERNANCE_PAGE_TITLE, GOVERNANCE_SUCCESS_MESSAGES, SCOPE_OPTIONS } from "./data-scope-governance/constants";
import { useDataScopeGovernance } from "./data-scope-governance/useDataScopeGovernance";

export function DataScopeGovernancePage() {
  const { tokens } = useTheme();
  const governance = useDataScopeGovernance();
  const runWithStatus = (
    action: () => Promise<void>,
    successMessage: string
  ) => () => void governance.runAction(action, successMessage);

  return (
    <div
      style={{
        display: "grid",
        gap: tokens.space.md,
        width: "min(100%, 1680px)",
        margin: "0 auto",
      }}
    >
      <h2 style={{ margin: 0, fontSize: 20, fontWeight: 800, letterSpacing: "-0.02em" }}>{GOVERNANCE_PAGE_TITLE}</h2>
      <GovernanceStatusBar loading={governance.loading} status={governance.status} error={governance.error} />

      <BasicsTemplateSection
        loading={governance.loading}
        metadata={governance.metadata}
        templates={governance.templates}
        onLoad={runWithStatus(governance.loadGovernanceBasics, GOVERNANCE_SUCCESS_MESSAGES.LOAD_BASICS)}
        onApplyTemplate={governance.setCustomExpression}
      />

      <RoleScopeSection
        loading={governance.loading}
        role={governance.role}
        scope={governance.scope}
        expectedRevision={governance.expectedRevision}
        customExpression={governance.customExpression}
        detail={governance.detail}
        parseResult={governance.parseResult}
        scopeOptions={SCOPE_OPTIONS}
        onRoleChange={governance.setRole}
        onScopeChange={governance.setScope}
        onExpectedRevisionChange={governance.setExpectedRevision}
        onCustomExpressionChange={governance.setCustomExpression}
        onLoadRole={runWithStatus(governance.loadRoleScopeAndHistory, GOVERNANCE_SUCCESS_MESSAGES.LOAD_ROLE)}
        onSaveRole={runWithStatus(governance.saveRoleScope, GOVERNANCE_SUCCESS_MESSAGES.SAVE_ROLE)}
        onValidateAndParse={runWithStatus(
          governance.validateAndParseExpression,
          GOVERNANCE_SUCCESS_MESSAGES.VALIDATE_AND_PARSE
        )}
      />

      <RuleComposerSection
        loading={governance.loading}
        canCompose={governance.canCompose}
        rules={governance.rules}
        metadata={governance.metadata}
        onAddRule={governance.addRule}
        onCompose={runWithStatus(governance.composeExpressionByRules, GOVERNANCE_SUCCESS_MESSAGES.COMPOSE_BY_RULES)}
        onUpdateRule={governance.updateRule}
        onRemoveRule={governance.removeRule}
      />

      <HistoryDiffRollbackSection
        loading={governance.loading}
        diffFromVersion={governance.diffFromVersion}
        diffToVersion={governance.diffToVersion}
        rollbackVersion={governance.rollbackVersion}
        history={governance.history}
        diffResult={governance.diffResult}
        onDiffFromVersionChange={governance.setDiffFromVersion}
        onDiffToVersionChange={governance.setDiffToVersion}
        onRollbackVersionChange={governance.setRollbackVersion}
        onRefreshHistory={runWithStatus(governance.loadRoleScopeAndHistory, GOVERNANCE_SUCCESS_MESSAGES.REFRESH_HISTORY)}
        onQueryDiff={runWithStatus(governance.queryDiff, GOVERNANCE_SUCCESS_MESSAGES.QUERY_DIFF)}
        onRollback={runWithStatus(governance.rollbackScope, GOVERNANCE_SUCCESS_MESSAGES.ROLLBACK)}
      />
    </div>
  );
}

