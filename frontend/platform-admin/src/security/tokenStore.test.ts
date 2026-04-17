import { beforeEach, describe, expect, it } from "vitest";
import { clearAccessToken, getAccessToken, getRefreshToken, setAuthTokens } from "./tokenStore";

describe("tokenStore", () => {
  beforeEach(() => {
    clearAccessToken();
    window.sessionStorage.clear();
  });

  it("stores tokens in sessionStorage", () => {
    setAuthTokens({
      accessToken: "access-token",
      refreshToken: "refresh-token",
    });

    expect(getAccessToken()).toBe("access-token");
    expect(getRefreshToken()).toBe("refresh-token");
    expect(window.sessionStorage.getItem("unicore.accessToken")).toBe("access-token");
    expect(window.sessionStorage.getItem("unicore.refreshToken")).toBe("refresh-token");
    expect(window.localStorage.getItem("unicore.accessToken")).toBeNull();
  });
});
