import React, { createContext, useContext, useMemo } from "react";

const messages: Record<string, string> = {
  appTitle: "管理台",
  welcome: "欢迎",
  welcomeDesc:
    "这是 UniCore 前端基座，已支持登录、布局、动态菜单、权限守卫、主题切换与中文界面。",
  logout: "退出登录",
  theme: "主题",
};

type I18nState = {
  t: (key: string) => string;
};

const I18nContext = createContext<I18nState | null>(null);

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const value = useMemo<I18nState>(
    () => ({
      t: (key: string) => messages[key] ?? key,
    }),
    [],
  );
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error("useI18n must be used within I18nProvider");
  return ctx;
}
