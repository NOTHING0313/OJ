import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getChallengePeerReviewAdminAudit, type ChallengePeerReviewAdminSummary } from "../api/challengesApi";

export function ChallengePeerReviewAuditPage() {
  const { challengeId } = useParams();
  const [audit, setAudit] = useState<ChallengePeerReviewAdminSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!challengeId) return;
    let ignore = false;
    getChallengePeerReviewAdminAudit(challengeId)
      .then((data) => {
        if (!ignore) {
          setAudit(data);
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) setError(err instanceof Error ? err.message : "互评审计加载失败");
      });
    return () => { ignore = true; };
  }, [challengeId]);

  if (error) {
    return (
      <section className="page-section narrow">
        <div className="alert error">{error}</div>
        <Link className="button" to={`/challenges/${challengeId}`}>返回挑战</Link>
      </section>
    );
  }
  if (!audit) return <div className="state-line">正在加载互评审计...</div>;

  return (
    <section className="challenge-page admin-summary-page ui-v2-page analytics-v2-page">
      <div className="leaderboard-header ui-v2-page-header">
        <div>
          <h1>项目互评审计</h1>
        </div>
        <Link className="button" to={`/challenges/${challengeId}`}>返回挑战</Link>
      </div>

      <div className="admin-metrics">
        <Metric label="互评任务" value={audit.assignmentCount} />
        <Metric label="已提交" value={audit.submittedCount} />
      </div>

      <section className="admin-panel">
        <div className="admin-panel-header">
          <div><h2>互评明细</h2><p>{audit.submittedCount} / {audit.assignmentCount} 已提交</p></div>
        </div>
        {audit.assignments.length === 0 ? <div className="empty-state">暂无互评分配</div> : (
          <div className="table-wrap leaderboard-table-wrap">
            <table className="leaderboard-table">
              <thead><tr><th>评审战队</th><th>目标项目</th><th>状态</th><th>评分</th><th>评审内容</th></tr></thead>
              <tbody>{audit.assignments.map((assignment) => (
                <tr key={assignment.assignmentId}>
                  <td><strong>{assignment.reviewerTeam}</strong><br /><span className="muted">{assignment.reviewerRoster.join("、")}</span></td>
                  <td><strong>{assignment.targetTeam} · {assignment.targetProjectName}</strong><br /><a href={assignment.targetRepositoryUrl} target="_blank" rel="noreferrer noopener">仓库快照</a></td>
                  <td>{assignment.reviewStatus === 2 ? "已提交" : assignment.reviewStatus === 1 ? "草稿" : "未开始"}<br /><span className="muted">{assignment.submittedAt ? formatDate(assignment.submittedAt) : "—"}</span></td>
                  <td>{assignment.overallScore ?? "—"}</td>
                  <td><details><summary>查看完整评审</summary><p>{assignment.summary ?? "—"}</p><p><strong>优点：</strong>{assignment.strengths ?? "—"}</p><p><strong>建议：</strong>{assignment.improvements ?? "—"}</p></details></td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        )}
      </section>
    </section>
  );
}

function Metric({ label, value }: { label: string; value: number }) {
  return <div className="admin-metric"><span>{label}</span><strong>{value}</strong></div>;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("zh-CN", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
}
