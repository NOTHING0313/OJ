import type { JudgeLanguage, JudgeStatus } from "./submissionsApi";
import { request } from "./httpClient";

export interface ProfileSummary {
  user: ProfileUser;
  submissionSummary: SubmissionSummary;
  problemSummary: ProblemSummary;
  languageSummary: LanguageSummary[];
  challengeSummary: ChallengeProfileSummary;
  recentSubmissions: RecentSubmission[];
  recentChallengeCompletions: RecentChallengeCompletion[];
  recentFileReviews: RecentFileReview[];
}

export interface ProfileUser {
  id: string;
  userName: string;
  email: string;
  avatarUrl: string | null;
  role: number;
  isBlacklisted: boolean;
  createdAt: string;
}

export interface SubmissionSummary {
  totalSubmissionCount: number;
  acceptedSubmissionCount: number;
  wrongAnswerCount: number;
  compileErrorCount: number;
  runtimeErrorCount: number;
  systemErrorCount: number;
  acceptedRate: number;
  lastSubmittedAt: string | null;
}

export interface ProblemSummary {
  acceptedProblemCount: number;
  recentAcceptedProblems: AcceptedProblem[];
}

export interface AcceptedProblem {
  problemId: string;
  title: string;
  acceptedAt: string;
}

export interface LanguageSummary {
  language: JudgeLanguage;
  submissionCount: number;
  acceptedCount: number;
}

export interface ChallengeProfileSummary {
  participatedChallengeCount: number;
  completedTaskCount: number;
  totalScore: number;
  lastCompletedAt: string | null;
}

export interface RecentSubmission {
  id: string;
  problemId: string;
  problemTitle: string;
  submissionKind: number;
  language: JudgeLanguage | null;
  status: JudgeStatus;
  createdAt: string;
  finishedAt: string | null;
}

export interface RecentChallengeCompletion {
  challengeId: string;
  challengeTitle: string;
  taskId: string;
  taskTitle: string;
  score: number;
  completedAt: string;
}

export interface RecentFileReview {
  challengeId: string;
  challengeTitle: string;
  taskId: string;
  taskTitle: string;
  reviewScore: number | null;
  reviewComment: string | null;
  reviewedAt: string | null;
  submittedAt: string;
}

export function getMyProfile() {
  return request<ProfileSummary>("/api/profile/me");
}

export function getUserProfile(userId: string) {
  return request<ProfileSummary>(`/api/profile/users/${userId}`);
}
