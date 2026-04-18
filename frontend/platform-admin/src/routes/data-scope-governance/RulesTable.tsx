import React from "react";
import { Button } from "../../components/base/Button";
import type { DataScopeMetadata, DataScopeRule } from "./types";
import { useTheme } from "../../design/theme/ThemeProvider";
import { createGlassControlVars } from "../../styles/glass";
import { JOINER_DISPLAY_LABELS, OPERATOR_DISPLAY_LABELS } from "./constants";
import { formatDataScopeFieldOptionLabel } from "./utils";

type RulesTableProps = {
  rules: DataScopeRule[];
  metadata: DataScopeMetadata | null;
  loading: boolean;
  onUpdateRule: (index: number, patch: Partial<DataScopeRule>) => void;
  onRemoveRule: (index: number) => void;
};

export function RulesTable({ rules, metadata, loading, onUpdateRule, onRemoveRule }: RulesTableProps) {
  const { tokens } = useTheme();
  const glassControlStyle = createGlassControlVars(tokens);
  const thStyle: React.CSSProperties = {
    padding: "8px 6px",
    border: `1px solid ${tokens.colors.border}`,
    color: tokens.colors.textSecondary,
    fontSize: 12,
  };
  const tdStyle: React.CSSProperties = {
    padding: "8px 6px",
    border: `1px solid ${tokens.colors.border}`,
    verticalAlign: "middle",
  };

  return (
    <div style={{ overflowX: "auto" }}>
      <table cellPadding={0} style={{ borderCollapse: "collapse", width: "100%", minWidth: 640 }}>
        <thead>
          <tr style={{ textAlign: "left" }}>
            <th style={thStyle}>#</th>
            <th style={thStyle}>连接</th>
            <th style={thStyle}>字段</th>
            <th style={thStyle}>操作符</th>
            <th style={thStyle}>值</th>
            <th style={thStyle}>左括</th>
            <th style={thStyle}>右括</th>
            <th style={thStyle}>操作</th>
          </tr>
        </thead>
        <tbody>
          {rules.map((rule, idx) => (
            <tr key={`rule-${idx}`}>
              <td style={tdStyle}>{idx + 1}</td>
              <td style={tdStyle}>
                <select
                  value={rule.joinWithPrevious ?? "AND"}
                  onChange={(e) => onUpdateRule(idx, { joinWithPrevious: e.target.value })}
                  disabled={idx === 0}
                  className="glass-control"
                  style={{ ...glassControlStyle, padding: "6px 8px" }}
                >
                  {(metadata?.joiners ?? ["AND", "OR"]).map((x) => (
                    <option key={x} value={x}>
                      {JOINER_DISPLAY_LABELS[x] ?? x}
                    </option>
                  ))}
                </select>
              </td>
              <td style={tdStyle}>
                <select
                  value={rule.field}
                  onChange={(e) => onUpdateRule(idx, { field: e.target.value })}
                  className="glass-control"
                  style={{ ...glassControlStyle, padding: "6px 8px" }}
                >
                  {(metadata?.fields ?? [{ code: "tenant_id", name: "租户ID", type: "string" }]).map((f) => (
                    <option key={f.code} value={f.code}>
                      {formatDataScopeFieldOptionLabel(f)}
                    </option>
                  ))}
                </select>
              </td>
              <td style={tdStyle}>
                <select
                  value={rule.operator}
                  onChange={(e) => onUpdateRule(idx, { operator: e.target.value })}
                  className="glass-control"
                  style={{ ...glassControlStyle, padding: "6px 8px" }}
                >
                  {(metadata?.operators ?? ["=", "!=", ">", ">=", "<", "<="]).map((x) => (
                    <option key={x} value={x}>
                      {OPERATOR_DISPLAY_LABELS[x] ?? x}
                    </option>
                  ))}
                </select>
              </td>
              <td style={tdStyle}>
                <input
                  value={rule.value}
                  onChange={(e) => onUpdateRule(idx, { value: e.target.value })}
                  className="glass-control"
                  style={{ ...glassControlStyle, padding: "6px 8px" }}
                />
              </td>
              <td style={tdStyle}>
                <input
                  type="number"
                  min={0}
                  value={rule.openGroupCount}
                  onChange={(e) => onUpdateRule(idx, { openGroupCount: Number(e.target.value) || 0 })}
                  className="glass-control"
                  style={{ ...glassControlStyle, width: 60, padding: "6px 8px" }}
                />
              </td>
              <td style={tdStyle}>
                <input
                  type="number"
                  min={0}
                  value={rule.closeGroupCount}
                  onChange={(e) => onUpdateRule(idx, { closeGroupCount: Number(e.target.value) || 0 })}
                  className="glass-control"
                  style={{ ...glassControlStyle, width: 60, padding: "6px 8px" }}
                />
              </td>
              <td style={tdStyle}>
                <Button onClick={() => onRemoveRule(idx)} disabled={rules.length <= 1 || loading} variant="danger">
                  删除
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
