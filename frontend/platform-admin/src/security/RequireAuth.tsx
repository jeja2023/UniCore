import React from "react";
import { Navigate, useLocation } from "react-router-dom";
import { ROUTE_PATHS } from "../routes/routePaths";
import { clearAccessToken, getAccessToken, isAccessTokenExpired } from "./tokenStore";

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const location = useLocation();
  const token = getAccessToken();
  if (!token || isAccessTokenExpired(token)) {
    clearAccessToken();
    return <Navigate to={ROUTE_PATHS.LOGIN} replace state={{ from: location.pathname }} />;
  }
  return <>{children}</>;
}

