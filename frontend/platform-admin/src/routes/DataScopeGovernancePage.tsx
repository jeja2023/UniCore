import React, { useEffect, useMemo, useState } from "react";
import { apiFetch } from "../security/apiClient";
import { ListPageTemplate } from "../components/patterns/ListPageTemplate";
import { Button } from "../components/base/Button";

type ApiEnvelope<T> = { data: T };

type DataScopeMetadata = {
  fields: Array<{ code: string; name: string; type: string }>;
  operators: string[];
  joiners: string[];
};

type DataScopeTemplate = {
  code: string;
  name: string;
  scope: string;
  expression: string;
};

type DataScopeRule = {
  field: string;
  operator: string;
  value: string;
  joinWithPrevious?: string;
  openGroupCount: number;
  closeGroupCount: number;
};

type ParsedToken = { value: string; kind: string };
type ParseResult = { isValid: boolean; errorMessage?: string | null; tokens: ParsedToken[] };

type ScopeDetail = {
  roleCode: string;
  scope: string;
  customExpression?: string | null;
  revision: number;
  updatedAt?: string | null;
};

type ScopeHistoryItem = {
  historyId: string;
  version: number;
  scope: string;
  customExpression?: string | null;
  changedBy: string;
  changedAt: string;
};

type ScopeDiff = {
  roleCode: string;
  fromVersion: number;
  toVersion: number;
  fromScope: string;
  toScope: string;
  fromExpression?: string | null;
  toExpression?: string | null;
  addedTokens: string[];
  removedTokens: string[];
  isSemanticallySame: boolean;
  summary: string;
};

function getErrorMessage(err: unknown): string {
  if (typeof err === "object" && err !== null && "message" in err) {
    const msg = (err as { message?: unknown }).message;
    if (typeof msg === "string" && msg.length > 0) return msg;
  }
  return "请求失败";
}

function nowIsoText(input?: string | null) {
  if (!input) return "-";
  const date = new Date(input);
  if (Number.isNaN(date.getTime())) return input;
  return date.toLocaleString();
}

export function DataScopeGovernancePage() {
  const [role, setRole] = useState("admin");
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
  const [rules, setRules] = useState<DataScopeRule[]>([
    {
      field: "tenant_id",
      operator: "=",
      value: "{current_tenant_id}",
      joinWithPrevious: "AND",
      openGroupCount: 0,
      closeGroupCount: 0,
    },
  ]);

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

  useEffect(() => {
    void runAction(async () => {
      await loadGovernanceBasics();
      await loadRoleScopeAndHistory();
    }, "已自动加载治理元数据与角色配置。");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function loadGovernanceBasics() {
    const [metadataResp, templatesResp] = await Promise.all([
      apiFetch<ApiEnvelope<DataScopeMetadata>>("/api/permission/data-scope/metadata"),
      apiFetch<ApiEnvelope<DataScopeTemplate[]>>("/api/permission/data-scope/templates"),
    ]);
    setMetadata(metadataResp.data);
    setTemplates(templatesResp.data ?? []);
  }

  async function loadRoleScopeAndHistory() {
    const normalized = role.trim();
    if (!normalized) {
      throw new Error("请先输入角色编码。");
    }
    const [detailResp, historyResp] = await Promise.all([
      apiFetch<ApiEnvelope<ScopeDetail>>(`/api/permission/roles/${encodeURIComponent(normalized)}/data-scope`),
      apiFetch<ApiEnvelope<ScopeHistoryItem[]>>(
        `/api/permission/roles/${encodeURIComponent(normalized)}/data-scope/history?take=30`
      ),
    ]);
    setDetail(detailResp.data);
    setHistory(historyResp.data ?? []);
    setScope(detailResp.data.scope || "Self");
    setCustomExpression(detailResp.data.customExpression ?? "");
    setExpectedRevision(detailResp.data.revision.toString());
  }

  async function validateAndParseExpression() {
    const body = JSON.stringify({ customExpression });
    await apiFetch<ApiEnvelope<{ isValid: boolean }>>("/api/permission/data-scope/validate", {
      method: "POST",
      body,
    });
    const parsed = await apiFetch<ApiEnvelope<ParseResult>>("/api/permission/data-scope/parse", {
      method: "POST",
      body,
    });
    setParseResult(parsed.data);
  }

  async function composeExpressionByRules() {
    const resp = await apiFetch<ApiEnvelope<{ expression: string }>>("/api/permission/data-scope/compose", {
      method: "POST",
      body: JSON.stringify({ rules }),
    });
    setCustomExpression(resp.data.expression);
  }

  async function saveRoleScope() {
    const normalized = role.trim();
    if (!normalized) {
      throw new Error("请先输入角色编码。");
    }
    const payload = {
      scope,
      customExpression: scope === "Custom" ? customExpression : null,
      expectedRevision: expectedRevision.trim() ? Number(expectedRevision) : null,
    };
    await apiFetch(`/api/permission/roles/${encodeURIComponent(normalized)}/data-scope`, {
      method: "POST",
      body: JSON.stringify(payload),
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
    const resp = await apiFetch<ApiEnvelope<ScopeDiff>>(
      `/api/permission/roles/${encodeURIComponent(normalized)}/data-scope/diff?fromVersion=${fromVersion}&toVersion=${toVersion}`
    );
    setDiffResult(resp.data);
  }

  async function rollbackScope() {
    const normalized = role.trim();
    const targetVersion = Number(rollbackVersion);
    if (!normalized || !Number.isInteger(targetVersion)) {
      throw new Error("请填写角色编码与合法回滚版本。");
    }
    await apiFetch(`/api/permission/roles/${encodeURIComponent(normalized)}/data-scope/rollback`, {
      method: "POST",
      body: JSON.stringify({ targetVersion }),
    });
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

  return (
    <div style={{ display: "grid", gap: 16 }}>
      <h3>数据权限表达式治理</h3>
      {loading ? <div>处理中…</div> : null}
      {status ? <div style={{ color: "#067647" }}>{status}</div> : null}
      {error ? <div style={{ color: "#b42318" }}>{error}</div> : null}

      <ListPageTemplate
        title="1) 基础元数据与模板"
        toolbar={
          <div style={{ display: "flex", gap: 8 }}>
            <Button onClick={() => void runAction(loadGovernanceBasics, "已加载元数据与模板。")} variant="primary" disabled={loading}>
              加载治理元数据
            </Button>
          </div>
        }
      >
        <div style={{ display: "grid", gap: 8 }}>
          <div>字段：{metadata?.fields.map((f) => `${f.code}(${f.type})`).join(", ") || "-"}</div>
          <div>操作符：{metadata?.operators.join(", ") || "-"}</div>
          <div>连接符：{metadata?.joiners.join(", ") || "-"}</div>
          <div>
            模板：
            <ul>
              {templates.map((t) => (
                <li key={t.code}>
                  <b>{t.name}</b>：{t.expression}{" "}
                  <Button onClick={() => setCustomExpression(t.expression)} disabled={loading}>套用</Button>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </ListPageTemplate>

      <ListPageTemplate
        title="2) 角色范围配置"
        toolbar={
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <label>
              角色编码：
              <input value={role} onChange={(e) => setRole(e.target.value)} style={{ marginLeft: 6 }} />
            </label>
            <label>
              范围：
              <select value={scope} onChange={(e) => setScope(e.target.value)} style={{ marginLeft: 6 }}>
                <option value="Self">Self</option>
                <option value="Department">Department</option>
                <option value="Tenant">Tenant</option>
                <option value="Custom">Custom</option>
              </select>
            </label>
            <label>
              期望版本：
              <input value={expectedRevision} onChange={(e) => setExpectedRevision(e.target.value)} style={{ marginLeft: 6, width: 90 }} />
            </label>
            <Button onClick={() => void runAction(loadRoleScopeAndHistory, "已读取角色数据权限。")} disabled={loading}>读取角色</Button>
            <Button onClick={() => void runAction(saveRoleScope, "角色数据权限已保存。")} variant="primary" disabled={loading}>
              保存角色范围
            </Button>
          </div>
        }
      >
        <div style={{ display: "grid", gap: 8 }}>
          <label>
            customExpression：
            <textarea
              value={customExpression}
              onChange={(e) => setCustomExpression(e.target.value)}
              rows={4}
              style={{ display: "block", width: "100%", marginTop: 6 }}
            />
          </label>
          <div style={{ display: "flex", gap: 8 }}>
            <Button onClick={() => void runAction(validateAndParseExpression, "表达式校验通过并完成解析。")} disabled={loading}>
              校验并解析表达式
            </Button>
          </div>
          <div>
            当前详情：
            {detail ? (
              <div>
                role={detail.roleCode}, scope={detail.scope}, revision={detail.revision}, updatedAt={nowIsoText(detail.updatedAt)}
              </div>
            ) : (
              "-"
            )}
          </div>
          <div>
            解析结果：
            {parseResult ? (
              <div>
                <div>isValid: {String(parseResult.isValid)}</div>
                {parseResult.errorMessage ? <div>error: {parseResult.errorMessage}</div> : null}
                <div>
                  tokens:
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

      <ListPageTemplate
        title="3) 可视化规则拼装"
        toolbar={
          <div style={{ display: "flex", gap: 8 }}>
            <Button onClick={addRule} disabled={loading}>新增规则</Button>
            <Button
              onClick={() => void runAction(composeExpressionByRules, "已按规则拼装表达式。")}
              variant="primary"
              disabled={!canCompose || loading}
            >
              规则拼装为表达式
            </Button>
          </div>
        }
      >
        <table cellPadding={8} style={{ borderCollapse: "collapse", width: "100%" }}>
          <thead>
            <tr style={{ textAlign: "left", borderBottom: "1px solid #ddd" }}>
              <th>#</th>
              <th>Join</th>
              <th>Field</th>
              <th>Operator</th>
              <th>Value</th>
              <th>(</th>
              <th>)</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            {rules.map((rule, idx) => (
              <tr key={`rule-${idx}`} style={{ borderBottom: "1px solid #f2f4f7" }}>
                <td>{idx + 1}</td>
                <td>
                  <select
                    value={rule.joinWithPrevious ?? "AND"}
                    onChange={(e) => updateRule(idx, { joinWithPrevious: e.target.value })}
                    disabled={idx === 0}
                  >
                    {(metadata?.joiners ?? ["AND", "OR"]).map((x) => (
                      <option key={x} value={x}>
                        {x}
                      </option>
                    ))}
                  </select>
                </td>
                <td>
                  <select value={rule.field} onChange={(e) => updateRule(idx, { field: e.target.value })}>
                    {(metadata?.fields ?? [{ code: "tenant_id", name: "租户ID", type: "string" }]).map((f) => (
                      <option key={f.code} value={f.code}>
                        {f.code}
                      </option>
                    ))}
                  </select>
                </td>
                <td>
                  <select value={rule.operator} onChange={(e) => updateRule(idx, { operator: e.target.value })}>
                    {(metadata?.operators ?? ["=", "!=", ">", ">=", "<", "<="]).map((x) => (
                      <option key={x} value={x}>
                        {x}
                      </option>
                    ))}
                  </select>
                </td>
                <td>
                  <input value={rule.value} onChange={(e) => updateRule(idx, { value: e.target.value })} />
                </td>
                <td>
                  <input
                    type="number"
                    min={0}
                    value={rule.openGroupCount}
                    onChange={(e) => updateRule(idx, { openGroupCount: Number(e.target.value) || 0 })}
                    style={{ width: 60 }}
                  />
                </td>
                <td>
                  <input
                    type="number"
                    min={0}
                    value={rule.closeGroupCount}
                    onChange={(e) => updateRule(idx, { closeGroupCount: Number(e.target.value) || 0 })}
                    style={{ width: 60 }}
                  />
                </td>
                <td>
                  <Button onClick={() => removeRule(idx)} disabled={rules.length <= 1 || loading} variant="danger">
                    删除
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </ListPageTemplate>

      <ListPageTemplate
        title="4) 版本历史、差异与回滚"
        toolbar={
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <Button onClick={() => void runAction(loadRoleScopeAndHistory, "已刷新历史版本。")} disabled={loading}>刷新历史</Button>
            <label>
              from:
              <input value={diffFromVersion} onChange={(e) => setDiffFromVersion(e.target.value)} style={{ width: 72, marginLeft: 6 }} />
            </label>
            <label>
              to:
              <input value={diffToVersion} onChange={(e) => setDiffToVersion(e.target.value)} style={{ width: 72, marginLeft: 6 }} />
            </label>
            <Button onClick={() => void runAction(queryDiff, "已完成版本差异比对。")} disabled={loading}>查询差异</Button>
            <label>
              回滚版本:
              <input value={rollbackVersion} onChange={(e) => setRollbackVersion(e.target.value)} style={{ width: 72, marginLeft: 6 }} />
            </label>
            <Button onClick={() => void runAction(rollbackScope, "已回滚角色数据权限。")} variant="danger" disabled={loading}>
              回滚
            </Button>
          </div>
        }
      >
        <div style={{ display: "grid", gap: 8 }}>
          <div>历史版本：</div>
          <table cellPadding={8} style={{ borderCollapse: "collapse", width: "100%" }}>
            <thead>
              <tr style={{ textAlign: "left", borderBottom: "1px solid #ddd" }}>
                <th>Version</th>
                <th>Scope</th>
                <th>Expression</th>
                <th>ChangedBy</th>
                <th>ChangedAt</th>
              </tr>
            </thead>
            <tbody>
              {history.map((h) => (
                <tr key={h.historyId} style={{ borderBottom: "1px solid #f2f4f7" }}>
                  <td>{h.version}</td>
                  <td>{h.scope}</td>
                  <td>{h.customExpression ?? "-"}</td>
                  <td>{h.changedBy}</td>
                  <td>{nowIsoText(h.changedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <div>
            版本差异：
            {diffResult ? (
              <div style={{ marginTop: 6 }}>
                <div>{diffResult.summary}</div>
                <div>fromScope={diffResult.fromScope}, toScope={diffResult.toScope}</div>
                <div>addedTokens={diffResult.addedTokens.join(", ") || "-"}</div>
                <div>removedTokens={diffResult.removedTokens.join(", ") || "-"}</div>
                <div>isSemanticallySame={String(diffResult.isSemanticallySame)}</div>
              </div>
            ) : (
              "-"
            )}
          </div>
        </div>
      </ListPageTemplate>
    </div>
  );
}

