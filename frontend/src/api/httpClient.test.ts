import { afterEach, describe, expect, it, vi } from "vitest";
import { apiFetch } from "./httpClient";

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
