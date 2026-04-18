import React from "react";
import { useTheme } from "../../design/theme/ThemeProvider";

export function StatusText({
  tone,
  children,
  icon,
}: {
  tone: "danger" | "success" | "muted";
  children: React.ReactNode;
  icon?: React.ReactNode;
}) {
  const { tokens } = useTheme();
  const color =
    tone === "danger"
      ? tokens.colors.danger
      : tone === "success"
        ? tokens.colors.brand
        : tokens.colors.textSecondary;

  return (
    <div
      style={{
        color,
        fontSize: 13,
        display: "flex",
        alignItems: "center",
        gap: 8,
        lineHeight: 1.35,
      }}
    >
      {icon ? <span style={{ display: "flex", flexShrink: 0, color }}>{icon}</span> : null}
      <span>{children}</span>
    </div>
  );
}
