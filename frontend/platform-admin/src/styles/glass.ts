import type React from "react";
import type { AppTokens } from "../design/tokens/tokens";

export function createGlassControlVars(tokens: AppTokens): React.CSSProperties {
  return {
    "--glass-border": tokens.glass.border,
    "--glass-surface": tokens.glass.surface,
    "--glass-blur": tokens.glass.blur,
    "--glass-radius": `${tokens.radius.md}px`,
    "--glass-focus-border": tokens.glass.borderHighlight,
    "--glass-focus-ring": tokens.colors.brandSoft,
  } as React.CSSProperties;
}

export function createGlassCheckboxVars(tokens: AppTokens): React.CSSProperties {
  return {
    "--glass-border": tokens.glass.border,
    "--glass-surface": tokens.glass.surface,
    "--glass-blur": tokens.glass.blur,
    "--glass-focus-border": tokens.glass.borderHighlight,
    "--glass-focus-ring": tokens.colors.brandSoft,
    "--glass-check-fill": tokens.colors.brand,
    "--glass-check-bg": tokens.colors.brandSoft,
    color: tokens.colors.brand,
  } as React.CSSProperties;
}

export function createGlassPanelStyle(
  tokens: AppTokens,
  options?: {
    shadow?: string;
    saturate?: number;
    borderRadius?: number;
    background?: string;
  }
): React.CSSProperties {
  const saturate = options?.saturate ?? 170;
  return {
    borderRadius: options?.borderRadius ?? tokens.radius.lg,
    background: options?.background ?? tokens.glass.surfaceStrong,
    backdropFilter: `saturate(${saturate}%) blur(${tokens.glass.blur})`,
    WebkitBackdropFilter: `saturate(${saturate}%) blur(${tokens.glass.blur})`,
    border: `1px solid ${tokens.glass.border}`,
    boxShadow: options?.shadow ?? tokens.glass.shadow,
  };
}
