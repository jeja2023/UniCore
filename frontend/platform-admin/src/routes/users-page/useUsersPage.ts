import { useCallback, useEffect, useState } from "react";
import { apiFetch } from "../../security/apiClient";
import { getErrorMessage } from "../../utils/errorMessage";
import type { UserDto, UsersResponse } from "./types";

export function useUsersPage() {
  const [users, setUsers] = useState<UserDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const loadUsers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const resp = await apiFetch<UsersResponse>("/api/identity/users");
      setUsers(resp.data);
    } catch (err: unknown) {
      setError(getErrorMessage(err, "加载失败"));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadUsers();
  }, [loadUsers]);

  return {
    users,
    error,
    loading,
    loadUsers,
  };
}
