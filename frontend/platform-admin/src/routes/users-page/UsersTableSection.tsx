import React from "react";
import { ListPageTemplate } from "../../components/patterns/ListPageTemplate";
import { useTheme } from "../../design/theme/ThemeProvider";
import type { UserDto } from "./types";

type UsersTableSectionProps = {
  users: UserDto[];
};

export function UsersTableSection({ users }: UsersTableSectionProps) {
  const { tokens } = useTheme();

  return (
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
              <th style={{ padding: "10px 12px", border: `1px solid ${tokens.colors.border}`, color: tokens.colors.textSecondary }}>用户标识（ID）</th>
              <th style={{ padding: "10px 12px", border: `1px solid ${tokens.colors.border}`, color: tokens.colors.textSecondary }}>租户</th>
              <th style={{ padding: "10px 12px", border: `1px solid ${tokens.colors.border}`, color: tokens.colors.textSecondary }}>账号</th>
              <th style={{ padding: "10px 12px", border: `1px solid ${tokens.colors.border}`, color: tokens.colors.textSecondary }}>昵称</th>
              <th style={{ padding: "10px 12px", border: `1px solid ${tokens.colors.border}`, color: tokens.colors.textSecondary }}>启用</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.userId}>
                <td style={{ padding: "10px 12px", border: `1px solid ${tokens.colors.border}` }}>{u.userId}</td>
                <td style={{ padding: "10px 12px", border: `1px solid ${tokens.colors.border}` }}>{u.tenantId}</td>
                <td style={{ padding: "10px 12px", border: `1px solid ${tokens.colors.border}` }}>{u.username}</td>
                <td style={{ padding: "10px 12px", border: `1px solid ${tokens.colors.border}` }}>{u.displayName}</td>
                <td
                  style={{
                    padding: "10px 12px",
                    border: `1px solid ${tokens.colors.border}`,
                    fontWeight: 600,
                    color: u.enabled ? tokens.colors.brand : tokens.colors.textSecondary,
                  }}
                >
                  {u.enabled ? "是" : "否"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </ListPageTemplate>
  );
}
