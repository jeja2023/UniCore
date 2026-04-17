import React from "react";
import { Button } from "../../components/base/Button";
import type { DataScopeMetadata, DataScopeRule } from "./types";

type RulesTableProps = {
  rules: DataScopeRule[];
  metadata: DataScopeMetadata | null;
  loading: boolean;
  onUpdateRule: (index: number, patch: Partial<DataScopeRule>) => void;
  onRemoveRule: (index: number) => void;
};

export function RulesTable({ rules, metadata, loading, onUpdateRule, onRemoveRule }: RulesTableProps) {
  return (
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
                onChange={(e) => onUpdateRule(idx, { joinWithPrevious: e.target.value })}
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
              <select value={rule.field} onChange={(e) => onUpdateRule(idx, { field: e.target.value })}>
                {(metadata?.fields ?? [{ code: "tenant_id", name: "租户ID", type: "string" }]).map((f) => (
                  <option key={f.code} value={f.code}>
                    {f.code}
                  </option>
                ))}
              </select>
            </td>
            <td>
              <select value={rule.operator} onChange={(e) => onUpdateRule(idx, { operator: e.target.value })}>
                {(metadata?.operators ?? ["=", "!=", ">", ">=", "<", "<="]).map((x) => (
                  <option key={x} value={x}>
                    {x}
                  </option>
                ))}
              </select>
            </td>
            <td>
              <input value={rule.value} onChange={(e) => onUpdateRule(idx, { value: e.target.value })} />
            </td>
            <td>
              <input
                type="number"
                min={0}
                value={rule.openGroupCount}
                onChange={(e) => onUpdateRule(idx, { openGroupCount: Number(e.target.value) || 0 })}
                style={{ width: 60 }}
              />
            </td>
            <td>
              <input
                type="number"
                min={0}
                value={rule.closeGroupCount}
                onChange={(e) => onUpdateRule(idx, { closeGroupCount: Number(e.target.value) || 0 })}
                style={{ width: 60 }}
              />
            </td>
            <td>
              <Button onClick={() => onRemoveRule(idx)} disabled={rules.length <= 1 || loading} variant="danger">
                删除
              </Button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
