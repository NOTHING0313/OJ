import { requestFile, request } from "./httpClient";

export type TestCaseVisibility = 1 | 2;
export type JudgeMode = 1 | 2;
export type JudgeLanguage = 1 | 2 | 3;
export type ProblemKind = 1 | 2;
export type ProblemDifficulty = 0 | 1 | 2 | 3;
export type ChoiceSelectionMode = 1 | 2;
export type ChoiceAnswerRevealPolicy = 1 | 2;

export interface ChoiceOptionDto { id: string; order: number; contentMarkdown: string; }
export interface ChoiceQuestionDto {
  id: string;
  order: number;
  stemMarkdown: string;
  selectionMode: ChoiceSelectionMode;
  score: number;
  options: ChoiceOptionDto[];
  correctOptionIds?: string[];
  explanationMarkdown?: string;
}

export interface ChoiceQuestionWriteRequest {
  id?: string;
  stemMarkdown: string;
  selectionMode: ChoiceSelectionMode;
  score: number;
  explanationMarkdown: string;
  options: Array<{ id?: string; contentMarkdown: string; isCorrect: boolean }>;
}

export interface ProblemListItemDto {
  difficulty: ProblemDifficulty;
  id: string;
  title: string;
  problemKind: ProblemKind;
  timeLimitMs: number | null;
  memoryLimitMb: number | null;
  isPublished: boolean;
  judgeMode: JudgeMode | null;
  allowedLanguagesMask: number;
  totalScore: number;
  createdAt: string;
}

export interface TestCaseDto {
  id: string;
  problemId: string;
  input: string;
  expectedOutput: string;
  argumentsJson?: string | null;
  expectedJson?: string | null;
  visibility: TestCaseVisibility;
  score: number;
  createdAt: string;
}

export interface ProblemDetailDto {
  difficulty: ProblemDifficulty;
  id: string;
  problemKind: ProblemKind;
  authoringVersion: number;
  currentJudgeRevisionId: string | null;
  title: string;
  description: string;
  inputDescription: string;
  outputDescription: string;
  timeLimitMs: number | null;
  memoryLimitMb: number | null;
  isPublished: boolean;
  judgeMode: JudgeMode | null;
  allowedLanguagesMask: number;
  totalScore: number;
  functionSpecJson?: string | null;
  starterCodeJson?: string | null;
  choiceAnswerRevealPolicy?: ChoiceAnswerRevealPolicy | null;
  choiceAnswerRevealAt?: string | null;
  createdAt: string;
  updatedAt: string;
  testCases: TestCaseDto[];
  choiceQuestions: ChoiceQuestionDto[];
}

export interface ProblemJudgeAssetDto {
  id: string;
  language: JudgeLanguage;
  originalFileName: string;
  fileSizeBytes: number;
  sha256: string;
  createdAt: string;
}

export interface CreateProblemRequest {
  difficulty?: ProblemDifficulty;
  problemKind: ProblemKind;
  title: string;
  description: string;
  inputDescription: string;
  outputDescription: string;
  timeLimitMs: number | null;
  memoryLimitMb: number | null;
  isPublished: boolean;
  judgeMode: JudgeMode | null;
  allowedLanguagesMask: number;
  functionSpecJson?: string | null;
  starterCodeJson?: string | null;
  choiceAnswerRevealPolicy?: ChoiceAnswerRevealPolicy | null;
  choiceAnswerRevealAt?: string | null;
  choiceQuestions: ChoiceQuestionWriteRequest[];
}

export type UpdateProblemRequest = CreateProblemRequest & { expectedAuthoringVersion?: number };

export interface CreateTestCaseRequest {
  input: string;
  expectedOutput: string;
  argumentsJson?: string | null;
  expectedJson?: string | null;
  visibility: TestCaseVisibility;
  score: number;
}

export interface ImportTestCaseItem {
  input?: string | null;
  expectedOutput?: string | null;
  argumentsJson?: unknown;
  expectedJson?: unknown;
  score?: number | null;
  visibility?: TestCaseVisibility | "Sample" | "Hidden" | null;
}

export interface ImportTestCasesRequest {
  items: ImportTestCaseItem[];
}

export interface ImportTestCaseError {
  index: number;
  field: string;
  message: string;
}

export interface ImportTestCaseResultItem {
  id: string;
  score: number;
  visibility: TestCaseVisibility;
}

export interface ImportTestCasesResult {
  message: string;
  importedCount: number;
  items: ImportTestCaseResultItem[];
  errors: ImportTestCaseError[];
}

export interface ExportedTestCasesFile {
  blob: Blob;
  fileName: string;
}

export function getProblems() {
  return request<ProblemListItemDto[]>("/api/problems");
}

export function getProblem(id: string) {
  return request<ProblemDetailDto>(`/api/problems/${id}`);
}

export function getProblemAuthoring(id: string) {
  return request<ProblemDetailDto>(`/api/problems/${id}/authoring`);
}

export function createProblem(payload: CreateProblemRequest) {
  return request<ProblemDetailDto>("/api/problems", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function updateProblem(id: string, payload: UpdateProblemRequest) {
  return request<ProblemDetailDto>(`/api/problems/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload)
  });
}

export function updateProblemAuthoring(id: string, payload: UpdateProblemRequest) {
  return request<ProblemDetailDto>(`/api/problems/${id}/authoring`, {
    method: "PUT",
    body: JSON.stringify(payload)
  });
}

export function deleteProblem(id: string) {
  return request<void>(`/api/problems/${id}`, {
    method: "DELETE"
  });
}

export type UpdateTestCaseRequest = CreateTestCaseRequest;

export function getJudgeAssets(problemId: string) {
  return request<ProblemJudgeAssetDto[]>(`/api/problems/${problemId}/judge-assets`);
}

export function uploadJudgeAsset(problemId: string, language: JudgeLanguage, file: File) {
  const formData = new FormData();
  formData.append("language", String(language));
  formData.append("file", file);

  return request<ProblemJudgeAssetDto>(`/api/problems/${problemId}/judge-assets`, {
    method: "POST",
    body: formData
  });
}

export function deleteJudgeAsset(problemId: string, assetId: string) {
  return request<void>(`/api/problems/${problemId}/judge-assets/${assetId}`, {
    method: "DELETE"
  });
}

export function addTestCase(problemId: string, payload: CreateTestCaseRequest) {
  return request<TestCaseDto>(`/api/problems/${problemId}/test-cases`, {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function updateTestCase(problemId: string, testCaseId: string, payload: UpdateTestCaseRequest) {
  return request<TestCaseDto>(`/api/problems/${problemId}/test-cases/${testCaseId}`, {
    method: "PUT",
    body: JSON.stringify(payload)
  });
}

export function deleteTestCase(problemId: string, testCaseId: string) {
  return request<void>(`/api/problems/${problemId}/test-cases/${testCaseId}`, {
    method: "DELETE"
  });
}

export async function importTestCases(problemId: string, payload: ImportTestCasesRequest) {
  return request<ImportTestCasesResult>(`/api/problems/${problemId}/test-cases/import`, {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export async function exportTestCases(problemId: string): Promise<ExportedTestCasesFile> {
  const response = await requestFile(`/api/problems/${problemId}/test-cases/export`);

  const disposition = response.headers.get("Content-Disposition");
  return {
    blob: response.blob,
    fileName: getFileNameFromDisposition(disposition) || `problem-${problemId}-test-cases.json`
  };
}

function getFileNameFromDisposition(disposition: string | null) {
  if (!disposition) {
    return null;
  }

  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1]);
  }

  const asciiMatch = /filename="?([^";]+)"?/i.exec(disposition);
  return asciiMatch?.[1] || null;
}

export function queryProblems(keyword: string, page: number, signal?: AbortSignal) {
  const search = new URLSearchParams({ keyword, page: String(page), pageSize: "20" });
  return request<import("./submissionsApi").PagedResult<ProblemListItemDto>>(`/api/problems/query?${search}`, { signal });
}
