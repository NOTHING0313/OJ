import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  getChallengeLeaderboardHistory,
  getChallengeLeaderboardProgress,
  type ChallengeLeaderboardProgress
} from "../api/challengesApi";
import {
  getChallengeLeaderboardIndex,
  type ChallengeLeaderboardIndex,
  type ChallengeLeaderboardSummary,
  type RankHistory
} from "../api/leaderboardsApi";
import { ChallengeCompletionMatrix } from "../components/ChallengeCompletionMatrix";
import { RankHistoryChart } from "../components/RankHistoryChart";
import { useRankMovementAnimation } from "../components/useRankMovementAnimation";
import { mergeCurrentRankHistory } from "../utils/rankHistory";

const LIVE_REFRESH_MS = 10_000;

export function ChallengeLeaderboardIndexPage() {
  const [index, setIndex] = useState<ChallengeLeaderboardIndex | null>(null);
  const [selectedChallengeId, setSelectedChallengeId] = useState<string | null>(null);
  const [progress, setProgress] = useState<ChallengeLeaderboardProgress | null>(null);
  const [history, setHistory] = useState<RankHistory | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingDetails, setIsLoadingDetails] = useState(false);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);

  useEffect(() => {
    let ignore = false;

    getChallengeLeaderboardIndex()
      .then((data) => {
        if (!ignore) {
          setIndex(data);
          setSelectedChallengeId((current) =>
            current && data.challenges.some((challenge) => challenge.challengeId === current)
              ? current
              : data.challenges[0]?.challengeId ?? null
          );
          setLastUpdatedAt(new Date());
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "挑战榜单加载失败");
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

  useEffect(() => {
    if (!selectedChallengeId) {
      setProgress(null);
      setHistory(null);
      return;
    }

    let ignore = false;
    setIsLoadingDetails(true);

    Promise.all([
      getChallengeLeaderboardProgress(selectedChallengeId).catch(() => null),
      getChallengeLeaderboardHistory(selectedChallengeId, 10).catch(() => null)
    ])
      .then(([progressData, historyData]) => {
        if (!ignore) {
          setProgress(progressData);
          setHistory(historyData);
        }
      })
      .finally(() => {
        if (!ignore) {
          setIsLoadingDetails(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [selectedChallengeId]);

  const refresh = useCallback(async () => {
    if (document.visibilityState === "hidden") {
      return;
    }

    try {
      const data = await getChallengeLeaderboardIndex();
      setIndex(data);
      setSelectedChallengeId((current) =>
        current && data.challenges.some((challenge) => challenge.challengeId === current)
          ? current
          : data.challenges[0]?.challengeId ?? null
      );

      if (selectedChallengeId) {
        const progressData = await getChallengeLeaderboardProgress(selectedChallengeId).catch(() => null);
        if (progressData) {
          setProgress(progressData);
          setHistory((current) =>
            mergeCurrentRankHistory(
              current,
              progressData.users
                .filter((user): user is typeof user & { rank: number } => user.rank !== null)
                .map((user) => ({
                  userId: user.userId,
                  userName: user.userName,
                  rank: user.rank,
                  totalScore: user.totalScore,
                  completedTaskCount: user.completedTaskCount,
                  isCurrentUser: user.isCurrentUser,
                  alias: user.alias,
                  isAnonymous: user.isAnonymous
                }))
            )
          );
        }
      }

      setLastUpdatedAt(new Date());
    } catch {
      // 保留最后一次成功数据，避免短暂网络波动让榜单闪空。
    }
  }, [selectedChallengeId]);

  useEffect(() => {
    const timerId = window.setInterval(() => void refresh(), LIVE_REFRESH_MS);
    return () => window.clearInterval(timerId);
  }, [refresh]);

  if (isLoading) {
    return <div className="state-line">正在加载挑战榜单...</div>;
  }

  if (error) {
    return (
      <section className="page-section narrow">
        <div className="alert error">{error}</div>
        <Link className="button" to="/leaderboards">
          返回榜单中心
        </Link>
      </section>
    );
  }

  if (!index || index.challenges.length === 0) {
    return <div className="empty-state">暂无已发布挑战</div>;
  }

  const selectedChallenge = index.challenges.find((challenge) => challenge.challengeId === selectedChallengeId) ?? index.challenges[0];
  const chartEntries = progress?.users
    .filter((user): user is typeof user & { rank: number } => user.rank !== null)
    .map((user) => ({
      userId: user.userId,
      userName: user.userName,
      rank: user.rank,
      isCurrentUser: user.isCurrentUser
    })) ?? [];

  return (
    <section className="challenge-page leaderboard-page leaderboard-v2-page leaderboard-live-page">
      <div className="leaderboard-header leaderboard-v2-header">
        <div>
          <p className="eyebrow">CHALLENGE LEADERBOARDS</p>
          <h1>挑战榜单</h1>
          <p>浏览所有已发布挑战的实时领先者，并查看挑战内每位参与者的完成情况与近十天名次变化。</p>
        </div>
        <div className="leaderboard-header-actions">
          <span className="leaderboard-live-status">
            <i /> 实时更新 · 10 秒
            {lastUpdatedAt && <small>{formatUpdatedTime(lastUpdatedAt)}</small>}
          </span>
          <span className="leaderboard-total">共 {index.challenges.length} 个挑战</span>
          <Link className="button" to="/leaderboards">
            返回榜单中心
          </Link>
        </div>
      </div>

      <div className="leaderboard-challenge-list leaderboard-v2-challenge-list">
        {index.challenges.map((challenge) => (
          <ChallengeSummaryCard
            challenge={challenge}
            isSelected={challenge.challengeId === selectedChallenge.challengeId}
            onSelect={() => setSelectedChallengeId(challenge.challengeId)}
            key={challenge.challengeId}
          />
        ))}
      </div>

      <section className="challenge-leaderboard-detail-switcher">
        <div>
          <p className="eyebrow">CHALLENGE DETAILS</p>
          <h2>挑战参与与名次趋势</h2>
          <p>选择挑战后，下方同步展示所有参与者的任务完成矩阵与最近十天排名轨迹。</p>
        </div>
        <label>
          <span>当前挑战</span>
          <select value={selectedChallenge.challengeId} onChange={(event) => setSelectedChallengeId(event.target.value)}>
            {index.challenges.map((challenge) => (
              <option value={challenge.challengeId} key={challenge.challengeId}>
                {challenge.title}
              </option>
            ))}
          </select>
        </label>
      </section>

      {isLoadingDetails ? (
        <div className="leaderboard-detail-loading">正在加载 {selectedChallenge.title} 的参与者进度与历史排名...</div>
      ) : (
        <>
          <ChallengeCompletionMatrix progress={progress} />
          <RankHistoryChart
            history={history}
            currentEntries={chartEntries}
            title={`${selectedChallenge.title} · 近 10 天名次变化`}
            description="名次 1 位于图表顶部；今天的最后一个数据点会随实时榜单同步更新。"
          />
        </>
      )}
    </section>
  );
}

function ChallengeSummaryCard({
  challenge,
  isSelected,
  onSelect
}: {
  challenge: ChallengeLeaderboardSummary;
  isSelected: boolean;
  onSelect: () => void;
}) {
  const { setRowNode } = useRankMovementAnimation(challenge.topEntries.map(entryKey));

  return (
    <article className={`leaderboard-challenge-card leaderboard-v2-challenge-card ${isSelected ? "is-selected" : ""}`}>
      <div className="leaderboard-challenge-main">
        <div className="leaderboard-challenge-title-row">
          <span className="management-badge management-status-published">已发布</span>
          <span className="leaderboard-challenge-task-count">{challenge.totalTaskCount} 个任务</span>
          {isSelected && <span className="leaderboard-selected-badge">当前查看</span>}
        </div>
        <h2>{challenge.title}</h2>
        <p>{challenge.description ? challenge.description.slice(0, 160) : "暂无简介"}</p>

        <div className="leaderboard-challenge-stats">
          <div>
            <span>{challenge.participationMode === 2 ? "参赛战队" : "参与人数"}</span>
            <strong>{challenge.participantCount}</strong>
          </div>
          <div>
            <span>{challenge.participationMode === 2 ? "完成战队" : "完成人数"}</span>
            <strong>{challenge.completedUserCount}</strong>
          </div>
          <div>
            <span>完成率</span>
            <strong>{formatPercent(challenge.completedUserCount, challenge.participantCount)}</strong>
          </div>
        </div>

        <div className="challenge-time leaderboard-v2-time-row">
          <span>开始：{formatDate(challenge.startAt)}</span>
          <span>截止：{formatDate(challenge.endAt)}</span>
        </div>

        <button className="button leaderboard-select-challenge-button" type="button" onClick={onSelect} disabled={isSelected}>
          {isSelected ? "正在查看此挑战" : "查看参与者进度"}
        </button>
      </div>

      <aside className="leaderboard-top-panel leaderboard-v2-top-panel">
        <div className="leaderboard-v2-top-header">
          <div>
            <p className="eyebrow">TOP 3</p>
            <strong>领先用户</strong>
          </div>
          <Link className="admin-user-view-link" to={`/challenges/${challenge.challengeId}/leaderboard`}>
            完整榜单
          </Link>
        </div>

        {challenge.topEntries.length === 0 ? (
          <div className="leaderboard-preview-empty leaderboard-preview-empty-compact">暂无完成记录</div>
        ) : (
          <ol>
            {challenge.topEntries.map((entry) => (
              <li key={entryKey(entry)} ref={(node) => setRowNode(entryKey(entry), node)}>
                <span className={`leaderboard-rank ${getRankClass(entry.rank)}`}>{entry.rank}</span>
                <div>
                  <strong>{entry.userName}</strong>
                  <span>
                    {entry.totalScore} 分 · {entry.completedTaskCount} 题
                  </span>
                </div>
              </li>
            ))}
          </ol>
        )}
      </aside>
    </article>
  );
}

function entryKey(entry: { userId: string | null; userName: string }) {
  return entry.userId ?? `anonymous:${entry.userName}`;
}

function getRankClass(rank: number) {
  if (rank === 1) return "top-one";
  if (rank === 2) return "top-two";
  if (rank === 3) return "top-three";
  return "";
}

function formatDate(value: string | null) {
  if (!value) return "—";

  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}

function formatPercent(completed: number, total: number) {
  if (total <= 0) return "0%";
  return `${Math.round((completed / total) * 100)}%`;
}

function formatUpdatedTime(value: Date) {
  return new Intl.DateTimeFormat("zh-CN", { hour: "2-digit", minute: "2-digit", second: "2-digit" }).format(value);
}
