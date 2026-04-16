import React, { useMemo } from "react";
import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";
import { clearAccessToken } from "../security/tokenStore";
import { hasPermission, usePermissions } from "../security/permissions";
import { useTheme } from "../design/theme/ThemeProvider";
import { useI18n } from "../i18n/I18nProvider";

type MenuItem = {
  key: string;
  title: string;
  path: string;
  permission?: string | null;
};

export function ShellLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const { loading, menus, permissions } = usePermissions();
  const { mode, setMode, tokens } = useTheme();
  const { locale, setLocale, t } = useI18n();

  const activePath = useMemo(() => location.pathname, [location.pathname]);

  function logout() {
    clearAccessToken();
    navigate("/login", { replace: true });
  }

  return (
    <div
      style={{
        display: "flex",
        minHeight: "100vh",
        background: tokens.colors.bgSubtle,
        color: tokens.colors.text,
        fontFamily: tokens.font.family,
      }}
    >
      <aside
        style={{
          width: 240,
          borderRight: `1px solid ${tokens.colors.border}`,
          padding: tokens.space.lg,
          background: tokens.colors.bg,
        }}
      >
        <div style={{ fontWeight: 800, fontSize: 18, marginBottom: tokens.space.lg }}>UniCore</div>
        <nav style={{ display: "grid", gap: 8 }}>
          <div style={{ marginTop: tokens.space.sm, fontSize: 12, color: tokens.colors.textSecondary }}>
            菜单（平台 + 模块契约）
          </div>
          {loading ? (
            <div style={{ fontSize: 12, color: tokens.colors.textSecondary }}>加载中…</div>
          ) : menus.length === 0 ? (
            <div style={{ fontSize: 12, color: tokens.colors.textSecondary }}>暂无</div>
          ) : (
            (menus as MenuItem[])
              .filter((m) => hasPermission(permissions, m.permission))
              .map((m) => (
                <Link
                  key={m.key}
                  to={m.path}
                  style={{
                    fontWeight: activePath === m.path ? 700 : 500,
                    color: activePath === m.path ? tokens.colors.brand : tokens.colors.text,
                    textDecoration: "none",
                    padding: "8px 10px",
                    borderRadius: tokens.radius.sm,
                    background: activePath === m.path ? tokens.colors.bgSubtle : "transparent",
                  }}
                >
                  {m.title}
                </Link>
              ))
          )}
        </nav>
      </aside>

      <main style={{ flex: 1, padding: tokens.space.xl }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: tokens.space.lg,
            padding: `${tokens.space.md}px ${tokens.space.lg}px`,
            border: `1px solid ${tokens.colors.border}`,
            borderRadius: tokens.radius.md,
            background: tokens.colors.bg,
          }}
        >
          <div style={{ fontWeight: 700, fontSize: 16 }}>{t("appTitle")}</div>
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <label style={{ fontSize: 12 }}>
              {t("theme")}
              <select
                value={mode}
                onChange={(e) => setMode(e.target.value as "light" | "dark")}
                style={{ marginLeft: 6, border: `1px solid ${tokens.colors.border}`, borderRadius: tokens.radius.sm }}
              >
                <option value="light">light</option>
                <option value="dark">dark</option>
              </select>
            </label>
            <label style={{ fontSize: 12 }}>
              {t("language")}
              <select
                value={locale}
                onChange={(e) => setLocale(e.target.value as "zh-CN" | "en-US")}
                style={{ marginLeft: 6, border: `1px solid ${tokens.colors.border}`, borderRadius: tokens.radius.sm }}
              >
                <option value="zh-CN">zh-CN</option>
                <option value="en-US">en-US</option>
              </select>
            </label>
            <button
              onClick={logout}
              style={{
                border: `1px solid ${tokens.colors.border}`,
                borderRadius: tokens.radius.sm,
                background: tokens.colors.bg,
                padding: "6px 10px",
                cursor: "pointer",
              }}
            >
              {t("logout")}
            </button>
          </div>
        </div>
        <Outlet />
      </main>
    </div>
  );
}

