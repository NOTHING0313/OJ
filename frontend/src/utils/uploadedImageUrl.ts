import { baseUrl } from "../api/httpClient";

export function normalizeUploadedImagePath(url: string | null | undefined) {
  if (!url) {
    return null;
  }

  const trimmed = url.trim();
  if (!trimmed) {
    return null;
  }

  if (trimmed.startsWith("/uploads/images/")) {
    return trimmed;
  }

  try {
    const parsed = new URL(trimmed);
    if (parsed.pathname.startsWith("/uploads/images/")) {
      return parsed.pathname;
    }
  } catch {
    // Keep the original value so backend validation can return a precise error.
  }

  return trimmed;
}

export function resolveSiteAssetUrl(url: string | null | undefined) {
  if (!url) {
    return undefined;
  }

  if (/^https?:\/\//i.test(url)) {
    return url;
  }

  if (url.startsWith("/")) {
    return `${baseUrl}${url}`;
  }

  return url;
}
