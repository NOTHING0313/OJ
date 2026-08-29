export const baseUrl = "";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly errorCode?: string,
    public readonly retryAfterSeconds?: number
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export interface ApiRequestOptions extends RequestInit {
  suppressAuthenticationHandler?: boolean;
}

type AuthenticationErrorHandler = (error: ApiError) => void;

let authenticationErrorHandler: AuthenticationErrorHandler | null = null;
let authenticationErrorHandled = false;

const apiBusinessMessages: Record<string, string> = {
  "Slug already exists.": "Slug 已被使用。"
};

export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback;
  if (error instanceof ApiError && error.status === 429) return error.message;
  return apiBusinessMessages[error.message.trim()] ?? fallback;
}

export function setAuthenticationErrorHandler(handler: AuthenticationErrorHandler | null) {
  authenticationErrorHandler = handler;
}

export function resetAuthenticationErrorGuard() {
  authenticationErrorHandled = false;
}

export async function request<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  const token = localStorage.getItem("accessToken");
  const headers = new Headers(options.headers);
  const { suppressAuthenticationHandler = false, ...fetchOptions } = options;

  if (!(options.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  if (token && !headers.has("Authorization")) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(`${baseUrl}${path}`, {
    ...fetchOptions,
    headers
  });

  if (!response.ok) {
    const error = await readApiError(response);
    if (response.status === 401 && error.errorCode?.startsWith("AUTH_") && !suppressAuthenticationHandler && !authenticationErrorHandled && authenticationErrorHandler) {
      authenticationErrorHandled = true;
      authenticationErrorHandler(error);
    }
    throw error;
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();

  if (!text) {
    return undefined as T;
  }

  return JSON.parse(text) as T;
}

async function readApiError(response: Response): Promise<ApiError> {
  const text = await response.text();

  if (!text) {
    return new ApiError(response.statusText || `Request failed with status ${response.status}`, response.status);
  }

  try {
    const parsed = JSON.parse(text) as unknown;

    if (typeof parsed === "string") {
      return new ApiError(parsed, response.status);
    }

    if (parsed && typeof parsed === "object") {
      const payload = parsed as { errorCode?: unknown; message?: unknown; retryAfterSeconds?: unknown; title?: unknown };
      const message = payload.message ?? payload.title;
      const retryAfterSeconds = typeof payload.retryAfterSeconds === "number" && Number.isFinite(payload.retryAfterSeconds)
        ? Math.max(0, Math.ceil(payload.retryAfterSeconds))
        : undefined;
      const displayMessage = typeof message === "string" ? message : `Request failed with status ${response.status}`;
      return new ApiError(
        response.status === 429 && retryAfterSeconds
          ? `${displayMessage} 请在 ${retryAfterSeconds} 秒后重试。`
          : displayMessage,
        response.status,
        typeof payload.errorCode === "string" ? payload.errorCode : undefined,
        retryAfterSeconds
      );
    }
  }
  catch {
    return new ApiError(text, response.status);
  }

  return new ApiError(text, response.status);
}
