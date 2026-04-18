import React from "react";
import { PageSection } from "../../components/patterns/PageSection";
import { Button } from "../../components/base/Button";
import { PageAsyncState } from "../../components/patterns/PageAsyncState";
import { useTheme } from "../../design/theme/ThemeProvider";

type UsersHeaderSectionProps = {
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
};

export function UsersHeaderSection({ loading, error, onRefresh }: UsersHeaderSectionProps) {
  const { tokens } = useTheme();

  return (
    <>
      <PageSection title="用户">
        <div style={{ display: "flex", justifyContent: "space-between", gap: tokens.space.md, alignItems: "flex-start" }}>
          <div>
            <h3 style={{ margin: 0 }}>用户</h3>
            <div style={{ marginTop: tokens.space.xs, color: tokens.colors.textSecondary }}>查看租户下用户基础信息和启用状态</div>
          </div>
          <Button onClick={onRefresh} disabled={loading} variant="primary">
            {loading ? "刷新中…" : "刷新"}
          </Button>
        </div>
      </PageSection>
      <PageAsyncState loading={loading} error={error} loadingText="正在加载用户列表…" />
    </>
  );
}
