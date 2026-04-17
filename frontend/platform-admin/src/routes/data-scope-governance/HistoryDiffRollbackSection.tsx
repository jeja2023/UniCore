import React from "react";
import { Button } from "../../components/base/Button";
import { ListPageTemplate } from "../../components/patterns/ListPageTemplate";
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
  return (
    <ListPageTemplate
      title={GOVERNANCE_UI_TEXT.HISTORY.TITLE}
      toolbar={
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <Button onClick={onRefreshHistory} disabled={loading}>
            {GOVERNANCE_UI_TEXT.HISTORY.REFRESH_BUTTON}
          </Button>
          <label>
            {GOVERNANCE_UI_TEXT.HISTORY.FROM_LABEL}
            <input value={diffFromVersion} onChange={(e) => onDiffFromVersionChange(e.target.value)} style={{ width: 72, marginLeft: 6 }} />
          </label>
          <label>
            {GOVERNANCE_UI_TEXT.HISTORY.TO_LABEL}
            <input value={diffToVersion} onChange={(e) => onDiffToVersionChange(e.target.value)} style={{ width: 72, marginLeft: 6 }} />
          </label>
          <Button onClick={onQueryDiff} disabled={loading}>
            {GOVERNANCE_UI_TEXT.HISTORY.QUERY_DIFF_BUTTON}
          </Button>
          <label>
            {GOVERNANCE_UI_TEXT.HISTORY.ROLLBACK_VERSION_LABEL}
            <input value={rollbackVersion} onChange={(e) => onRollbackVersionChange(e.target.value)} style={{ width: 72, marginLeft: 6 }} />
          </label>
          <Button onClick={onRollback} variant="danger" disabled={loading}>
            {GOVERNANCE_UI_TEXT.HISTORY.ROLLBACK_BUTTON}
          </Button>
        </div>
      }
    >
      <HistoryDiffPanel history={history} diffResult={diffResult} />
    </ListPageTemplate>
  );
}
