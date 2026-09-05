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
  teamCount: number;
  participationMode: 1 | 2;
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
export type LeaderboardJudgeLanguage = 1 | 2 | 3;
export type LeaderboardSeasonBoardType = 1 | 2;

export interface LeaderboardSeasonBoard {
  id?: string;
  boardType: LeaderboardSeasonBoardType;
  challengeId: string | null;
  challengeTitle: string | null;
}

export interface LeaderboardSeasonPublicSummaryResponse {
  season: LeaderboardSeasonPublicSummary | null;
}

export interface LeaderboardSeasonPublicSummary {
  name: string;
  status: LeaderboardSeasonStatus;
  startAt: string;
  freezeAt: string;
  publicUntil: string;
  boards: LeaderboardSeasonBoard[];
}

export interface LeaderboardPerformanceBonusTier {
  maxRatioPercentage: number;
  bonusPercentage: number;
}

export interface LeaderboardScoringRules {
  firstCompletionBonusEnabled: boolean;
  runtimeBonusEnabled: boolean;
  memoryBonusEnabled: boolean;
  timeBonusPercentages: number[];
  runtimeBonusTiers: LeaderboardPerformanceBonusTier[];
  memoryBonusTiers: LeaderboardPerformanceBonusTier[];
}

export interface LeaderboardSeasonProblemBenchmark {
  language: LeaderboardJudgeLanguage;
  runtimeBaselineMs: number;
  memoryBaselineKb: number;
}

export interface LeaderboardSeasonProblem {
  id: string;
  problemId: string;
  problemTitle: string;
  problemKind: number;
  baseScore: number;
  allowedLanguagesMask: number;
  benchmarks: LeaderboardSeasonProblemBenchmark[];
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
  activatedAt: string | null;
  frozenAt: string | null;
  finalizedAt: string | null;
  archivedAt: string | null;
  manuallyFrozenAt: string | null;
  scoringRules: LeaderboardScoringRules;
  boards: LeaderboardSeasonBoard[];
  problems: LeaderboardSeasonProblem[];
}

export interface LeaderboardSeasonHistorySummary {
  seasonId: string;
  name: string;
  startAt: string;
  freezeAt: string;
  publicUntil: string;
  archivedAt: string | null;
  participantCount: number;
  top3: { rank: number; displayName: string; finalScore: number }[];
}

export interface LeaderboardSeasonArchiveProblemScore {
  problemId: string;
  problemTitleSnapshot: string;
  baseScore: number;
  earnedBaseScore: number;
  timeRank: number | null;
  firstFullScoreAt: string;
  timeBonus: number;
  runtimeMs: number | null;
  runtimeBonus: number;
  memoryKb: number | null;
  memoryBonus: number;
  finalProblemScore: number;
}

export interface LeaderboardSeasonArchive {
  seasonId: string;
  seasonName: string;
  entries: {
    userId: string | null;
    alias: string;
    displayNameSnapshot: string;
    wasAnonymous: boolean;
    finalRank: number;
    finalScore: number;
    finalBaseScore: number;
    finalTimeBonus: number;
    finalRuntimeBonus: number;
    finalMemoryBonus: number;
    solvedCount: number;
    problemScores: LeaderboardSeasonArchiveProblemScore[];
  }[];
}

export interface LeaderboardSeasonPersonal {
  season: LeaderboardSeason | null;
  currentRank: number | null;
  totalParticipants: number;
  totalScore: number;
  totalBaseScore: number;
  totalTimeBonus: number;
  totalRuntimeBonus: number;
  totalMemoryBonus: number;
  solvedCount: number;
  seasonProblemCount: number;
  top10ProblemCount: number;
  firstPlaceProblemCount: number;
  bestRank: number | null;
  rankChange: number | null;
  problems: { problemId: string; title: string; score: number; timeRank: number | null; timeBonus: number; performanceBonus: number }[];
  rankHistory: { recordedAt: string; rank: number; totalScore: number }[];
}

export interface LeaderboardSeasonPersonalHistory {
  seasonId: string;
  seasonName: string;
  finalRank: number;
  finalScore: number;
  solvedCount: number;
  timeBonus: number;
  performanceBonus: number;
  problems: LeaderboardSeasonArchiveProblemScore[];
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

export interface SeasonProblemLeaderboardEntry {
  rank: number;
  userId: string | null;
  userName: string | null;
  displayName: string;
  alias: string;
  isAnonymous: boolean;
  isCurrentUser: boolean;
  baseScore: number;
  earnedBaseScore: number;
  timeRank: number | null;
  timeBonus: number;
  performanceLanguage: LeaderboardJudgeLanguage | null;
  runtimeMs: number | null;
  runtimeBaselineMs: number | null;
  runtimeBonus: number;
  memoryKb: number | null;
  memoryBaselineKb: number | null;
  memoryBonus: number;
  performanceBonus: number;
  totalProblemScore: number;
  firstFullScoreAt: string;
}

export interface SeasonProblemLeaderboard {
  season: LeaderboardSeason | null;
  problem: LeaderboardSeasonProblem | null;
  entries: SeasonProblemLeaderboardEntry[];
}

export interface LeaderboardSeasonRequest {
  name: string;
  startAt: string;
  freezeAt: string;
  publicUntil: string;
  includeGlobalBoard: boolean;
  challengeIds: string[];
  firstCompletionBonusEnabled: boolean;
  runtimeBonusEnabled: boolean;
  memoryBonusEnabled: boolean;
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

export function getCurrentSeasonPublicSummary() {
  return request<LeaderboardSeasonPublicSummaryResponse>("/api/leaderboard-seasons/current/summary");
}

export function getCurrentSeasonProblemLeaderboard(problemId: string) {
  return request<SeasonProblemLeaderboard>(`/api/leaderboards/season/current/problems/${problemId}`);
}

export function getLeaderboardSeasonHistory() {
  return request<LeaderboardSeasonHistorySummary[]>("/api/leaderboard-seasons/history");
}

export function getLeaderboardSeasonHistoryDetail(seasonId: string) {
  return request<LeaderboardSeasonArchive>(`/api/leaderboard-seasons/history/${seasonId}`);
}

export function getCurrentSeasonPersonal() {
  return request<LeaderboardSeasonPersonal>("/api/leaderboard-seasons/current/me");
}

export function getSeasonPersonalHistory() {
  return request<LeaderboardSeasonPersonalHistory[]>("/api/leaderboard-seasons/me/history");
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

export function addLeaderboardSeasonProblems(seasonId: string, problemIds: string[]) {
  return request<LeaderboardSeason>(`/api/admin/leaderboard-seasons/${seasonId}/problems/batch`, {
    method: "POST",
    body: JSON.stringify({ problemIds })
  });
}

export function removeLeaderboardSeasonProblems(seasonId: string, problemIds: string[]) {
  return request<void>(`/api/admin/leaderboard-seasons/${seasonId}/problems/batch-remove`, {
    method: "POST",
    body: JSON.stringify({ problemIds })
  });
}

export function removeLeaderboardSeasonProblem(seasonId: string, problemId: string) {
  return request<void>(`/api/admin/leaderboard-seasons/${seasonId}/problems/${problemId}`, { method: "DELETE" });
}

export function updateLeaderboardSeasonProblemBenchmark(
  seasonId: string,
  problemId: string,
  language: LeaderboardJudgeLanguage,
  runtimeBaselineMs: number | null,
  memoryBaselineKb: number | null
) {
  return request<LeaderboardSeason>(`/api/admin/leaderboard-seasons/${seasonId}/problems/${problemId}/benchmarks/${language}`, {
    method: "PUT",
    body: JSON.stringify({ runtimeBaselineMs, memoryBaselineKb })
  });
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
