import React from "react";
import { useTheme } from "../../design/theme/ThemeProvider";

export type ButtonVariant = "primary" | "default" | "danger";

export function Button({
  children,
  onClick,
  disabled,
  variant = "default",
  type = "button",
}: {
  children: React.ReactNode;
  onClick?: () => void;
  disabled?: boolean;
  variant?: ButtonVariant;
  type?: "button" | "submit";
}) {
  const { tokens } = useTheme();
  const bg =
    variant === "primary"
      ? tokens.colors.brand
      : variant === "danger"
        ? tokens.colors.danger
        : tokens.colors.bg;
  const color =
    variant === "default" ? tokens.colors.text : "#fff";
  const border =
    variant === "default" ? `1px solid ${tokens.colors.border}` : "1px solid transparent";

  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled}
      style={{
        padding: "8px 12px",
        borderRadius: tokens.radius.sm,
        border,
        background: disabled ? tokens.colors.bgSubtle : bg,
        color: disabled ? tokens.colors.textSecondary : color,
        cursor: disabled ? "not-allowed" : "pointer",
      }}
    >
      {children}
    </button>
  );
}

