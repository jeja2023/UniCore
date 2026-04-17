const ACCESS_TOKEN_KEY = "unicore.accessToken";
const REFRESH_TOKEN_KEY = "unicore.refreshToken";
export const AUTH_CHANGED_EVENT = "unicore-auth-changed";
let accessTokenInMemory: string | null = null;
let refreshTokenInMemory: string | null = null;

function getSafeStorage(): Storage | null {
  try {
    return typeof window === "undefined" ? null : window.sessionStorage;
  } catch {
    return null;
  }
}

function emitAuthChanged() {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
  }
}

export function getAccessToken(): string | null {
  if (accessTokenInMemory) {
    return accessTokenInMemory;
  }

  const persisted = getSafeStorage()?.getItem(ACCESS_TOKEN_KEY) ?? null;
  if (persisted) {
    accessTokenInMemory = persisted;
  }

  return accessTokenInMemory;
}

export function isAccessTokenExpired(token: string, skewSeconds = 15): boolean {
  try {
    const payloadRaw = token.split(".")[1];
    if (!payloadRaw) {
      return true;
    }

    const base64 = payloadRaw.replace(/-/g, "+").replace(/_/g, "/");
    const json = decodeURIComponent(
      atob(base64)
        .split("")
        .map((ch) => `%${ch.charCodeAt(0).toString(16).padStart(2, "0")}`)
        .join("")
    );
    const payload = JSON.parse(json) as { exp?: number };
    if (typeof payload.exp !== "number") {
      return true;
    }

    const now = Math.floor(Date.now() / 1000);
    return payload.exp <= now + skewSeconds;
  } catch {
    return true;
  }
}

export function setAccessToken(token: string) {
  accessTokenInMemory = token;
  getSafeStorage()?.setItem(ACCESS_TOKEN_KEY, token);
  emitAuthChanged();
}

export function getRefreshToken(): string | null {
  if (refreshTokenInMemory) {
    return refreshTokenInMemory;
  }

  const persisted = getSafeStorage()?.getItem(REFRESH_TOKEN_KEY) ?? null;
  if (persisted) {
    refreshTokenInMemory = persisted;
  }

  return refreshTokenInMemory;
}

export function setRefreshToken(token: string) {
  refreshTokenInMemory = token;
  getSafeStorage()?.setItem(REFRESH_TOKEN_KEY, token);
  emitAuthChanged();
}

export function setAuthTokens(tokens: { accessToken: string; refreshToken: string }) {
  accessTokenInMemory = tokens.accessToken;
  refreshTokenInMemory = tokens.refreshToken;
  getSafeStorage()?.setItem(ACCESS_TOKEN_KEY, tokens.accessToken);
  getSafeStorage()?.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
  emitAuthChanged();
}

export function clearAccessToken() {
  accessTokenInMemory = null;
  refreshTokenInMemory = null;
  getSafeStorage()?.removeItem(ACCESS_TOKEN_KEY);
  getSafeStorage()?.removeItem(REFRESH_TOKEN_KEY);
  emitAuthChanged();
}

