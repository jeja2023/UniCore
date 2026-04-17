import { apiFetch } from "../../security/apiClient";
import type {
  ApiEnvelope,
  DataScopeMetadata,
  DataScopeRule,
  DataScopeTemplate,
  ParseResult,
  ScopeDetail,
  ScopeDiff,
  ScopeHistoryItem,
} from "./types";

export async function fetchGovernanceMetadataAndTemplates() {
  const [metadataResp, templatesResp] = await Promise.all([
    apiFetch<ApiEnvelope<DataScopeMetadata>>("/api/permission/data-scope/metadata"),
    apiFetch<ApiEnvelope<DataScopeTemplate[]>>("/api/permission/data-scope/templates"),
  ]);
  return {
    metadata: metadataResp.data,
    templates: templatesResp.data ?? [],
  };
}

export async function fetchRoleScopeAndHistory(roleCode: string) {
  const encodedRole = encodeURIComponent(roleCode);
  const [detailResp, historyResp] = await Promise.all([
    apiFetch<ApiEnvelope<ScopeDetail>>(`/api/permission/roles/${encodedRole}/data-scope`),
    apiFetch<ApiEnvelope<ScopeHistoryItem[]>>(`/api/permission/roles/${encodedRole}/data-scope/history?take=30`),
  ]);
  return {
    detail: detailResp.data,
    history: historyResp.data ?? [],
  };
}

export async function validateAndParseDataScopeExpression(customExpression: string) {
  const body = JSON.stringify({ customExpression });
  await apiFetch<ApiEnvelope<{ isValid: boolean }>>("/api/permission/data-scope/validate", {
    method: "POST",
    body,
  });
  const parsed = await apiFetch<ApiEnvelope<ParseResult>>("/api/permission/data-scope/parse", {
    method: "POST",
    body,
  });
  return parsed.data;
}

export async function composeDataScopeExpressionByRules(rules: DataScopeRule[]) {
  const resp = await apiFetch<ApiEnvelope<{ expression: string }>>("/api/permission/data-scope/compose", {
    method: "POST",
    body: JSON.stringify({ rules }),
  });
  return resp.data.expression;
}

export async function saveRoleDataScope(input: {
  roleCode: string;
  scope: string;
  customExpression: string;
  expectedRevision: string;
}) {
  const payload = {
    scope: input.scope,
    customExpression: input.scope === "Custom" ? input.customExpression : null,
    expectedRevision: input.expectedRevision.trim() ? Number(input.expectedRevision) : null,
  };
  await apiFetch(`/api/permission/roles/${encodeURIComponent(input.roleCode)}/data-scope`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export async function fetchRoleDataScopeDiff(input: { roleCode: string; fromVersion: number; toVersion: number }) {
  const resp = await apiFetch<ApiEnvelope<ScopeDiff>>(
    `/api/permission/roles/${encodeURIComponent(input.roleCode)}/data-scope/diff?fromVersion=${input.fromVersion}&toVersion=${input.toVersion}`
  );
  return resp.data;
}

export async function rollbackRoleDataScope(input: { roleCode: string; targetVersion: number }) {
  await apiFetch(`/api/permission/roles/${encodeURIComponent(input.roleCode)}/data-scope/rollback`, {
    method: "POST",
    body: JSON.stringify({ targetVersion: input.targetVersion }),
  });
}
