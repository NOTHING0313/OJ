import { baseUrl, request } from "./httpClient";

export type TestCaseVisibility = 1 | 2;
export type JudgeMode = 1 | 2;

export interface ProblemListItemDto {
  id: string;
  title: string;
  timeLimitMs: number;
  memoryLimitMb: number;
  isPublished: boolean;
  judgeMode: JudgeMode;
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
  id: string;
  title: string;
  description: string;
  inputDescription: string;
  outputDescription: string;
  timeLimitMs: number;
  memoryLimitMb: number;
  isPublished: boolean;
  judgeMode: JudgeMode;
  allowedLanguagesMask: number;
  totalScore: number;
  functionSpecJson?: string | null;
  starterCodeJson?: string | null;
  createdAt: string;
  updatedAt: string;
  testCases: TestCaseDto[];
}

export interface CreateProblemRequest {
  title: string;
  description: string;
  inputDescription: string;
  outputDescription: string;
  timeLimitMs: number;
  memoryLimitMb: number;
  isPublished: boolean;
  judgeMode: JudgeMode;
  allowedLanguagesMask: number;
  functionSpecJson?: string | null;
  starterCodeJson?: string | null;
}

export type UpdateProblemRequest = CreateProblemRequest;

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

export function deleteProblem(id: string) {
  return request<void>(`/api/problems/${id}`, {
    method: "DELETE"
  });
}

export function addTestCase(problemId: string, payload: CreateTestCaseRequest) {
  return request<TestCaseDto>(`/api/problems/${problemId}/test-cases`, {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export async function importTestCases(problemId: string, payload: ImportTestCasesRequest) {
  return request<ImportTestCasesResult>(`/api/problems/${problemId}/test-cases/import`, {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export async function exportTestCases(problemId: string): Promise<ExportedTestCasesFile> {
  const token = localStorage.getItem("accessToken");
  const headers = new Headers();

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(`${baseUrl}/api/problems/${problemId}/test-cases/export`, {
    method: "GET",
    headers
  });

  if (!response.ok) {
    throw new Error(await response.text() || `Request failed with status ${response.status}`);
  }

  const disposition = response.headers.get("Content-Disposition");
  return {
    blob: await response.blob(),
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
