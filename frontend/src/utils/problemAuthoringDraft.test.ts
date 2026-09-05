import { describe, expect, it } from "vitest";
import { parseAuthoringDraft, type AuthoringDraft } from "./problemAuthoringDraft";
const draft: AuthoringDraft = { schema: 1, version: 4, fields: {
  difficulty: 3, problemKind: 2, title: "draft", description: "body", inputDescription: "", outputDescription: "", timeLimitMs: 1000, memoryLimitMb: 128,
  isPublished: false, judgeMode: 1, choiceRevealPolicy: 2, choiceRevealAt: "2026-09-06T10:00", isLanguageRestricted: false, allowedLanguagesMask: 7,
  functionName: "solve", returnType: "int", parameters: [{ name: "n", type: "int" }], customTypes: [{ name: "Point", fields: [{ name: "x", type: "int" }] }],
  cpp17StarterCode: "cpp", c11StarterCode: "c", csharpStarterCode: "csharp",
  choiceQuestions: [{ stemMarkdown: "q", explanationMarkdown: "explain", selectionMode: 1, score: 5, options: [{ contentMarkdown: "a", isCorrect: true }] }]
} };
describe("authoring draft recovery", () => {
  it("round trips all editor fields and preserves the server base version", () => { expect(parseAuthoringDraft(JSON.stringify(draft))).toEqual(draft); });
  it("rejects partial, unsupported, and malformed nested drafts", () => {
    expect(parseAuthoringDraft("bad")).toBeNull();
    expect(parseAuthoringDraft(JSON.stringify({ ...draft, schema: 2 }))).toBeNull();
    expect(parseAuthoringDraft(JSON.stringify({ ...draft, fields: { title: "partial" } }))).toBeNull();
    expect(parseAuthoringDraft(JSON.stringify({ ...draft, fields: { ...draft.fields, choiceQuestions: [{ ...draft.fields.choiceQuestions[0], options: [null] }] } }))).toBeNull();
  });
});

it("restores legacy ungraded drafts and rejects invalid difficulty", () => {
  const fields: Partial<AuthoringDraft["fields"]> = { ...draft.fields }; delete fields.difficulty;
  expect(parseAuthoringDraft(JSON.stringify({ ...draft, fields }))?.fields.difficulty).toBe(0);
  expect(parseAuthoringDraft(JSON.stringify({ ...draft, fields: { ...draft.fields, difficulty: 4 } }))).toBeNull();
});
