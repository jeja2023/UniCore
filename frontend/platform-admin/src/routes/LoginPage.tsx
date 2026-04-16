import React, { useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { apiFetch } from "../security/apiClient";
import { setAccessToken } from "../security/tokenStore";
import { Button } from "../components/base/Button";
import { FormPageTemplate } from "../components/patterns/FormPageTemplate";
import { useTheme } from "../design/theme/ThemeProvider";

type LoginResult = {
  data: { accessToken: string; refreshToken: string };
};

function getErrorMessage(err: unknown): string {
  if (typeof err === "object" && err !== null && "message" in err) {
    const msg = (err as { message?: unknown }).message;
    if (typeof msg === "string" && msg.length > 0) return msg;
  }
  return "登录失败";
}

export function LoginPage() {
  const { tokens } = useTheme();
  const navigate = useNavigate();
  const location = useLocation();
  const redirectTo = useMemo(() => {
    const state = location.state as { from?: string } | null;
    return state?.from ?? "/";
  }, [location.state]);

  const [username, setUsername] = useState("admin");
  const [password, setPassword] = useState("UniCore@123");
  const [tenantId, setTenantId] = useState("default");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const resp = await apiFetch<LoginResult>("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({ username, password, tenantId }),
      });
      setAccessToken(resp.data.accessToken);
      navigate(redirectTo, { replace: true });
    } catch (err: unknown) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div
      style={{
        maxWidth: 460,
        margin: "8vh auto",
        padding: tokens.space.xl,
        borderRadius: tokens.radius.md,
        background: tokens.colors.bgSubtle,
      }}
    >
      <h2 style={{ margin: 0, marginBottom: tokens.space.sm }}>UniCore Admin</h2>
      <div style={{ color: tokens.colors.textSecondary, marginBottom: tokens.space.lg }}>请输入账号信息后登录管理台</div>
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
        <label style={{ display: "block", marginBottom: 8 }}>
          租户
          <input
            value={tenantId}
            onChange={(e) => setTenantId(e.target.value)}
            style={{
              width: "100%",
              padding: "9px 10px",
              marginTop: 4,
              border: `1px solid ${tokens.colors.border}`,
              borderRadius: tokens.radius.sm,
              background: tokens.colors.bg,
              color: tokens.colors.text,
              boxSizing: "border-box",
            }}
          />
        </label>
        <label style={{ display: "block", marginBottom: 8 }}>
          账号
          <input
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            style={{
              width: "100%",
              padding: "9px 10px",
              marginTop: 4,
              border: `1px solid ${tokens.colors.border}`,
              borderRadius: tokens.radius.sm,
              background: tokens.colors.bg,
              color: tokens.colors.text,
              boxSizing: "border-box",
            }}
          />
        </label>
        <label style={{ display: "block", marginBottom: 12 }}>
          密码
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            style={{
              width: "100%",
              padding: "9px 10px",
              marginTop: 4,
              border: `1px solid ${tokens.colors.border}`,
              borderRadius: tokens.radius.sm,
              background: tokens.colors.bg,
              color: tokens.colors.text,
              boxSizing: "border-box",
            }}
          />
        </label>
      </FormPageTemplate>
      {error ? (
        <div style={{ marginTop: 12, color: tokens.colors.danger, fontSize: 13 }}>{error}</div>
      ) : null}
    </div>
  );
}

