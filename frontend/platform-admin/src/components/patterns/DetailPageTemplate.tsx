import React from "react";
import { PageSection } from "./PageSection";
import { useTheme } from "../../design/theme/ThemeProvider";

export function DetailPageTemplate({
  title,
  rows,
}: {
  title: string;
  rows: Array<{ label: string; value: React.ReactNode }>;
}) {
  const { tokens } = useTheme();
  return (
    <PageSection title={title}>
      <div style={{ display: "grid", rowGap: tokens.space.sm }}>
        {rows.map((r) => (
          <div
            key={r.label}
            style={{
              display: "grid",
              gridTemplateColumns: "140px 1fr",
              alignItems: "center",
              paddingBottom: tokens.space.xs,
              borderBottom: `1px dashed ${tokens.colors.border}`,
            }}
          >
            <div style={{ color: tokens.colors.textSecondary }}>{r.label}</div>
            <div style={{ fontWeight: 500 }}>{r.value}</div>
          </div>
        ))}
      </div>
    </PageSection>
  );
}

