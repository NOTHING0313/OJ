import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { problemDraftKey, readChoiceDraft, readDraft, writeDraft } from "./problemDrafts";

function storage() {
  const values = new Map<string, string>();
  return { getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => values.set(key, value), removeItem: (key: string) => values.delete(key) };
}
beforeEach(() => { vi.stubGlobal("localStorage", storage()); vi.stubGlobal("sessionStorage", storage()); });
afterEach(() => vi.unstubAllGlobals());

describe("problem drafts", () => {
  it("isolates users, languages, problems, and choice revisions", () => {
    const key = problemDraftKey("alice", "source", "p1", "cpp");
    writeDraft(key, "alice code");
    expect(readDraft(key)).toBe("alice code");
    for (const other of [problemDraftKey("bob", "source", "p1", "cpp"), problemDraftKey("alice", "source", "p2", "cpp"), problemDraftKey("alice", "source", "p1", "csharp")]) expect(readDraft(other)).toBeNull();
    writeDraft(problemDraftKey("alice", "choice", "p1", "revision1"), '{"q1":["a"]}');
    expect(readChoiceDraft(problemDraftKey("alice", "choice", "p1", "revision2"))).toEqual({});
  });
  it("restores choice selections and clears them for another practice", () => {
    const key = problemDraftKey("alice", "choice", "p1", "r1");
    writeDraft(key, JSON.stringify({ q1: ["a", "b"] }));
    expect(readChoiceDraft(key)).toEqual({ q1: ["a", "b"] });
    writeDraft(key, null);
    expect(readChoiceDraft(key)).toEqual({});
  });
  it("rejects anonymous drafts and never adopts unowned legacy code", () => {
    const key = problemDraftKey(undefined, "choice", "p1", "r1");
    expect(writeDraft(key, '{"q1":["a"]}')).toBe(false);
    expect(readDraft(key)).toBeNull();
    expect(sessionStorage.getItem(key)).toBeNull();
    expect(localStorage.getItem(key)).toBeNull();
    localStorage.setItem("oj:source:p1:1:standalone:none", "legacy");
    expect(readDraft(problemDraftKey("alice", "source", "p1", "1", "standalone", "none"))).toBeNull();
  });
  it("survives corrupted or unavailable storage", () => {
    const key = problemDraftKey("alice", "choice");
    writeDraft(key, "invalid json");
    expect(readChoiceDraft(key)).toEqual({});
    writeDraft(key, '{"q1":3,"q2":["a"]}');
    expect(readChoiceDraft(key)).toEqual({ q2: ["a"] });
    vi.stubGlobal("localStorage", { getItem() { throw new Error(); }, setItem() { throw new Error(); } });
    expect(readDraft(key)).toBeNull();
    expect(writeDraft(key, "code")).toBe(false);
  });
});
