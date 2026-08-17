import { request } from "./httpClient";

export type JudgeLanguage = 1 | 2 | 3;
export type JudgeStatus = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9;

export interface CreateSubmissionRequest {
  problemId: string;
  challengeTaskId?: string;
  language: JudgeLanguage;
  sourceCode: string;
}

export interface SubmissionCaseResultDto {
  id: string;
  submissionId: string;
  testCaseId: string;
  status: JudgeStatus;
  timeUsedMs: number | null;
  memoryUsedKb: number | null;
  actualOutput: string | null;
  expectedOutput: string | null;
  errorMessage: string | null;
  isHidden: boolean;
  isRedacted: boolean;
}

export interface SubmissionDto {
  id: string;
  problemId: string;
  problemTitle: string;
  userId: string;
  userName: string;
  challengeTaskId: string | null;
  language: JudgeLanguage;
  sourceCode: string;
  status: JudgeStatus;
  timeUsedMs: number | null;
  memoryUsedKb: number | null;
  errorMessage: string | null;
  createdAt: string;
  finishedAt: string | null;
  caseResults: SubmissionCaseResultDto[];
}

export interface SubmissionQueryItem {
  id: string;
  problemId: string;
  problemTitle: string;
  userId: string;
  userName: string;
  language: JudgeLanguage;
  status: JudgeStatus;
  timeUsedMs: number | null;
  memoryUsedKb: number | null;
  createdAt: string;
  finishedAt: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SubmissionQueryParams {
  mine?: boolean;
  userId?: string;
  problemId?: string;
  status?: JudgeStatus | "";
  language?: JudgeLanguage | "";
  problemKeyword?: string;
  userKeyword?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export function createSubmission(payload: CreateSubmissionRequest) {
  return request<SubmissionDto>("/api/submissions", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function getSubmission(id: string) {
  return request<SubmissionDto>(`/api/submissions/${id}`);
}

export function querySubmissions(params: SubmissionQueryParams = {}) {
  const search = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === "") {
      return;
    }

    search.set(key, String(value));
  });

  const queryString = search.toString();
  return request<PagedResult<SubmissionQueryItem>>(`/api/submissions${queryString ? `?${queryString}` : ""}`);
}
