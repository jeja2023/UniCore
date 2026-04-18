import React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { ROUTE_PATHS } from "./routePaths";
import { ShellLayout } from "./ShellLayout";

const permissionState = vi.hoisted(() => ({
  loading: false,
  permissions: new Set<string>(),
  menus: [] as Array<{ key: string; title: string; path: string; permission?: string | null }>,
}));
const navigateMock = vi.hoisted(() => vi.fn());
const clearAccessTokenMock = vi.hoisted(() => vi.fn());
const setModeMock = vi.hoisted(() => vi.fn());
const setLocaleMock = vi.hoisted(() => vi.fn());

vi.mock("../security/permissions", () => ({
  hasPermission: (permissions: Set<string>, required?: string | null) => !required || permissions.has(required),
  usePermissions: () => permissionState,
}));

vi.mock("../security/tokenStore", () => ({
  clearAccessToken: clearAccessTokenMock,
}));

vi.mock("../design/theme/ThemeProvider", () => ({
  useTheme: () => ({
    mode: "light",
    setMode: setModeMock,
    tokens: {
      colors: {
        bgSubtle: "#f5f5f5",
        text: "#111111",
        border: "#d0d0d0",
        bg: "#ffffff",
        textSecondary: "#666666",
        brand: "#0055cc",
      },
      space: {
        xs: 4,
        sm: 8,
        md: 12,
        lg: 16,
        xl: 24,
      },
      font: {
        family: "sans-serif",
      },
      radius: {
        sm: 4,
        md: 8,
      },
    },
  }),
}));

vi.mock("../i18n/I18nProvider", () => ({
  useI18n: () => ({
    locale: "en-US",
    setLocale: setLocaleMock,
    t: (key: string) =>
      ({
        appTitle: "UniCore",
        theme: "Theme",
        language: "Language",
        logout: "Logout",
      })[key] ?? key,
  }),
}));

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    useNavigate: () => navigateMock,
  };
});

describe("ShellLayout", () => {
  beforeEach(() => {
    navigateMock.mockReset();
    clearAccessTokenMock.mockReset();
    permissionState.loading = false;
    permissionState.permissions = new Set(["sample.read"]);
    permissionState.menus = [
      { key: "sample.home", title: "Sample", path: "/modules/sample", permission: "sample.read" },
      { key: "admin.secret", title: "Admin", path: "/admin", permission: "admin.manage" },
    ];
  });

  it("shows only menus allowed by the current permission set", () => {
    render(
      <MemoryRouter initialEntries={["/modules/sample"]}>
        <ShellLayout />
      </MemoryRouter>
    );

    expect(screen.getByText("Sample")).toBeTruthy();
    expect(screen.queryByText("Admin")).toBeNull();
  });

  it("clears auth state and navigates to login on logout", () => {
    render(
      <MemoryRouter initialEntries={["/modules/sample"]}>
        <ShellLayout />
      </MemoryRouter>
    );

    const [logoutButton] = screen.getAllByRole("button", { name: "Logout" });
    fireEvent.click(logoutButton);

    expect(clearAccessTokenMock).toHaveBeenCalledTimes(1);
    expect(navigateMock).toHaveBeenCalledWith(ROUTE_PATHS.LOGIN, { replace: true });
  });
});
