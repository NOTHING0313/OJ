import { request } from "./httpClient";

export interface GlobalUserLeaderboard {
  entries: GlobalUserLeaderboardEntry[];
}

export interface GlobalUserLeaderboardEntry {
  rank: number;
  userId: string;
  userName: string;
  avatarUrl: string | null;
  completedChallengeCount: number;
  completedTaskCount: number;
  totalScore: number;
  lastCompletedAt: string | null;
  isCurrentUser: boolean;
}

export interface ChallengeLeaderboardIndex {
  challenges: ChallengeLeaderboardSummary[];
}

export interface ChallengeLeaderboardSummary {
  challengeId: string;
  title: string;
  description: string | null;
  totalTaskCount: number;
  participantCount: number;
  completedUserCount: number;
  startAt: string;
  endAt: string;
  isPublished: boolean;
  topEntries: ChallengeLeaderboardTopEntry[];
}

export interface ChallengeLeaderboardTopEntry {
  rank: number;
  userId: string;
  userName: string;
  avatarUrl: string | null;
  completedTaskCount: number;
  totalScore: number;
  lastCompletedAt: string | null;
}

export function getGlobalUserLeaderboard() {
  return request<GlobalUserLeaderboard>("/api/leaderboards/users");
}

export function getChallengeLeaderboardIndex() {
  return request<ChallengeLeaderboardIndex>("/api/leaderboards/challenges");
}
