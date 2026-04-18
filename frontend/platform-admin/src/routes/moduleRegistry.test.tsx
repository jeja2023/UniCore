import React from "react";
import { describe, expect, it } from "vitest";
import { RequirePermission } from "../security/RequirePermission";
import { renderModuleRoutes } from "./moduleRegistry";

describe("renderModuleRoutes", () => {
  it("normalizes leading slashes and wraps protected routes", () => {
    const [route] = renderModuleRoutes();

    expect(route.props.path).toBe("modules/sample");
    expect(route.props.element.type).toBe(RequirePermission);
    expect(route.props.element.props.permission).toBe("sample.read");
  });
});
