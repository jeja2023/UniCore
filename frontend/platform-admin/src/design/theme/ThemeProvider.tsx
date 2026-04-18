import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import { tokens, type AppTokens } from "../tokens/tokens";

type ThemeMode = "light" | "dark";

type ThemeState = {
  mode: ThemeMode;
  setMode: (mode: ThemeMode) => void;
  tokens: AppTokens;
};

const ThemeContext = createContext<ThemeState | null>(null);

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [mode, setMode] = useState<ThemeMode>("light");
  const themedTokens = useMemo(() => {
    if (mode === "light") return tokens;
    return {
      ...tokens,
      colors: {
        ...tokens.colors,
        brand: "#6B9FFF",
        brandSoft: "rgba(107, 159, 255, 0.16)",
        bg: "#0B1220",
        bgSubtle: "#111A2E",
        text: "#F2F4F7",
        textSecondary: "#98A2B3",
        border: "rgba(148, 163, 184, 0.35)",
      },
      glass: {
        surface: "rgba(22, 32, 52, 0.64)",
        surfaceStrong: "rgba(28, 40, 64, 0.88)",
        border: "rgba(255, 255, 255, 0.18)",
        borderHighlight: "rgba(129, 161, 255, 0.44)",
        shadow: "0 12px 30px rgba(0, 0, 0, 0.48), inset 0 1px 0 rgba(255, 255, 255, 0.07)",
        shadowHover: "0 18px 42px rgba(0, 0, 0, 0.6)",
        blur: "22px",
      },
    };
  }, [mode]);

  useEffect(() => {
    document.documentElement.dataset.theme = mode;
  }, [mode]);

  const value = useMemo<ThemeState>(() => ({ mode, setMode, tokens: themedTokens }), [mode, themedTokens]);
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}

