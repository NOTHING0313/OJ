import { afterEach, describe, expect, it, vi } from "vitest";
import { exportTestCases } from "./problemsApi";
import { downloadChallengeAdminUsersCsv, downloadChallengeAdminTasksCsv, downloadChallengeFileSubmission } from "./challengesApi";
import { exportThemePreset } from "./siteSettingsApi";

const downloads = [
  ["test cases", () => exportTestCases("p")],
  ["challenge users", () => downloadChallengeAdminUsersCsv("c")],
  ["challenge tasks", () => downloadChallengeAdminTasksCsv("c")],
  ["submission ZIP", () => downloadChallengeFileSubmission("c", "s")],
  ["theme ZIP", () => exportThemePreset("t", "theme")]
] as const;
describe("every download entry uses shared error handling", () => {
  afterEach(() => vi.unstubAllGlobals());
  it.each(downloads)("%s handles Forbidden", async (_name, download) => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("Forbidden", { status: 403 })));
    await expect(download()).rejects.toMatchObject({ status: 403, message: "没有权限执行此操作，请返回或联系管理员。" });
  });
  it.each(downloads)("%s handles an offline connection", async (_name, download) => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    await expect(download()).rejects.toMatchObject({ errorCode: "NETWORK_ERROR" });
  });
});
