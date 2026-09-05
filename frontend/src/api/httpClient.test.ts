import { afterEach, describe, expect, it, vi } from "vitest";
import { apiFetch, request, requestFile, ApiError, resetAuthenticationErrorGuard, setAuthenticationErrorHandler } from "./httpClient";

describe("apiFetch browser session protection", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("adds same-origin credentials and the CSRF token to unsafe cookie requests", async () => {
    vi.stubGlobal("document", { cookie: "__Host-OnlineJudge.Csrf=csrf%20token" });
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await apiFetch("/api/account/profile", { method: "PATCH" });

    const [, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(options.credentials).toBe("same-origin");
    expect(new Headers(options.headers).get("X-CSRF-TOKEN")).toBe("csrf token");
  });

  it("does not add CSRF to explicit bearer requests", async () => {
    vi.stubGlobal("document", { cookie: "__Host-OnlineJudge.Csrf=csrf-token" });
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await apiFetch("/api/account/profile", {
      method: "PATCH",
      headers: { Authorization: "Bearer explicit-token" }
    });

    const [, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(new Headers(options.headers).has("X-CSRF-TOKEN")).toBe(false);
  });
});

describe("API error presentation", () => {
  afterEach(() => vi.unstubAllGlobals());
  it("localizes plain English errors while retaining status and business details", async () => {
    vi.stubGlobal("document", { cookie: "" });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("Forbidden", { status: 403 })));
    await expect(request("/api/test")).rejects.toMatchObject({ status: 403, message: "没有权限执行此操作，请返回或联系管理员。" });
    expect(new ApiError("题目版本冲突，请刷新。", 409).message).toBe("题目版本冲突，请刷新。");
    expect(new ApiError("Internal Server Error", 500).message).toContain("服务暂时不可用");
  });
});


describe("file and network error paths", () => {
  afterEach(() => { vi.unstubAllGlobals(); setAuthenticationErrorHandler(null); resetAuthenticationErrorGuard(); });
  it.each([request, requestFile])("localizes fetch failure for JSON and files", async call => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    await expect(call("/api/test")).rejects.toMatchObject({ errorCode: "NETWORK_ERROR", message: "网络连接中断，请检查网络后重试。" });
  });
  it("keeps aborts distinct from network errors", async () => {
    const abort = new DOMException("The operation was aborted", "AbortError");
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(abort));
    await expect(requestFile("/api/test")).rejects.toBe(abort);
    const controller = new AbortController(); controller.abort();
    const reason = new Error("custom cancellation");
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(reason));
    await expect(request("/api/test", { signal: controller.signal })).rejects.toBe(reason);
  });
  it("handles a connection lost after headers for JSON and file bodies", async () => {
    for (const call of [request, requestFile]) {
      const stream = new ReadableStream({ start(controller) { controller.error(new TypeError("terminated")); } });
      vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(stream)));
      await expect(call("/api/test")).rejects.toMatchObject({ errorCode: "NETWORK_ERROR" });
    }
  });
  it("shares session expiry handling and supports suppression for downloads", async () => {
    const handler = vi.fn(); setAuthenticationErrorHandler(handler); resetAuthenticationErrorGuard();
    vi.stubGlobal("fetch", vi.fn().mockImplementation(() => Promise.resolve(new Response(JSON.stringify({ errorCode: "AUTH_SESSION_INVALID", message: "Unauthorized" }), { status: 401 }))));
    await expect(requestFile("/api/test", { suppressAuthenticationHandler: true })).rejects.toMatchObject({ status: 401 });
    expect(handler).not.toHaveBeenCalled();
    await expect(requestFile("/api/test")).rejects.toMatchObject({ status: 401 });
    await expect(request("/api/test")).rejects.toMatchObject({ status: 401 });
    expect(handler).toHaveBeenCalledTimes(1);
  });
  it("preserves rate limits without logging out and preserves file headers", async () => {
    const handler = vi.fn(); setAuthenticationErrorHandler(handler);
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({ message: "Too Many Requests", retryAfterSeconds: 7 }), { status: 429 })));
    await expect(requestFile("/api/test")).rejects.toMatchObject({ status: 429, retryAfterSeconds: 7, message: "操作过于频繁，请稍后重试。 请在 7 秒后重试。" });
    expect(handler).not.toHaveBeenCalled();
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("file data", { headers: { "Content-Disposition": "attachment; filename=test.json" } })));
    const file = await requestFile("/api/test");
    expect(await file.blob.text()).toBe("file data");
    expect(file.headers.get("Content-Disposition")).toContain("test.json");
  });
});


it("reports malformed successful JSON without exposing parser errors", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("<html>bad proxy</html>")));
  try { await expect(request("/api/test")).rejects.toMatchObject({ errorCode: "INVALID_RESPONSE", message: "服务器返回了无法识别的数据，请稍后重试。" }); }
  finally { vi.unstubAllGlobals(); }
});
