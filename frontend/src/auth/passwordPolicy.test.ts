import { describe, expect, it } from "vitest";
import { getPasswordLengthError } from "./passwordPolicy";

describe("passwordPolicy", () => {
  it("rejects seven Unicode characters and accepts eight", () => {
    expect(getPasswordLengthError("🙂".repeat(7))).toBe("密码至少需要 8 个字符");
    expect(getPasswordLengthError("🙂".repeat(8))).toBeNull();
  });

  it("counts canonically equivalent input after NFC normalization", () => {
    expect(getPasswordLengthError("Cafe\u0301abcd")).toBeNull();
  });
});
