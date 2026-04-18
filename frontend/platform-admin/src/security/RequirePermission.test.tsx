import React from "react";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ROUTE_PATHS } from "../routes/routePaths";
import { RequirePermission } from "./RequirePermission";

const permissionState = vi.hoisted(() => ({
  loading: false,
  permissions: new Set<string>(),
}));

vi.mock("./permissions", () => ({
  hasPermission: (permissions: Set<string>, required?: string | null) => !required || permissions.has(required),
  usePermissions: () => permissionState,
}));

vi.mock("../components/patterns/PageAsyncState", () => ({
  PageAsyncState: ({ loadingText }: { loadingText: string }) => <div data-testid="loading">{loadingText}</div>,
}));

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    Navigate: ({ to }: { to: string }) => <div data-testid="navigate">{to}</div>,
  };
});

describe("RequirePermission", () => {
  beforeEach(() => {
    permissionState.loading = false;
    permissionState.permissions = new Set<string>();
  });

  it("renders children when the user has permission", () => {
    permissionState.permissions = new Set(["sample.read"]);

    render(
      <RequirePermission permission="sample.read">
        <div>allowed content</div>
      </RequirePermission>
    );

    expect(screen.getByText("allowed content")).toBeTruthy();
  });

  it("redirects to root when the user lacks permission", () => {
    render(
      <RequirePermission permission="sample.read">
        <div>hidden content</div>
      </RequirePermission>
    );

    expect(screen.getByTestId("navigate").textContent).toBe(ROUTE_PATHS.ROOT);
  });
});
