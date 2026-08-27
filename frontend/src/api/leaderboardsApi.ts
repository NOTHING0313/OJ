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

export interface RankHistory {
  days: RankHistoryDay[];
}

export interface RankHistoryDay {
  date: string;
  entries: RankHistoryEntry[];
}

export interface RankHistoryEntry {
  userId: string;
  userName: string;
  rank: number;
  totalScore: number;
  completedTaskCount: number;
  isCurrentUser: boolean;
}

export function getGlobalUserLeaderboard() {
  return request<GlobalUserLeaderboard>("/api/leaderboards/users");
}

export function getGlobalUserRankHistory(days = 10) {
  return request<RankHistory>(`/api/leaderboards/users/history?days=${days}`);
}

export function getChallengeLeaderboardIndex() {
  return request<ChallengeLeaderboardIndex>("/api/leaderboards/challenges");
}
