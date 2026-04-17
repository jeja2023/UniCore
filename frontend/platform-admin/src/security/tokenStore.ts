const ACCESS_TOKEN_KEY = "unicore.accessToken";
const REFRESH_TOKEN_KEY = "unicore.refreshToken";
export const AUTH_CHANGED_EVENT = "unicore-auth-changed";
let accessTokenInMemory: string | null = null;
let refreshTokenInMemory: string | null = null;

export function getAccessToken(): string | null {
  if (accessTokenInMemory) {
    return accessTokenInMemory;
  }

  const persisted = localStorage.getItem(ACCESS_TOKEN_KEY);
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
  localStorage.setItem(ACCESS_TOKEN_KEY, token);
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}

export function getRefreshToken(): string | null {
  if (refreshTokenInMemory) {
    return refreshTokenInMemory;
  }

  const persisted = localStorage.getItem(REFRESH_TOKEN_KEY);
  if (persisted) {
    refreshTokenInMemory = persisted;
  }

  return refreshTokenInMemory;
}

export function setRefreshToken(token: string) {
  refreshTokenInMemory = token;
  localStorage.setItem(REFRESH_TOKEN_KEY, token);
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}

export function setAuthTokens(tokens: { accessToken: string; refreshToken: string }) {
  accessTokenInMemory = tokens.accessToken;
  refreshTokenInMemory = tokens.refreshToken;
  localStorage.setItem(ACCESS_TOKEN_KEY, tokens.accessToken);
  localStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}

export function clearAccessToken() {
  accessTokenInMemory = null;
  refreshTokenInMemory = null;
  localStorage.removeItem(ACCESS_TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}

