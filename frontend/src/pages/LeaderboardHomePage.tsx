import { useEffect, useState } from "react";
import { Navigate } from "react-router-dom";
import {
  getChallengeLeaderboardIndex,
  getCurrentSeasonLeaderboard,
  getCurrentSeasonPublicSummary,
  type ChallengeLeaderboardIndex,
  type LeaderboardSeasonPublicSummary,
  type SeasonLeaderboard
} from "../api/leaderboardsApi";
import { useAuth } from "../auth/AuthContext";
import { canManageContent } from "../auth/roles";
import { LeaderboardHomeView } from "../components/leaderboards/LeaderboardHomeView";

export function LeaderboardHomePage() {
  const [globalLeaderboard, setGlobalLeaderboard] = useState<SeasonLeaderboard | null>(null);
  const [challengeIndex, setChallengeIndex] = useState<ChallengeLeaderboardIndex | null>(null);
  const [summary, setSummary] = useState<LeaderboardSeasonPublicSummary | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let ignore = false;

    Promise.all([getCurrentSeasonLeaderboard(), getChallengeLeaderboardIndex(), getCurrentSeasonPublicSummary()])
      .then(([globalData, challengeData, summaryData]) => {
        if (!ignore) {
          setGlobalLeaderboard(globalData);
          setChallengeIndex(challengeData);
          setSummary(summaryData.season);
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "榜单概览加载失败");
        }
      })
      .finally(() => {
        if (!ignore) {
          setIsLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, []);

  const { currentUser } = useAuth();
  const boards = summary?.boards ?? [];

  if (!isLoading && boards.length === 0 && !canManageContent(currentUser?.role)) {
    return <Navigate to="/problems" replace />;
  }

  // Personal season records are suspended pending explicit product requirements.
  return <LeaderboardHomeView globalLeaderboard={globalLeaderboard} summary={summary} challenges={challengeIndex?.challenges ?? []} isLoading={isLoading} error={error} canManage={canManageContent(currentUser?.role)} showPersonalRecord={false} />;
}
