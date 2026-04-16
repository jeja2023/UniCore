import React, { createContext, useContext, useMemo, useState } from "react";

type Locale = "zh-CN" | "en-US";

const messages: Record<Locale, Record<string, string>> = {
  "zh-CN": {
    appTitle: "管理台",
    welcome: "欢迎",
    welcomeDesc: "这是 UniCore 前端基座，已支持登录、布局、动态菜单、权限守卫、主题与国际化。",
    logout: "退出",
    theme: "主题",
    language: "语言",
  },
  "en-US": {
    appTitle: "Admin Console",
    welcome: "Welcome",
    welcomeDesc: "This is UniCore frontend base with auth, layout, dynamic menu, permission guards, theme, and i18n.",
    logout: "Logout",
    theme: "Theme",
    language: "Language",
  },
};

type I18nState = {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: (key: string) => string;
};

const I18nContext = createContext<I18nState | null>(null);

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [locale, setLocale] = useState<Locale>("zh-CN");
  const value = useMemo<I18nState>(() => {
    return {
      locale,
      setLocale,
      t: (key: string) => messages[locale][key] ?? key,
    };
  }, [locale]);
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error("useI18n must be used within I18nProvider");
  return ctx;
}

