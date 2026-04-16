import { getAccessToken } from "./tokenStore";

export type ApiError = {
  status: number;
  code?: string;
  message: string;
  traceId?: string;
};

export async function apiFetch<T>(
  input: string,
  init?: RequestInit
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

