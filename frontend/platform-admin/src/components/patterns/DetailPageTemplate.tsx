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
              gridTemplateColumns: "minmax(160px, 36%) 1fr",
              alignItems: "center",
              paddingBottom: tokens.space.xs,
              borderBottom: `1px dashed ${tokens.colors.border}`,
            }}
          >
            <div style={{ color: tokens.colors.textSecondary, fontSize: 12 }}>{r.label}</div>
            <div className="u-display-field u-display-field--compact" style={{ fontWeight: 500 }}>
              {r.value}
            </div>
          </div>
        ))}
      </div>
    </PageSection>
  );
}

