import React, { useEffect, useState } from "react";
import { apiFetch } from "../security/apiClient";
import { ListPageTemplate } from "../components/patterns/ListPageTemplate";
import { useTheme } from "../design/theme/ThemeProvider";

type UserDto = {
  userId: string;
  tenantId: string;
  username: string;
  displayName: string;
  enabled: boolean;
};

type UsersResponse = { data: UserDto[] };

function getErrorMessage(err: unknown): string {
  if (typeof err === "object" && err !== null && "message" in err) {
    const msg = (err as { message?: unknown }).message;
    if (typeof msg === "string" && msg.length > 0) return msg;
  }
  return "加载失败";
}

export function UsersPage() {
  const { tokens } = useTheme();
  const [users, setUsers] = useState<UserDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setError(null);
      try {
        const resp = await apiFetch<UsersResponse>("/api/identity/users");
        if (!cancelled) setUsers(resp.data);
      } catch (err: unknown) {
        if (!cancelled) setError(getErrorMessage(err));
      }
    }
    load();
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div style={{ display: "grid", gap: tokens.space.lg }}>
      <section
        style={{
          border: `1px solid ${tokens.colors.border}`,
          borderRadius: tokens.radius.md,
          background: tokens.colors.bg,
          padding: tokens.space.lg,
        }}
      >
        <h3 style={{ margin: 0 }}>用户</h3>
        <div style={{ marginTop: tokens.space.xs, color: tokens.colors.textSecondary }}>查看租户下用户基础信息和启用状态</div>
      </section>
      {error ? <div style={{ color: tokens.colors.danger, fontSize: 13 }}>{error}</div> : null}
      <ListPageTemplate title="用户列表">
        <div style={{ overflowX: "auto" }}>
          <table
            cellPadding={0}
            style={{
              borderCollapse: "separate",
              borderSpacing: 0,
              width: "100%",
              minWidth: 720,
            }}
          >
          <thead>
            <tr style={{ textAlign: "left" }}>
              <th style={{ padding: "10px 12px", borderBottom: `1px solid ${tokens.colors.border}`, color: tokens.colors.textSecondary }}>ID</th>
              <th style={{ padding: "10px 12px", borderBottom: `1px solid ${tokens.colors.border}`, color: tokens.colors.textSecondary }}>租户</th>
              <th style={{ padding: "10px 12px", borderBottom: `1px solid ${tokens.colors.border}`, color: tokens.colors.textSecondary }}>账号</th>
              <th style={{ padding: "10px 12px", borderBottom: `1px solid ${tokens.colors.border}`, color: tokens.colors.textSecondary }}>昵称</th>
              <th style={{ padding: "10px 12px", borderBottom: `1px solid ${tokens.colors.border}`, color: tokens.colors.textSecondary }}>启用</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.userId}>
                <td style={{ padding: "10px 12px", borderBottom: `1px solid ${tokens.colors.border}` }}>{u.userId}</td>
                <td style={{ padding: "10px 12px", borderBottom: `1px solid ${tokens.colors.border}` }}>{u.tenantId}</td>
                <td style={{ padding: "10px 12px", borderBottom: `1px solid ${tokens.colors.border}` }}>{u.username}</td>
                <td style={{ padding: "10px 12px", borderBottom: `1px solid ${tokens.colors.border}` }}>{u.displayName}</td>
                <td style={{ padding: "10px 12px", borderBottom: `1px solid ${tokens.colors.border}`, fontWeight: 600, color: u.enabled ? tokens.colors.brand : tokens.colors.textSecondary }}>
                  {u.enabled ? "是" : "否"}
                </td>
              </tr>
            ))}
          </tbody>
          </table>
        </div>
      </ListPageTemplate>
    </div>
  );
}

