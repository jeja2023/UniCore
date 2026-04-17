import { clearAccessToken, getAccessToken, getRefreshToken, setAuthTokens } from "./tokenStore";

export type ApiError = {
  status: number;
  code?: string;
  message: string;
  traceId?: string;
};

type RefreshResponse = {
  data: { accessToken: string; refreshToken: string };
};

let refreshingPromise: Promise<boolean> | null = null;

async function tryRefreshToken(): Promise<boolean> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) {
    return false;
  }
  if (refreshingPromise) {
    return refreshingPromise;
  }

  refreshingPromise = (async () => {
    const resp = await fetch("/api/auth/refresh", {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ refreshToken }),
    });
    if (!resp.ok) {
      clearAccessToken();
      return false;
    }
    const body = (await resp.json().catch(() => null)) as RefreshResponse | null;
    const accessToken = body?.data?.accessToken;
    const newRefreshToken = body?.data?.refreshToken;
    if (!accessToken || !newRefreshToken) {
      clearAccessToken();
      return false;
    }

    setAuthTokens({ accessToken, refreshToken: newRefreshToken });
    return true;
  })().finally(() => {
    refreshingPromise = null;
  });

  return refreshingPromise;
}

export async function apiFetch<T>(
  input: string,
  init?: RequestInit,
  retryOnUnauthorized = true
): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set("Accept", "application/json");
  if (!headers.has("Content-Type") && init?.body) {
    headers.set("Content-Type", "application/json");
  }
  const token = getAccessToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const resp = await fetch(input, { ...init, headers });
  const contentType = resp.headers.get("content-type") ?? "";
  const isJson = contentType.includes("application/json");
  const body = isJson ? await resp.json().catch(() => null) : await resp.text();

  if (!resp.ok) {
    if (resp.status === 401 && retryOnUnauthorized) {
      const refreshed = await tryRefreshToken();
      if (refreshed) {
        return apiFetch<T>(input, init, false);
      }
    }

    const err: ApiError = {
      status: resp.status,
      code: body?.error?.code ?? body?.code,
      message:
        body?.error?.message ??
        body?.message ??
        (typeof body === "string" ? body : "请求失败"),
      traceId: body?.traceId ?? body?.error?.traceId,
    };
    throw err;
  }

  return body as T;
}

