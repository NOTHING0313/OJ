import { useEffect, useState } from "react";
import {
  getChallengeLeaderboardIndex,
  getCurrentSeasonLeaderboard,
  getCurrentSeasonPublicSummary,
  type ChallengeLeaderboardIndex,
  type LeaderboardSeasonPublicSummary,
  type SeasonLeaderboard
} from "../api/leaderboardsApi";
import { useAuth } from "../auth/AuthContext";
import { LeaderboardHomeView } from "../components/leaderboards/LeaderboardHomeView";

export function LeaderboardHomePage() {
  const [globalLeaderboard, setGlobalLeaderboard] = useState<SeasonLeaderboard | null>(null);
  const [challengeIndex, setChallengeIndex] = useState<ChallengeLeaderboardIndex | null>(null);
  const [summary, setSummary] = useState<LeaderboardSeasonPublicSummary | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const { currentUser } = useAuth();
  const isRoot = currentUser?.role === 3;

  useEffect(() => {
    let ignore = false;

    Promise.all([isRoot ? getCurrentSeasonLeaderboard() : Promise.resolve(null), getChallengeLeaderboardIndex(), getCurrentSeasonPublicSummary()])
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
  }, [isRoot]);

  return <LeaderboardHomeView globalLeaderboard={globalLeaderboard} summary={summary} challenges={challengeIndex?.challenges ?? []} isLoading={isLoading} error={error} canManage={isRoot} showPersonalRecord={currentUser?.role === 1} />;
}
