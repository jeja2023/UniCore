import React from "react";
import type { GovernanceStatusBarProps } from "./types";
import { PageAsyncState } from "../../components/patterns/PageAsyncState";
import { GOVERNANCE_UI_TEXT } from "./constants";

export function GovernanceStatusBar({ loading, status, error }: GovernanceStatusBarProps) {
  return <PageAsyncState loading={loading} error={error} success={status} loadingText={GOVERNANCE_UI_TEXT.PROCESSING} />;
}
