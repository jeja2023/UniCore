import React from "react";
import { Navigate, useLocation } from "react-router-dom";
import { getAccessToken } from "./tokenStore";

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const location = useLocation();
  const token = getAccessToken();
  if (!token) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }
  return <>{children}</>;
}

