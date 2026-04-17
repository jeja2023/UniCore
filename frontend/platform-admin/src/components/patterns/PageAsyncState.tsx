import React from "react";
import { StatusText } from "../base/StatusText";

type PageAsyncStateProps = {
  loading: boolean;
  error?: string | null;
  success?: string | null;
  loadingText?: string;
};

export function PageAsyncState({
  loading,
  error = null,
  success = null,
  loadingText = "加载中...",
}: PageAsyncStateProps) {
  if (error) {
    return <StatusText tone="danger">{error}</StatusText>;
  }

  if (success) {
    return <StatusText tone="success">{success}</StatusText>;
  }

  if (loading) {
    return <StatusText tone="muted">{loadingText}</StatusText>;
  }

  return null;
}
