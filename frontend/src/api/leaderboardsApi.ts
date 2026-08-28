import { request } from "./httpClient";

export interface GlobalUserLeaderboard {
  entries: GlobalUserLeaderboardEntry[];
}

export interface GlobalUserLeaderboardEntry {
  rank: number;
  userId: string | null;
  userName: string;
  avatarUrl: string | null;
  completedChallengeCount: number;
  completedTaskCount: number;
  totalScore: number;
  lastCompletedAt: string | null;
  isCurrentUser: boolean;
  alias: string | null;
  isAnonymous: boolean;
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
  userId: string | null;
  userName: string;
  avatarUrl: string | null;
  completedTaskCount: number;
  totalScore: number;
  lastCompletedAt: string | null;
  alias: string | null;
  isAnonymous: boolean;
}

export interface RankHistory {
  days: RankHistoryDay[];
}

export interface RankHistoryDay {
  date: string;
  entries: RankHistoryEntry[];
}

export interface RankHistoryEntry {
  userId: string | null;
  userName: string;
  rank: number;
  totalScore: number;
  completedTaskCount: number;
  isCurrentUser: boolean;
  alias: string | null;
  isAnonymous: boolean;
}

export type LeaderboardSeasonStatus = 1 | 2 | 3 | 4 | 5;

export interface LeaderboardSeasonProblem {
  id: string;
  problemId: string;
  problemTitle: string;
  baseScore: number;
}

export interface LeaderboardSeason {
  id: string;
  name: string;
  startAt: string;
  freezeAt: string;
  publicUntil: string;
  status: LeaderboardSeasonStatus;
  effectiveStatus: LeaderboardSeasonStatus;
  isCurrent: boolean;
  problems: LeaderboardSeasonProblem[];
}

export interface SeasonLeaderboardEntry {
  rank: number;
  userId: string | null;
  userName: string | null;
  displayName: string;
  alias: string;
  isAnonymous: boolean;
  isCurrentUser: boolean;
  totalScore: number;
  baseScore: number;
  solvedCount: number;
  timeBonus: number;
  runtimeBonus: number;
  memoryBonus: number;
  lastScoreImprovedAt: string;
}

export interface SeasonLeaderboard {
  season: LeaderboardSeason | null;
  entries: SeasonLeaderboardEntry[];
}

export interface LeaderboardSeasonRequest {
  name: string;
  startAt: string;
  freezeAt: string;
  publicUntil: string;
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

export function getCurrentSeasonLeaderboard() {
  return request<SeasonLeaderboard>("/api/leaderboards/season/current");
}

export function getAdminLeaderboardSeasons() {
  return request<LeaderboardSeason[]>("/api/admin/leaderboard-seasons");
}

export function getCurrentSeasonAuditLeaderboard() {
  return request<SeasonLeaderboard>("/api/admin/leaderboard-seasons/current/leaderboard");
}

export function createLeaderboardSeason(payload: LeaderboardSeasonRequest) {
  return request<LeaderboardSeason>("/api/admin/leaderboard-seasons", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function updateLeaderboardSeason(seasonId: string, payload: LeaderboardSeasonRequest) {
  return request<LeaderboardSeason>(`/api/admin/leaderboard-seasons/${seasonId}`, {
    method: "PUT",
    body: JSON.stringify(payload)
  });
}

export function addLeaderboardSeasonProblem(seasonId: string, problemId: string) {
  return request<LeaderboardSeason>(`/api/admin/leaderboard-seasons/${seasonId}/problems`, {
    method: "POST",
    body: JSON.stringify({ problemId })
  });
}

export function removeLeaderboardSeasonProblem(seasonId: string, problemId: string) {
  return request<void>(`/api/admin/leaderboard-seasons/${seasonId}/problems/${problemId}`, { method: "DELETE" });
}

export function freezeLeaderboardSeason(seasonId: string) {
  return request<LeaderboardSeason>(`/api/admin/leaderboard-seasons/${seasonId}/freeze`, { method: "POST" });
}

export function finalizeLeaderboardSeason(seasonId: string) {
  return request(`/api/admin/leaderboard-seasons/${seasonId}/finalize`, { method: "POST" });
}

export function archiveLeaderboardSeason(seasonId: string) {
  return request<LeaderboardSeason>(`/api/admin/leaderboard-seasons/${seasonId}/archive`, { method: "POST" });
}
