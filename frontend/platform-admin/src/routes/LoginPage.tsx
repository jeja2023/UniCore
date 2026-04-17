import React, { useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { apiFetch } from "../security/apiClient";
import { setAuthTokens } from "../security/tokenStore";
import { Button } from "../components/base/Button";
import { FormPageTemplate } from "../components/patterns/FormPageTemplate";
import { PageAsyncState } from "../components/patterns/PageAsyncState";
import { useTheme } from "../design/theme/ThemeProvider";
import { ROUTE_PATHS } from "./routePaths";
import { getErrorMessage } from "../utils/errorMessage";

type LoginResult = {
  data: { accessToken: string; refreshToken: string };
};

export function LoginPage() {
  const { tokens } = useTheme();
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
      <div style={{ marginTop: 12 }}>
        <PageAsyncState loading={loading} error={error} loadingText="正在验证账号..." />
      </div>
    </div>
  );
}

