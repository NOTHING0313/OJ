export function problemDraftKey(userId: string | undefined, ...parts: string[]) {
  if (!userId) return "";
  return ["oj", "draft-v2", userId, ...parts].map(encodeURIComponent).join(":");
}

// Only authenticated accounts own drafts; legacy guest data is never read or adopted.
export function readDraft(key: string): string | null {
  if (!key) return null;
  try { return localStorage.getItem(key); }
  catch { return null; }
}

export function writeDraft(key: string, value: string | null): boolean {
  if (!key) return false;
  try {
    const storage = localStorage;
    if (value === null) storage.removeItem(key); else storage.setItem(key, value);
    return true;
  } catch { return false; }
}

export function readChoiceDraft(key: string): Record<string, string[]> {
  try {
    const value: unknown = JSON.parse(readDraft(key) ?? "{}");
    if (!value || typeof value !== "object" || Array.isArray(value)) return {};
    return Object.fromEntries(Object.entries(value).filter(([, ids]) => Array.isArray(ids) && ids.every(id => typeof id === "string")));
  } catch { return {}; }
}
