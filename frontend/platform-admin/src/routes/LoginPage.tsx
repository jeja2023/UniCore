import React, { useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { apiFetch } from "../security/apiClient";
import { setAuthTokens } from "../security/tokenStore";
import { Button } from "../components/base/Button";
import { FormPageTemplate } from "../components/patterns/FormPageTemplate";
import { PageAsyncState } from "../components/patterns/PageAsyncState";
import { useTheme } from "../design/theme/ThemeProvider";
import { createGlassControlVars, createGlassPanelStyle } from "../styles/glass";
import { ROUTE_PATHS } from "./routePaths";
import { getErrorMessage } from "../utils/errorMessage";
import { IconLock } from "../components/icons/icons";

type LoginResult = {
  data: { accessToken: string; refreshToken: string };
};

const blobBase: React.CSSProperties = {
  position: "absolute",
  borderRadius: "50%",
  filter: "blur(52px)",
  opacity: 0.5,
  pointerEvents: "none",
  animation: "float-blob 20s ease-in-out infinite",
};

export function LoginPage() {
  const { tokens, mode } = useTheme();
  const navigate = useNavigate();
  const location = useLocation();
  const redirectTo = useMemo(() => {
    const state = location.state as { from?: string } | null;
    return state?.from ?? ROUTE_PATHS.ROOT;
  }, [location.state]);

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [tenantId, setTenantId] = useState("default");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!tenantId.trim() || !username.trim() || !password) {
      setError("租户、账号和密码不能为空");
      return;
    }
    setLoading(true);
    try {
      const resp = await apiFetch<LoginResult>("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({ username: username.trim(), password, tenantId: tenantId.trim() }),
      });
      setAuthTokens({
        accessToken: resp.data.accessToken,
        refreshToken: resp.data.refreshToken,
      });
      navigate(redirectTo, { replace: true });
    } catch (err: unknown) {
      setError(getErrorMessage(err, "登录失败"));
    } finally {
      setLoading(false);
    }
  }

  const glassControlStyle = createGlassControlVars(tokens);
  const glassCard: React.CSSProperties = {
    ...createGlassPanelStyle(tokens, { shadow: tokens.glass.shadowHover, saturate: 175 }),
    width: "100%",
    maxWidth: 440,
    padding: tokens.space.xl,
  };

  return (
    <div
      style={{
        minHeight: "100vh",
        display: "grid",
        placeItems: "center",
        padding: tokens.space.lg,
        position: "relative",
        overflow: "hidden",
      }}
    >
      <div
        aria-hidden
        style={{
          ...blobBase,
          width: 360,
          height: 360,
          top: "-100px",
          right: "-80px",
          background: mode === "light" ? "rgba(99, 102, 241, 0.45)" : "rgba(59, 130, 246, 0.28)",
        }}
      />
      <div
        aria-hidden
        style={{
          ...blobBase,
          width: 300,
          height: 300,
          bottom: "-60px",
          left: "-50px",
          background: mode === "light" ? "rgba(236, 72, 153, 0.32)" : "rgba(168, 85, 247, 0.22)",
          animationDelay: "-8s",
        }}
      />

      <div style={glassCard}>
        <div style={{ display: "flex", alignItems: "center", gap: 14, marginBottom: tokens.space.lg }}>
          <span
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              width: 52,
              height: 52,
              borderRadius: tokens.radius.lg,
              background: tokens.colors.brandSoft,
              border: `1px solid ${tokens.glass.borderHighlight}`,
              color: tokens.colors.brand,
              flexShrink: 0,
              boxShadow: `inset 0 1px 0 rgba(255,255,255,0.4)`,
            }}
          >
            <IconLock size={24} title="登录" />
          </span>
          <div>
            <h1 style={{ margin: 0, fontSize: 22, fontWeight: 800, letterSpacing: "-0.02em" }}>UniCore 管理台</h1>
            <p style={{ margin: "6px 0 0", color: tokens.colors.textSecondary, fontSize: 13, lineHeight: 1.5 }}>
              请输入租户与账号信息后登录
            </p>
          </div>
        </div>
        <FormPageTemplate
          title="登录"
          onSubmit={onSubmit}
          actions={
            <div style={{ display: "grid" }}>
              <Button type="submit" variant="primary" disabled={loading}>
                {loading ? "登录中…" : "登录"}
              </Button>
            </div>
          }
        >
          <label style={{ display: "block", marginBottom: 10, fontSize: 13 }}>
            租户标识（ID）
            <input
              value={tenantId}
              onChange={(e) => setTenantId(e.target.value)}
              className="glass-control"
              style={{ ...glassControlStyle, width: "100%", padding: "11px 12px", marginTop: 6 }}
            />
          </label>
          <label style={{ display: "block", marginBottom: 10, fontSize: 13 }}>
            账号
            <input
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="glass-control"
              style={{ ...glassControlStyle, width: "100%", padding: "11px 12px", marginTop: 6 }}
            />
          </label>
          <label style={{ display: "block", marginBottom: 12, fontSize: 13 }}>
            密码
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="glass-control"
              style={{ ...glassControlStyle, width: "100%", padding: "11px 12px", marginTop: 6 }}
            />
          </label>
        </FormPageTemplate>
        <div style={{ marginTop: 14 }}>
          <PageAsyncState loading={loading} error={error} loadingText="正在验证账号…" />
        </div>
      </div>
    </div>
  );
}
