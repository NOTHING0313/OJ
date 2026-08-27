import type { RankHistory, RankHistoryEntry } from "../api/leaderboardsApi";

interface CurrentRankEntry {
  userId: string;
  userName: string;
  rank: number;
  totalScore: number;
  completedTaskCount: number;
  isCurrentUser: boolean;
}

export function mergeCurrentRankHistory(history: RankHistory | null, entries: CurrentRankEntry[]) {
  if (!history || history.days.length === 0) {
    return history;
  }

  const days = [...history.days];
  const latestIndex = days.length - 1;
  days[latestIndex] = {
    ...days[latestIndex],
    entries: entries.map<RankHistoryEntry>((entry) => ({
      userId: entry.userId,
      userName: entry.userName,
      rank: entry.rank,
      totalScore: entry.totalScore,
      completedTaskCount: entry.completedTaskCount,
      isCurrentUser: entry.isCurrentUser
    }))
  };

  return { days };
}
