import type { ApiError } from "../security/apiClient";

export function getErrorMessage(error: unknown, fallback = "请求失败"): string {
  if (typeof error === "object" && error !== null) {
    const apiError = error as Partial<ApiError>;
    if (typeof apiError.message === "string" && apiError.message.trim().length > 0) {
      return apiError.message;
    }
  }

  return fallback;
}
