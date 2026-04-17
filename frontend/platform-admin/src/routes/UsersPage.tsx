import React from "react";
import { useTheme } from "../design/theme/ThemeProvider";
import { useUsersPage } from "./users-page/useUsersPage";
import { UsersHeaderSection } from "./users-page/UsersHeaderSection";
import { UsersTableSection } from "./users-page/UsersTableSection";

export function UsersPage() {
  const { tokens } = useTheme();
  const { users, error, loading, loadUsers } = useUsersPage();

  return (
    <div style={{ display: "grid", gap: tokens.space.lg }}>
      <UsersHeaderSection loading={loading} error={error} onRefresh={() => void loadUsers()} />
      <UsersTableSection users={users} />
    </div>
  );
}

