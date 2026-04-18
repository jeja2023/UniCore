import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useModulesPage } from "./useModulesPage";

const apiFetchMock = vi.hoisted(() => vi.fn());

vi.mock("../../security/apiClient", () => ({
  apiFetch: apiFetchMock,
}));

vi.mock("../moduleRegistry.generated", () => ({
  frontendModules: [
    {
      sourceDir: "sample-module",
      packageName: "@unicore/sample-module",
      version: "0.1.0",
      moduleCode: "sample",
      routes: [{ path: "/modules/sample", permission: "sample.read" }],
      routePaths: ["/modules/sample"],
      routePermissions: ["sample.read"],
    },
  ],
}));

describe("useModulesPage", () => {
  beforeEach(() => {
    apiFetchMock.mockReset();
  });

  it("reports backend-only permissions and menu-to-route permission mismatches", async () => {
    apiFetchMock.mockResolvedValue({
      data: [
        {
          moduleCode: "sample",
          moduleName: "Sample",
          moduleVersion: "1.0.0",
          permissions: [{ permissionCode: "sample.read" }, { permissionCode: "sample.update" }],
          menus: [
            {
              menuCode: "sample.home",
              routePath: "/modules/sample",
              permissionCode: "sample.update",
            },
          ],
        },
      ],
    });

    const { result } = renderHook(() => useModulesPage());

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.error).toBeNull();
    expect(result.current.alignmentDetails).toHaveLength(1);
    expect(result.current.alignmentDetails[0].backendPermissionsWithoutFrontendRoutes).toEqual(["sample.update"]);
    expect(result.current.alignmentDetails[0].routePermissionMismatches).toEqual([
      "sample.home (sample.read != sample.update)",
    ]);
  });
});
