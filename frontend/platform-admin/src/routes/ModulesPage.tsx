import React, { useEffect, useState } from "react";
import { apiFetch } from "../security/apiClient";

type ModuleContractsResponse = {
  data: Array<{
    moduleCode: string;
    moduleName: string;
    moduleVersion: string;
    permissions: Array<{ code: string; name: string }>;
  }>;
};

function getErrorMessage(err: unknown): string {
  if (typeof err === "object" && err !== null && "message" in err) {
    const msg = (err as { message?: unknown }).message;
    if (typeof msg === "string" && msg.length > 0) return msg;
  }
  return "加载失败";
}

export function ModulesPage() {
  const [modules, setModules] = useState<ModuleContractsResponse["data"]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setError(null);
      try {
        const resp = await apiFetch<ModuleContractsResponse>(
          "/api/modules/contracts"
        );
        if (!cancelled) setModules(resp.data);
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
    <div>
      <h3>模块</h3>
      {error ? <div style={{ color: "#b42318" }}>{error}</div> : null}
      <ul>
        {modules.map((m) => (
          <li key={m.moduleCode}>
            <b>{m.moduleName}</b>（{m.moduleCode} / {m.moduleVersion}）权限点：
            {m.permissions.length}
          </li>
        ))}
      </ul>
    </div>
  );
}

