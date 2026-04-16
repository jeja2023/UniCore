import React from "react";
import { useTheme } from "../../design/theme/ThemeProvider";

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
        border: `1px solid ${tokens.colors.border}`,
        borderRadius: tokens.radius.md,
        padding: tokens.space.lg,
        background: tokens.colors.bg,
      }}
    >
      <div style={{ fontWeight: 700, marginBottom: tokens.space.md }}>{title}</div>
      <div>{children}</div>
    </section>
  );
}

