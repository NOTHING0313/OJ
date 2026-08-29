export const baseUrl = "";

const apiBusinessMessages: Record<string, string> = {
  "Slug already exists.": "Slug 已被使用。"
};

export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback;
  return apiBusinessMessages[error.message.trim()] ?? fallback;
}

export async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = localStorage.getItem("accessToken");
  const headers = new Headers(options.headers);

  if (!(options.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  if (token && !headers.has("Authorization")) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(`${baseUrl}${path}`, {
    ...options,
    headers
  });

  if (!response.ok) {
    const message = await readErrorMessage(response);
    throw new Error(message || `Request failed with status ${response.status}`);
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

async function readErrorMessage(response: Response): Promise<string> {
  const text = await response.text();

  if (!text) {
    return response.statusText;
  }

  try {
    const parsed = JSON.parse(text) as unknown;

    if (typeof parsed === "string") {
      return parsed;
    }

    if (parsed && typeof parsed === "object" && "title" in parsed) {
      return String((parsed as { title?: unknown }).title);
    }
  }
  catch {
    return text;
  }

  return text;
}
