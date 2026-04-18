import React from "react";
import { StatusText } from "../base/StatusText";
import { IconCircleAlert, IconCircleCheck, IconSpinner } from "../icons/icons";

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
  loadingText = "加载中…",
}: PageAsyncStateProps) {
  if (error) {
    return (
      <StatusText tone="danger" icon={<IconCircleAlert size={16} />}>
        {error}
      </StatusText>
    );
  }

  if (success) {
    return (
      <StatusText tone="success" icon={<IconCircleCheck size={16} />}>
        {success}
      </StatusText>
    );
  }

  if (loading) {
    return (
      <StatusText tone="muted" icon={<IconSpinner size={16} />}>
        {loadingText}
      </StatusText>
    );
  }

  return null;
}
