import React from "react";
import type { ScopeDiff, ScopeHistoryItem } from "./types";
import { nowIsoText } from "./utils";

type HistoryDiffPanelProps = {
  history: ScopeHistoryItem[];
  diffResult: ScopeDiff | null;
};

export function HistoryDiffPanel({ history, diffResult }: HistoryDiffPanelProps) {
  return (
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
  );
}
