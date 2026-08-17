import { Link } from "react-router-dom";

export function LeaderboardHomePage() {
  return (
    <section className="challenge-page leaderboard-page">
      <div className="leaderboard-header">
        <div>
          <p className="eyebrow">LEADERBOARDS</p>
          <h1>榜单中心</h1>
          <p>从全局积分到单个挑战，查看虚幻工作室网上答题平台的排名情况。</p>
        </div>
      </div>

      <div className="leaderboard-hub-grid">
        <Link className="leaderboard-hub-card" to="/leaderboards/users">
          <span className="eyebrow">GLOBAL</span>
          <h2>全局用户榜单</h2>
          <p>统计所有已发布挑战中的总分、完成题数和完成挑战数。</p>
        </Link>
        <Link className="leaderboard-hub-card" to="/leaderboards/challenges">
          <span className="eyebrow">CHALLENGES</span>
          <h2>挑战榜单</h2>
          <p>浏览所有已发布挑战的 Top 3 和完整排行榜入口。</p>
        </Link>
      </div>
    </section>
  );
}
