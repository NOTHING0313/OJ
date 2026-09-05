export const baseUrl = "";
const csrfCookieName = "__Host-OnlineJudge.Csrf";
const csrfHeaderName = "X-CSRF-TOKEN";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly errorCode?: string,
    public readonly retryAfterSeconds?: number
  ) {
    super(localizeApiError(message, status));
    this.name = "ApiError";
  }
}

function localizeApiError(message: string, status: number): string {
  if (/[\u3400-\u9fff]/.test(message)) return message;
  const messages: Record<number, string> = {
    400: "请求内容有误，请检查填写内容后重试。",
    401: "登录已失效，请重新登录。",
    403: "没有权限执行此操作，请返回或联系管理员。",
    404: "内容不存在或已被删除，请返回列表刷新。",
    409: "内容已发生变化，请刷新后重试。",
    413: "提交内容过大，请缩小文件或内容后重试。",
    429: "操作过于频繁，请稍后重试。"
  };
  if (message.trim() === "Slug already exists.") return "Slug 已被使用。";
  return messages[status] ?? (status >= 500 ? "服务暂时不可用，请稍后重试。" : "请求失败，请稍后重试。");
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
  if (error instanceof ApiError) return error.message;
  return apiBusinessMessages[error.message.trim()] ?? fallback;
}

export function setAuthenticationErrorHandler(handler: AuthenticationErrorHandler | null) {
  authenticationErrorHandler = handler;
}

export function resetAuthenticationErrorGuard() {
  authenticationErrorHandled = false;
}

export async function request<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  const headers = new Headers(options.headers);
  const { suppressAuthenticationHandler = false, ...fetchOptions } = options;

  if (!(options.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await requestResponse(path, { ...fetchOptions, headers, suppressAuthenticationHandler });

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await readBody(() => response.text(), options.signal);

  if (!text) {
    return undefined as T;
  }

  try { return JSON.parse(text) as T; }
  catch { throw new ApiError("服务器返回了无法识别的数据，请稍后重试。", response.status, "INVALID_RESPONSE"); }
}

async function requestResponse(path: string, options: ApiRequestOptions): Promise<Response> {
  const { suppressAuthenticationHandler = false, ...fetchOptions } = options;
  const response = await apiFetch(`${baseUrl}${path}`, fetchOptions);
  if (!response.ok) {
    const error = await readApiError(response, options.signal);
    if (response.status === 401 && error.errorCode?.startsWith("AUTH_") && !suppressAuthenticationHandler && !authenticationErrorHandled && authenticationErrorHandler) {
      authenticationErrorHandled = true;
      authenticationErrorHandler(error);
    }
    throw error;
  }
  return response;
}

export async function requestFile(path: string, options: ApiRequestOptions = {}): Promise<{ blob: Blob; headers: Headers }> {
  const response = await requestResponse(path, options);
  const blob = await readBody(() => response.blob(), options.signal);
  return { blob, headers: response.headers };
}

async function readBody<T>(read: () => Promise<T>, signal?: AbortSignal | null): Promise<T> {
  try { return await read(); }
  catch (error) {
    if (signal?.aborted || (error instanceof DOMException && error.name === "AbortError")) throw error;
    throw new ApiError("网络连接中断，请检查网络后重试。", 0, "NETWORK_ERROR");
  }
}

export function apiFetch(input: RequestInfo | URL, options: RequestInit = {}) {
  const headers = new Headers(options.headers);
  const method = (options.method ?? "GET").toUpperCase();
  const csrfToken = readCookie(csrfCookieName);

  if (!headers.has("Authorization") && isUnsafeMethod(method) && csrfToken) {
    headers.set(csrfHeaderName, csrfToken);
  }

  return readBody(() => fetch(input, {
    ...options,
    credentials: "same-origin",
    headers
  }), options.signal);
}

function isUnsafeMethod(method: string) {
  return !["GET", "HEAD", "OPTIONS", "TRACE"].includes(method);
}

function readCookie(name: string): string | null {
  if (typeof document === "undefined") return null;
  const prefix = `${encodeURIComponent(name)}=`;
  const item = document.cookie.split(";").map((value) => value.trim()).find((value) => value.startsWith(prefix));
  return item ? decodeURIComponent(item.slice(prefix.length)) : null;
}

async function readApiError(response: Response, signal?: AbortSignal | null): Promise<ApiError> {
  const text = await readBody(() => response.text(), signal);

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
      const displayMessage = localizeApiError(typeof message === "string" ? message : response.statusText, response.status);
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
