import type { CSSProperties } from "react";
import type { RankHistory } from "../api/leaderboardsApi";

interface CurrentRankEntry {
  userId: string | null;
  userName: string;
  rank: number;
  isCurrentUser: boolean;
}

interface RankHistoryChartProps {
  history: RankHistory | null;
  currentEntries: CurrentRankEntry[];
  title?: string;
  description?: string;
}

interface SeriesPoint {
  x: number;
  y: number;
  rank: number;
  date: string;
}

interface RankSeries {
  userId: string;
  userName: string;
  isCurrentUser: boolean;
  points: SeriesPoint[];
}

const WIDTH = 920;
const HEIGHT = 310;
const PADDING = { top: 28, right: 24, bottom: 48, left: 54 };
const MAX_SERIES = 5;

export function RankHistoryChart({
  history,
  currentEntries,
  title = "近 10 天名次变化",
  description = "按每日结束时的排名绘制，今天的数据会随实时榜单更新。"
}: RankHistoryChartProps) {
  if (!history || history.days.length === 0 || currentEntries.length === 0) {
    return (
      <section className="rank-history-card">
        <div className="rank-history-header">
          <div>
            <p className="eyebrow">RANK HISTORY</p>
            <h2>{title}</h2>
            <p>{description}</p>
          </div>
          <span className="leaderboard-live-badge">近 10 天</span>
        </div>
        <div className="rank-history-empty">暂无足够的排名历史数据。</div>
      </section>
    );
  }

  const selectedEntries = selectSeries(currentEntries);
  const selectedIds = new Set(selectedEntries.map(identityKey));
  const maxRank = Math.max(
    1,
    ...history.days.flatMap((day) => day.entries.filter((entry) => selectedIds.has(identityKey(entry))).map((entry) => entry.rank))
  );
  const displayMaxRank = Math.max(3, maxRank);
  const plotWidth = WIDTH - PADDING.left - PADDING.right;
  const plotHeight = HEIGHT - PADDING.top - PADDING.bottom;
  const xForIndex = (index: number) =>
    PADDING.left + (history.days.length <= 1 ? plotWidth / 2 : (index / (history.days.length - 1)) * plotWidth);
  const yForRank = (rank: number) => PADDING.top + ((rank - 1) / Math.max(1, displayMaxRank - 1)) * plotHeight;
  const yTicks = buildRankTicks(displayMaxRank);
  const series: RankSeries[] = selectedEntries.map((entry) => ({
    userId: identityKey(entry),
    userName: entry.userName,
    isCurrentUser: entry.isCurrentUser,
    points: history.days.flatMap((day, index) => {
      const point = day.entries.find((item) => identityKey(item) === identityKey(entry));
      return point
        ? [{ x: xForIndex(index), y: yForRank(point.rank), rank: point.rank, date: day.date }]
        : [];
    })
  }));

  return (
    <section className="rank-history-card">
      <div className="rank-history-header">
        <div>
          <p className="eyebrow">RANK HISTORY</p>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
        <span className="leaderboard-live-badge">近 10 天</span>
      </div>

      <div className="rank-history-legend">
        {series.map((item, index) => (
          <span className={`rank-history-legend-item rank-history-series-${index}`} key={item.userId}>
            <i />
            <strong>{item.userName}</strong>
            {item.isCurrentUser && <small>你</small>}
          </span>
        ))}
      </div>

      <div className="rank-history-canvas-wrap">
        <svg className="rank-history-chart" viewBox={`0 0 ${WIDTH} ${HEIGHT}`} role="img" aria-label={title}>
          {yTicks.map((rank) => {
            const y = yForRank(rank);
            return (
              <g className="rank-history-grid" key={rank}>
                <line x1={PADDING.left} y1={y} x2={WIDTH - PADDING.right} y2={y} />
                <text x={PADDING.left - 14} y={y + 4} textAnchor="end">
                  {rank}
                </text>
              </g>
            );
          })}

          {history.days.map((day, index) => {
            const x = xForIndex(index);
            return (
              <g className="rank-history-x-label" key={day.date}>
                <text x={x} y={HEIGHT - 17} textAnchor="middle">
                  {formatShortDate(day.date)}
                </text>
              </g>
            );
          })}

          <text className="rank-history-axis-title" x={14} y={PADDING.top + plotHeight / 2} textAnchor="middle" transform={`rotate(-90 14 ${PADDING.top + plotHeight / 2})`}>
            名次
          </text>

          {series.map((item, index) => {
            const points = item.points.map((point) => `${point.x},${point.y}`).join(" ");
            const style = { "--rank-series-delay": `${index * 70}ms` } as CSSProperties;
            return (
              <g className={`rank-history-series rank-history-series-${index}`} style={style} key={item.userId}>
                {item.points.length > 1 && <polyline points={points} pathLength="1" />}
                {item.points.map((point, pointIndex) => (
                  <g key={`${item.userId}-${pointIndex}`}>
                    <circle cx={point.x} cy={point.y} r="4.5" />
                    <title>
                      {item.userName} · {point.date} · 第 {point.rank} 名
                    </title>
                  </g>
                ))}
              </g>
            );
          })}
        </svg>
      </div>
      <p className="rank-history-footnote">图中展示当前前 {MAX_SERIES} 名；若当前用户不在前 {MAX_SERIES} 名，会额外保留当前用户曲线。</p>
    </section>
  );
}

function selectSeries(entries: CurrentRankEntry[]) {
  const selected = entries.slice(0, MAX_SERIES);
  const current = entries.find((entry) => entry.isCurrentUser);
  if (current && !selected.some((entry) => identityKey(entry) === identityKey(current))) {
    return [...selected, current];
  }

  return selected;
}

function identityKey(entry: { userId: string | null; userName: string }) {
  return entry.userId ?? `anonymous:${entry.userName}`;
}

function buildRankTicks(maxRank: number) {
  if (maxRank <= 6) {
    return Array.from({ length: maxRank }, (_, index) => index + 1);
  }

  return Array.from(new Set([1, Math.ceil(maxRank * 0.25), Math.ceil(maxRank * 0.5), Math.ceil(maxRank * 0.75), maxRank])).sort((a, b) => a - b);
}

function formatShortDate(value: string) {
  const parts = value.split("-");
  return parts.length === 3 ? `${parts[1]}/${parts[2]}` : value;
}
