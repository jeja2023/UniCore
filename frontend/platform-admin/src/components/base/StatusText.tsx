import React from "react";
import { useTheme } from "../../design/theme/ThemeProvider";

export function StatusText({
  tone,
  children,
}: {
  tone: "danger" | "success" | "muted";
  children: React.ReactNode;
}) {
  const { tokens } = useTheme();
  const color =
    tone === "danger"
      ? tokens.colors.danger
      : tone === "success"
      ? tokens.colors.brand
      : tokens.colors.textSecondary;

  return <div style={{ color, fontSize: 13 }}>{children}</div>;
}
