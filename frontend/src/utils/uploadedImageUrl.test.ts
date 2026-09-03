import { describe, expect, it } from "vitest";
import { normalizeUploadedImagePath, resolveSiteAssetUrl } from "./uploadedImageUrl";

describe("uploaded image URL helpers", () => {
  it("normalizes uploaded image URLs to their persisted path", () => {
    expect(normalizeUploadedImagePath(" https://oj.example/uploads/images/avatar.png?cache=1 ")).toBe("/uploads/images/avatar.png");
    expect(normalizeUploadedImagePath("/uploads/images/avatar.png")).toBe("/uploads/images/avatar.png");
  });

  it("preserves non-upload values for backend validation", () => {
    expect(normalizeUploadedImagePath("not a url")).toBe("not a url");
    expect(normalizeUploadedImagePath("   ")).toBeNull();
  });

  it("leaves absolute asset URLs unchanged", () => {
    expect(resolveSiteAssetUrl("https://cdn.example/banner.png")).toBe("https://cdn.example/banner.png");
    expect(resolveSiteAssetUrl(undefined)).toBeUndefined();
  });
});
