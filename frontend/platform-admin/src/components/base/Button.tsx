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
  const isPrimary = variant === "primary";
  const isDanger = variant === "danger";
  const bg =
    isPrimary
      ? `linear-gradient(135deg, ${tokens.colors.brand} 0%, #3b6cff 55%, #2563eb 100%)`
      : isDanger
        ? tokens.colors.danger
        : tokens.glass.surfaceStrong;
  const color = variant === "default" ? tokens.colors.text : "#fff";
  const border = isPrimary || isDanger ? "1px solid transparent" : `1px solid ${tokens.glass.border}`;
  const shadow =
    isPrimary && !disabled
      ? "0 6px 20px rgba(21, 94, 239, 0.35), inset 0 1px 0 rgba(255,255,255,0.25)"
      : variant === "default" && !disabled
        ? tokens.glass.shadow
        : "none";

  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled}
      style={{
        padding: "9px 14px",
        borderRadius: tokens.radius.md,
        border,
        background: disabled ? tokens.colors.bgSubtle : bg,
        color: disabled ? tokens.colors.textSecondary : color,
        cursor: disabled ? "not-allowed" : "pointer",
        boxShadow: disabled ? "none" : shadow,
        backdropFilter: variant === "default" && !disabled ? `blur(${tokens.glass.blur})` : undefined,
        WebkitBackdropFilter: variant === "default" && !disabled ? `blur(${tokens.glass.blur})` : undefined,
      }}
    >
      {children}
    </button>
  );
}

