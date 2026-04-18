import React, { useMemo } from "react";
import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";
import { clearAccessToken, getAccessToken } from "../security/tokenStore";
import { hasPermission, usePermissions } from "../security/permissions";
import { useTheme } from "../design/theme/ThemeProvider";
import { useI18n } from "../i18n/I18nProvider";
import { PageAsyncState } from "../components/patterns/PageAsyncState";
import { StatusText } from "../components/base/StatusText";
import { createGlassControlVars, createGlassPanelStyle } from "../styles/glass";
import { ROUTE_PATHS } from "./routePaths";
import { prewarmAfterAuth } from "./prewarm";
import {
  IconInbox,
  IconLayers,
  IconLogOut,
  IconMenu,
  IconMoon,
  IconPalette,
  IconSun,
} from "../components/icons/icons";

type MenuItem = {
  key: string;
  title: string;
  path: string;
  permission?: string | null;
};

const blobBase: React.CSSProperties = {
  position: "absolute",
  borderRadius: "50%",
  filter: "blur(56px)",
  opacity: 0.55,
  pointerEvents: "none",
  animation: "float-blob 18s ease-in-out infinite",
};

export function ShellLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const { loading, menus, permissions } = usePermissions();
  const { mode, setMode, tokens } = useTheme();
  const { t } = useI18n();

  const activePath = useMemo(() => location.pathname, [location.pathname]);

  React.useEffect(() => {
    if (loading) return;
    const token = getAccessToken();
    if (!token) return;

    const allowedMenuPaths = (menus as MenuItem[])
      .filter((m) => hasPermission(permissions, m.permission))
      .map((m) => m.path);

    prewarmAfterAuth({ key: token, menuPaths: allowedMenuPaths, maxModuleCount: 3 });
  }, [loading, menus, permissions]);

  function logout() {
    clearAccessToken();
    navigate(ROUTE_PATHS.LOGIN, { replace: true });
  }

  const glassPanel = createGlassPanelStyle(tokens);
  const glassControlStyle = createGlassControlVars(tokens);

  return (
    <div
      style={{
        position: "relative",
        display: "flex",
        minHeight: "100vh",
        color: tokens.colors.text,
        fontFamily: tokens.font.family,
        overflowX: "hidden",
      }}
    >
      <div
        aria-hidden
        style={{
          ...blobBase,
          width: 320,
          height: 320,
          top: "-80px",
          left: "-60px",
          background: mode === "light" ? "rgba(129, 161, 255, 0.5)" : "rgba(37, 99, 235, 0.35)",
          animationDelay: "0s",
        }}
      />
      <div
        aria-hidden
        style={{
          ...blobBase,
          width: 280,
          height: 280,
          bottom: "5%",
          right: "-40px",
          background: mode === "light" ? "rgba(244, 114, 182, 0.35)" : "rgba(124, 58, 237, 0.3)",
          animationDelay: "-6s",
        }}
      />

      <aside
        style={{
          width: 268,
          flexShrink: 0,
          margin: tokens.space.lg,
          marginRight: tokens.space.sm,
          padding: tokens.space.lg,
          alignSelf: "flex-start",
          position: "sticky",
          top: tokens.space.lg,
          maxHeight: "calc(100vh - 32px)",
          overflowY: "auto",
          borderRadius: tokens.radius.lg,
          ...glassPanel,
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 12,
            fontWeight: 800,
            fontSize: 18,
            marginBottom: tokens.space.lg,
            color: tokens.colors.text,
          }}
        >
          <span
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              width: 44,
              height: 44,
              borderRadius: tokens.radius.md,
              background: tokens.colors.brandSoft,
              color: tokens.colors.brand,
              boxShadow: `inset 0 1px 0 rgba(255,255,255,0.35)`,
            }}
          >
            <IconLayers size={22} title="UniCore 标识" />
          </span>
          <div>
            <div>UniCore</div>
            <div style={{ fontSize: 11, fontWeight: 600, color: tokens.colors.textSecondary, marginTop: 2 }}>
              平台管理
            </div>
          </div>
        </div>
        <nav style={{ display: "grid", gap: 6 }}>
          <div
            style={{
              marginTop: tokens.space.xs,
              marginBottom: 4,
              fontSize: 11,
              fontWeight: 700,
              color: tokens.colors.textSecondary,
              display: "flex",
              alignItems: "center",
              gap: 6,
            }}
          >
            <IconMenu size={14} title="导航" />
            导航菜单
          </div>
          {loading ? (
            <PageAsyncState loading={loading} loadingText="菜单加载中…" />
          ) : menus.length === 0 ? (
            <StatusText tone="muted" icon={<IconInbox size={14} />}>
              暂无菜单
            </StatusText>
          ) : (
            (menus as MenuItem[])
              .filter((m) => hasPermission(permissions, m.permission))
              .map((m) => {
                const active = activePath === m.path;
                return (
                  <Link
                    key={m.key}
                    to={m.path}
                    style={{
                      fontWeight: active ? 700 : 500,
                      fontSize: 14,
                      color: active ? tokens.colors.brand : tokens.colors.text,
                      textDecoration: "none",
                      padding: "10px 12px",
                      borderRadius: tokens.radius.md,
                      background: active ? tokens.colors.brandSoft : "transparent",
                      border: active ? `1px solid ${tokens.glass.borderHighlight}` : "1px solid transparent",
                      boxShadow: active ? tokens.glass.shadow : "none",
                      transition: "background 0.2s, box-shadow 0.2s, border-color 0.2s",
                    }}
                  >
                    {m.title}
                  </Link>
                );
              })
          )}
        </nav>
      </aside>

      <main
        style={{
          flex: 1,
          padding: `${tokens.space.lg}px ${tokens.space.xl}px ${tokens.space.xl}px`,
          minWidth: 0,
        }}
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: tokens.space.lg,
            padding: `${tokens.space.md}px ${tokens.space.lg}px`,
            borderRadius: tokens.radius.lg,
            gap: tokens.space.md,
            flexWrap: "wrap",
            ...glassPanel,
          }}
        >
          <div style={{ fontWeight: 700, fontSize: 17, letterSpacing: "-0.02em" }}>{t("appTitle")}</div>
          <div style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
            <label
              style={{
                fontSize: 12,
                display: "inline-flex",
                alignItems: "center",
                gap: 8,
                color: tokens.colors.textSecondary,
              }}
            >
              <span style={{ display: "flex", color: tokens.colors.text }}>
                <IconPalette size={14} title={t("theme")} />
              </span>
              {t("theme")}
              <select
                value={mode}
                onChange={(e) => setMode(e.target.value as "light" | "dark")}
                className="glass-control"
                style={{
                  ...glassControlStyle,
                  padding: "6px 10px",
                  color: tokens.colors.text,
                }}
              >
                <option value="light">浅色</option>
                <option value="dark">深色</option>
              </select>
              <span style={{ display: "flex", color: tokens.colors.text }} aria-hidden>
                {mode === "light" ? <IconSun size={14} /> : <IconMoon size={14} />}
              </span>
            </label>
            <button
              type="button"
              onClick={logout}
              style={{
                border: `1px solid ${tokens.glass.border}`,
                borderRadius: tokens.radius.md,
                background: tokens.glass.surface,
                padding: "8px 14px",
                cursor: "pointer",
                display: "inline-flex",
                alignItems: "center",
                gap: 8,
                color: tokens.colors.text,
                fontSize: 13,
                fontWeight: 600,
                backdropFilter: `blur(${tokens.glass.blur})`,
                boxShadow: tokens.glass.shadow,
              }}
            >
              <IconLogOut size={16} />
              {t("logout")}
            </button>
          </div>
        </div>
        <Outlet />
      </main>
    </div>
  );
}
