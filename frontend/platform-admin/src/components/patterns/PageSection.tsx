import React from "react";
import { useTheme } from "../../design/theme/ThemeProvider";
import { createGlassPanelStyle } from "../../styles/glass";

export function PageSection({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  const { tokens } = useTheme();
  return (
    <section
      style={{
        ...createGlassPanelStyle(tokens, { saturate: 165 }),
        padding: tokens.space.lg,
      }}
    >
      <div style={{ fontWeight: 700, marginBottom: tokens.space.md }}>{title}</div>
      <div>{children}</div>
    </section>
  );
}

