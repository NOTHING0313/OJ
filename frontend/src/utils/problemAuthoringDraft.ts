import type { ChoiceQuestionWriteRequest, JudgeMode, ProblemKind, ProblemDifficulty } from "../api/problemsApi";

export interface AuthoringFields {
  difficulty: ProblemDifficulty;
  problemKind: ProblemKind;
  title: string; description: string; inputDescription: string; outputDescription: string;
  timeLimitMs: number; memoryLimitMb: number; isPublished: boolean; judgeMode: JudgeMode;
  choiceRevealPolicy: 1 | 2; choiceRevealAt: string; choiceQuestions: ChoiceQuestionWriteRequest[];
  isLanguageRestricted: boolean; allowedLanguagesMask: number;
  functionName: string; returnType: string;
  parameters: Array<{ name: string; type: string }>;
  customTypes: Array<{ name: string; fields: Array<{ name: string; type: string }> }>;
  cpp17StarterCode: string; c11StarterCode: string; csharpStarterCode: string;
}
export interface AuthoringDraft { schema: 1; version: number; fields: AuthoringFields; }

function record(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === "object" && !Array.isArray(value); }
function parameter(value: unknown) { return record(value) && typeof value.name === "string" && typeof value.type === "string"; }
export function parseAuthoringDraft(raw: string | null): AuthoringDraft | null {
  try {
    const value: unknown = JSON.parse(raw ?? "null");
    if (!record(value) || value.schema !== 1 || !Number.isInteger(value.version) || (value.version as number) < 0 || !record(value.fields)) return null;
    const f = value.fields;
    // Drafts written before difficulty grading remain recoverable.
    if (f.difficulty === undefined) f.difficulty = 0;
    if (![0, 1, 2, 3].includes(f.difficulty as number)) return null;
    if (!["title", "description", "inputDescription", "outputDescription", "choiceRevealAt", "functionName", "returnType", "cpp17StarterCode", "c11StarterCode", "csharpStarterCode"].every(key => typeof f[key] === "string")) return null;
    if (!["timeLimitMs", "memoryLimitMb", "allowedLanguagesMask"].every(key => typeof f[key] === "number" && Number.isFinite(f[key]))) return null;
    if (!["problemKind", "judgeMode", "choiceRevealPolicy"].every(key => f[key] === 1 || f[key] === 2)) return null;
    if (typeof f.isPublished !== "boolean" || typeof f.isLanguageRestricted !== "boolean") return null;
    if (!Array.isArray(f.parameters) || !f.parameters.every(parameter)) return null;
    if (!Array.isArray(f.customTypes) || !f.customTypes.every(v => record(v) && typeof v.name === "string" && Array.isArray(v.fields) && v.fields.every(parameter))) return null;
    if (!Array.isArray(f.choiceQuestions) || !f.choiceQuestions.every(q => record(q) && (q.id === undefined || typeof q.id === "string") && typeof q.stemMarkdown === "string" && typeof q.explanationMarkdown === "string" && (q.selectionMode === 1 || q.selectionMode === 2) && typeof q.score === "number" && Number.isFinite(q.score) && Array.isArray(q.options) && q.options.every(o => record(o) && (o.id === undefined || typeof o.id === "string") && typeof o.contentMarkdown === "string" && typeof o.isCorrect === "boolean"))) return null;
    return value as unknown as AuthoringDraft;
  } catch { return null; }
}
