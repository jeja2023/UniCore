import { useEffect, useMemo, useState } from "react";
import { getErrorMessage } from "../../utils/errorMessage";
import { DEFAULT_DATA_SCOPE_RULE, DEFAULT_ROLE_CODE } from "./constants";
import {
  composeDataScopeExpressionByRules,
  fetchGovernanceMetadataAndTemplates,
  fetchRoleDataScopeDiff,
  fetchRoleScopeAndHistory,
  rollbackRoleDataScope,
  saveRoleDataScope,
  validateAndParseDataScopeExpression,
} from "./api";
import type {
  DataScopeMetadata,
  DataScopeRule,
  DataScopeTemplate,
  ParseResult,
  ScopeDetail,
  ScopeDiff,
  ScopeHistoryItem,
} from "./types";

export function useDataScopeGovernance() {
  const [role, setRole] = useState(DEFAULT_ROLE_CODE);
  const [scope, setScope] = useState("Custom");
  const [customExpression, setCustomExpression] = useState("");
  const [expectedRevision, setExpectedRevision] = useState("");
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const [metadata, setMetadata] = useState<DataScopeMetadata | null>(null);
  const [templates, setTemplates] = useState<DataScopeTemplate[]>([]);
  const [detail, setDetail] = useState<ScopeDetail | null>(null);
  const [history, setHistory] = useState<ScopeHistoryItem[]>([]);
  const [parseResult, setParseResult] = useState<ParseResult | null>(null);
  const [diffResult, setDiffResult] = useState<ScopeDiff | null>(null);
  const [diffFromVersion, setDiffFromVersion] = useState("");
  const [diffToVersion, setDiffToVersion] = useState("");
  const [rollbackVersion, setRollbackVersion] = useState("");
  const [rules, setRules] = useState<DataScopeRule[]>([{ ...DEFAULT_DATA_SCOPE_RULE }]);

  const canCompose = useMemo(
    () => rules.length > 0 && rules.every((x) => x.field && x.operator && x.value),
    [rules]
  );

  async function runAction(action: () => Promise<void>, okMessage: string) {
    setLoading(true);
    setStatus(null);
    setError(null);
    try {
      await action();
      setStatus(okMessage);
    } catch (err: unknown) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  async function loadGovernanceBasics() {
    const data = await fetchGovernanceMetadataAndTemplates();
    setMetadata(data.metadata);
    setTemplates(data.templates);
  }

  async function loadRoleScopeAndHistory() {
    const normalized = role.trim();
    if (!normalized) {
      throw new Error("请先输入角色编码。");
    }
    const data = await fetchRoleScopeAndHistory(normalized);
    setDetail(data.detail);
    setHistory(data.history);
    setScope(data.detail.scope || "Self");
    setCustomExpression(data.detail.customExpression ?? "");
    setExpectedRevision(data.detail.revision.toString());
  }

  async function validateAndParseExpression() {
    const parsed = await validateAndParseDataScopeExpression(customExpression);
    setParseResult(parsed);
  }

  async function composeExpressionByRules() {
    const expression = await composeDataScopeExpressionByRules(rules);
    setCustomExpression(expression);
  }

  async function saveRoleScope() {
    const normalized = role.trim();
    if (!normalized) {
      throw new Error("请先输入角色编码。");
    }
    await saveRoleDataScope({
      roleCode: normalized,
      scope,
      customExpression,
      expectedRevision,
    });
    await loadRoleScopeAndHistory();
  }

  async function queryDiff() {
    const normalized = role.trim();
    const fromVersion = Number(diffFromVersion);
    const toVersion = Number(diffToVersion);
    if (!normalized || !Number.isInteger(fromVersion) || !Number.isInteger(toVersion)) {
      throw new Error("请填写角色编码与合法版本号。");
    }
    const diff = await fetchRoleDataScopeDiff({ roleCode: normalized, fromVersion, toVersion });
    setDiffResult(diff);
  }

  async function rollbackScope() {
    const normalized = role.trim();
    const targetVersion = Number(rollbackVersion);
    if (!normalized || !Number.isInteger(targetVersion)) {
      throw new Error("请填写角色编码与合法回滚版本。");
    }
    await rollbackRoleDataScope({ roleCode: normalized, targetVersion });
    await loadRoleScopeAndHistory();
  }

  function addRule() {
    const firstField = metadata?.fields?.[0]?.code ?? "tenant_id";
    const firstOperator = metadata?.operators?.[0] ?? "=";
    setRules((prev) => [
      ...prev,
      {
        field: firstField,
        operator: firstOperator,
        value: "",
        joinWithPrevious: "AND",
        openGroupCount: 0,
        closeGroupCount: 0,
      },
    ]);
  }

  function updateRule(index: number, patch: Partial<DataScopeRule>) {
    setRules((prev) => prev.map((item, i) => (i === index ? { ...item, ...patch } : item)));
  }

  function removeRule(index: number) {
    setRules((prev) => prev.filter((_, i) => i !== index));
  }

  useEffect(() => {
    void runAction(async () => {
      await loadGovernanceBasics();
      await loadRoleScopeAndHistory();
    }, "已自动加载治理元数据与角色配置。");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return {
    loading,
    status,
    error,
    role,
    scope,
    expectedRevision,
    customExpression,
    metadata,
    templates,
    detail,
    parseResult,
    rules,
    canCompose,
    diffFromVersion,
    diffToVersion,
    rollbackVersion,
    history,
    diffResult,
    setRole,
    setScope,
    setExpectedRevision,
    setCustomExpression,
    setDiffFromVersion,
    setDiffToVersion,
    setRollbackVersion,
    runAction,
    loadGovernanceBasics,
    loadRoleScopeAndHistory,
    validateAndParseExpression,
    saveRoleScope,
    addRule,
    composeExpressionByRules,
    updateRule,
    removeRule,
    queryDiff,
    rollbackScope,
  };
}
