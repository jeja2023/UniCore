import React from "react";
import { Button } from "../../components/base/Button";
import { ListPageTemplate } from "../../components/patterns/ListPageTemplate";
import { useTheme } from "../../design/theme/ThemeProvider";
import { createGlassControlVars } from "../../styles/glass";
import { GOVERNANCE_UI_TEXT } from "./constants";
import { HistoryDiffPanel } from "./HistoryDiffPanel";
import type { ScopeDiff, ScopeHistoryItem } from "./types";

type HistoryDiffRollbackSectionProps = {
  loading: boolean;
  diffFromVersion: string;
  diffToVersion: string;
  rollbackVersion: string;
  history: ScopeHistoryItem[];
  diffResult: ScopeDiff | null;
  onDiffFromVersionChange: (value: string) => void;
  onDiffToVersionChange: (value: string) => void;
  onRollbackVersionChange: (value: string) => void;
  onRefreshHistory: () => void;
  onQueryDiff: () => void;
  onRollback: () => void;
};

export function HistoryDiffRollbackSection({
  loading,
  diffFromVersion,
  diffToVersion,
  rollbackVersion,
  history,
  diffResult,
  onDiffFromVersionChange,
  onDiffToVersionChange,
  onRollbackVersionChange,
  onRefreshHistory,
  onQueryDiff,
  onRollback,
}: HistoryDiffRollbackSectionProps) {
  const { tokens } = useTheme();
  const glassControlStyle = createGlassControlVars(tokens);

  return (
    <ListPageTemplate
      title={GOVERNANCE_UI_TEXT.HISTORY.TITLE}
      toolbar={
        <div style={{ display: "grid", gap: 8, gridTemplateColumns: "repeat(auto-fit, minmax(150px, 1fr))", alignItems: "end" }}>
          <div>
            <Button onClick={onRefreshHistory} disabled={loading}>
              {GOVERNANCE_UI_TEXT.HISTORY.REFRESH_BUTTON}
            </Button>
          </div>
          <label style={{ fontSize: 12 }}>
            {GOVERNANCE_UI_TEXT.HISTORY.FROM_LABEL}
            <input
              value={diffFromVersion}
              onChange={(e) => onDiffFromVersionChange(e.target.value)}
              className="glass-control"
              style={{ ...glassControlStyle, marginTop: 4, width: "100%", padding: "7px 10px" }}
            />
          </label>
          <label style={{ fontSize: 12 }}>
            {GOVERNANCE_UI_TEXT.HISTORY.TO_LABEL}
            <input
              value={diffToVersion}
              onChange={(e) => onDiffToVersionChange(e.target.value)}
              className="glass-control"
              style={{ ...glassControlStyle, marginTop: 4, width: "100%", padding: "7px 10px" }}
            />
          </label>
          <div>
            <Button onClick={onQueryDiff} disabled={loading}>
              {GOVERNANCE_UI_TEXT.HISTORY.QUERY_DIFF_BUTTON}
            </Button>
          </div>
          <label style={{ fontSize: 12 }}>
            {GOVERNANCE_UI_TEXT.HISTORY.ROLLBACK_VERSION_LABEL}
            <input
              value={rollbackVersion}
              onChange={(e) => onRollbackVersionChange(e.target.value)}
              className="glass-control"
              style={{ ...glassControlStyle, marginTop: 4, width: "100%", padding: "7px 10px" }}
            />
          </label>
          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button onClick={onRollback} variant="danger" disabled={loading}>
              {GOVERNANCE_UI_TEXT.HISTORY.ROLLBACK_BUTTON}
            </Button>
          </div>
        </div>
      }
    >
      <HistoryDiffPanel history={history} diffResult={diffResult} />
    </ListPageTemplate>
  );
}
