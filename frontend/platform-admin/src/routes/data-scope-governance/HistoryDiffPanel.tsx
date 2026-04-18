import React from "react";
import type { ScopeDiff, ScopeHistoryItem } from "./types";
import { SCOPE_OPTION_LABELS } from "./constants";
import { nowIsoText } from "./utils";
import { useTheme } from "../../design/theme/ThemeProvider";

type HistoryDiffPanelProps = {
  history: ScopeHistoryItem[];
  diffResult: ScopeDiff | null;
};

function scopeCellText(scope: string) {
  return SCOPE_OPTION_LABELS[scope as keyof typeof SCOPE_OPTION_LABELS] ?? scope;
}

export function HistoryDiffPanel({ history, diffResult }: HistoryDiffPanelProps) {
  const { tokens } = useTheme();
  const thStyle: React.CSSProperties = {
    padding: "8px 6px",
    border: `1px solid ${tokens.colors.border}`,
    color: tokens.colors.textSecondary,
    fontSize: 12,
  };
  const tdStyle: React.CSSProperties = {
    padding: "8px 6px",
    border: `1px solid ${tokens.colors.border}`,
    fontSize: 13,
  };

  return (
    <div style={{ display: "grid", gap: 8 }}>
      <div style={{ fontWeight: 600 }}>历史版本</div>
      <div
        style={{
          overflowX: "auto",
          border: `1px solid ${tokens.colors.border}`,
          borderRadius: tokens.radius.md,
          background: tokens.colors.bgSubtle,
        }}
      >
        <table cellPadding={0} style={{ borderCollapse: "collapse", width: "100%", minWidth: 560 }}>
          <thead>
            <tr style={{ textAlign: "left" }}>
              <th style={thStyle}>版本</th>
              <th style={thStyle}>范围</th>
              <th style={thStyle}>表达式</th>
              <th style={thStyle}>修改人</th>
              <th style={thStyle}>修改时间</th>
            </tr>
          </thead>
          <tbody>
            {history.map((h) => (
              <tr key={h.historyId}>
                <td style={tdStyle}>{h.version}</td>
                <td style={tdStyle}>
                  {scopeCellText(h.scope)}（{h.scope}）
                </td>
                <td style={tdStyle}>{h.customExpression ?? "—"}</td>
                <td style={tdStyle}>{h.changedBy}</td>
                <td style={tdStyle}>{nowIsoText(h.changedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div
        style={{
          padding: "8px 10px",
        }}
        className="u-display-field"
      >
        <div className="u-display-field__title" style={{ fontWeight: 600 }}>
          版本差异
        </div>
        {diffResult ? (
          <div style={{ marginTop: 4, fontSize: 13, lineHeight: 1.5 }}>
            <div>{diffResult.summary}</div>
            <div>
              起始范围：{scopeCellText(diffResult.fromScope)}（{diffResult.fromScope}）；目标范围：
              {scopeCellText(diffResult.toScope)}（{diffResult.toScope}）
            </div>
            <div>新增词法单元：{diffResult.addedTokens.join("，") || "—"}</div>
            <div>移除词法单元：{diffResult.removedTokens.join("，") || "—"}</div>
            <div>语义一致：{String(diffResult.isSemanticallySame)}</div>
          </div>
        ) : (
          "—"
        )}
      </div>
    </div>
  );
}
