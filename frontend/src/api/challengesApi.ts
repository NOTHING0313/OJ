import { baseUrl, request } from "./httpClient";

export type ChallengeTaskType = 1 | 2;
export type ChallengeTaskDifficulty = 1 | 2 | 3 | 4 | 5 | 6;

export interface ChallengeListItemDto {
  id: string;
  title: string;
  description: string;
  startAt: string;
  endAt: string;
  isPublished: boolean;
  createdAt: string;
  totalTaskCount: number;
  completedTaskCount: number;
  canManage: boolean;
}

export interface ChallengeTaskDto {
  id: string;
  challengeId: string;
  title: string;
  description: string;
  taskType: ChallengeTaskType;
  difficulty: ChallengeTaskDifficulty;
  boardX: number;
  boardY: number;
  algorithmProblemId: string | null;
  score: number;
  isPublished: boolean;
  createdAt: string;
  updatedAt: string;
  isCompleted: boolean;
  completedAt: string | null;
  completedScore: number | null;
}

export interface ChallengeDetailDto {
  id: string;
  title: string;
  description: string;
  startAt: string;
  endAt: string;
  createdByUserId: string;
  isPublished: boolean;
  createdAt: string;
  updatedAt: string;
  totalTaskCount: number;
  completedTaskCount: number;
  canManage: boolean;
  tasks: ChallengeTaskDto[];
}

export interface ChallengeTaskFileSubmissionDto {
  id: string;
  challengeId: string;
  challengeTaskId: string;
  userId: string;
  originalFileName: string;
  fileSizeBytes: number;
  createdAt: string;
  updatedAt: string;
  reviewScore: number | null;
  reviewComment: string | null;
  reviewedByUserId: string | null;
  reviewedByUserName: string | null;
  reviewedAt: string | null;
  isReviewed: boolean;
  canWithdrawSubmission: boolean;
}

export interface SaveChallengeRequest {
  title: string;
  description: string;
  startAt: string;
  endAt: string;
  isPublished: boolean;
}

export interface CreateChallengeTaskRequest {
  title: string;
  description: string;
  taskType: ChallengeTaskType;
  difficulty: ChallengeTaskDifficulty;
  boardX: number;
  boardY: number;
  algorithmProblemId: string | null;
  score: number;
  isPublished: boolean;
}

export interface UpdateChallengeTaskRequest {
  title: string;
  description: string;
  difficulty: ChallengeTaskDifficulty;
  boardX: number;
  boardY: number;
  algorithmProblemId: string | null;
  score: number;
  isPublished: boolean;
}

export interface ChallengeLeaderboardEntry {
  rank: number;
  userId: string;
  userName: string;
  avatarUrl: string | null;
  completedTaskCount: number;
  totalScore: number;
  lastCompletedAt: string | null;
  isCurrentUser: boolean;
}

export interface ChallengeLeaderboard {
  challengeId: string;
  challengeTitle: string;
  totalTaskCount: number;
  entries: ChallengeLeaderboardEntry[];
}

export interface ChallengeAdminSummary {
  challengeId: string;
  challengeTitle: string;
  totalTaskCount: number;
  participantCount: number;
  totalCompletionCount: number;
  users: ChallengeAdminUserProgress[];
  tasks: ChallengeAdminTaskProgress[];
}

export interface ChallengeAdminUserProgress {
  userId: string;
  userName: string;
  avatarUrl: string | null;
  completedTaskCount: number;
  totalScore: number;
  lastCompletedAt: string | null;
  taskStatuses: ChallengeAdminUserTaskStatus[];
}

export interface ChallengeAdminTaskProgress {
  taskId: string;
  title: string;
  taskType: ChallengeTaskType;
  difficulty: ChallengeTaskDifficulty;
  score: number;
  completedUserCount: number;
}

export interface ChallengeAdminUserTaskStatus {
  taskId: string;
  taskTitle: string;
  taskType: ChallengeTaskType;
  difficulty: ChallengeTaskDifficulty;
  score: number;
  isCompleted: boolean;
  completedAt: string | null;
  completedScore: number | null;
  submissionId: string | null;
  fileSubmissionId: string | null;
  originalFileName: string | null;
  fileSizeBytes: number | null;
  reviewScore: number | null;
  reviewComment: string | null;
  reviewedByUserId: string | null;
  reviewedByUserName: string | null;
  reviewedAt: string | null;
  isReviewed: boolean;
}

export function getChallenges() {
  return request<ChallengeListItemDto[]>("/api/challenges");
}

export function getChallenge(id: string) {
  return request<ChallengeDetailDto>(`/api/challenges/${id}`);
}

export function createChallenge(payload: SaveChallengeRequest) {
  return request<ChallengeDetailDto>("/api/challenges", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function updateChallenge(id: string, payload: SaveChallengeRequest) {
  return request<ChallengeDetailDto>(`/api/challenges/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload)
  });
}

export function deleteChallenge(id: string) {
  return request<void>(`/api/challenges/${id}`, {
    method: "DELETE"
  });
}

export function createChallengeTask(challengeId: string, payload: CreateChallengeTaskRequest) {
  return request<ChallengeTaskDto>(`/api/challenges/${challengeId}/tasks`, {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function updateChallengeTask(challengeId: string, taskId: string, payload: UpdateChallengeTaskRequest) {
  return request<ChallengeTaskDto>(`/api/challenges/${challengeId}/tasks/${taskId}`, {
    method: "PUT",
    body: JSON.stringify(payload)
  });
}

export function deleteChallengeTask(challengeId: string, taskId: string) {
  return request<void>(`/api/challenges/${challengeId}/tasks/${taskId}`, {
    method: "DELETE"
  });
}

export function getChallengeLeaderboard(challengeId: string) {
  return request<ChallengeLeaderboard>(`/api/challenges/${challengeId}/leaderboard`);
}

export function getChallengeAdminSummary(challengeId: string) {
  return request<ChallengeAdminSummary>(`/api/challenges/${challengeId}/admin-summary`);
}

export function joinChallenge(challengeId: string) {
  return request<void>(`/api/challenges/${challengeId}/join`, {
    method: "POST"
  });
}

export function getMyChallengeFileSubmission(challengeId: string, taskId: string) {
  return request<ChallengeTaskFileSubmissionDto | null>(`/api/challenges/${challengeId}/tasks/${taskId}/file-answer/me`);
}

export function withdrawMyChallengeFileSubmission(challengeId: string, taskId: string) {
  return request<void>(`/api/challenges/${challengeId}/tasks/${taskId}/file-answer/me`, {
    method: "DELETE"
  });
}

export function reviewChallengeFileSubmission(
  challengeId: string,
  fileSubmissionId: string,
  payload: { score: number; comment?: string }
) {
  return request<void>(`/api/challenges/${challengeId}/file-submissions/${fileSubmissionId}/review`, {
    method: "PUT",
    body: JSON.stringify(payload)
  });
}

export function downloadChallengeAdminUsersCsv(challengeId: string) {
  return downloadChallengeCsv(`/api/challenges/${challengeId}/admin-summary/export/users.csv`, "challenge-users.csv");
}

export function downloadChallengeAdminTasksCsv(challengeId: string) {
  return downloadChallengeCsv(`/api/challenges/${challengeId}/admin-summary/export/tasks.csv`, "challenge-tasks.csv");
}

export async function downloadChallengeFileSubmission(challengeId: string, fileSubmissionId: string, fallbackFileName?: string) {
  return downloadChallengeCsv(
    `/api/challenges/${challengeId}/file-submissions/${fileSubmissionId}/download`,
    fallbackFileName || "submission.zip"
  );
}

async function downloadChallengeCsv(path: string, fallbackFileName: string) {
  const token = localStorage.getItem("accessToken");
  const headers = new Headers();

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(`${baseUrl}${path}`, {
    headers
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || getDownloadErrorMessage(response.status));
  }

  const blob = await response.blob();
  const fileName = getDownloadFileName(response.headers.get("Content-Disposition"), fallbackFileName);
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export function submitChallengeTaskFileAnswer(challengeId: string, taskId: string, file: File) {
  const body = new FormData();
  body.append("file", file);

  return request<ChallengeTaskFileSubmissionDto>(`/api/challenges/${challengeId}/tasks/${taskId}/file-answer`, {
    method: "POST",
    body
  });
}

function getDownloadFileName(contentDisposition: string | null, fallbackFileName?: string) {
  if (!contentDisposition) {
    return fallbackFileName || "submission.zip";
  }

  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition);
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1].replace(/"/g, ""));
  }

  const fileNameMatch = /filename="?([^";]+)"?/i.exec(contentDisposition);
  if (fileNameMatch?.[1]) {
    return fileNameMatch[1];
  }

  return fallbackFileName || "submission.zip";
}

function getDownloadErrorMessage(status: number) {
  if (status === 401) {
    return "请先登录";
  }

  if (status === 403) {
    return "无权限导出或下载";
  }

  if (status === 404) {
    return "资源不存在";
  }

  return `下载失败：${status}`;
}
