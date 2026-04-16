import React, { createContext, useContext, useMemo, useState } from "react";
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
        bg: "#101828",
        bgSubtle: "#1D2939",
        text: "#F2F4F7",
        textSecondary: "#98A2B3",
        border: "#344054",
      },
    };
  }, [mode]);
  const value = useMemo<ThemeState>(() => ({ mode, setMode, tokens: themedTokens }), [mode, themedTokens]);
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}

