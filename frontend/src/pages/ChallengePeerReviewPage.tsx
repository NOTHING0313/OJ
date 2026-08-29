import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  getChallengePeerReview,
  saveChallengePeerReviewDraft,
  submitChallengePeerReview,
  type ChallengePeerReviewWorkspace,
  type SaveChallengePeerReviewRequest
} from "../api/challengesApi";

export function ChallengePeerReviewPage() {
  const { challengeId } = useParams();
  const [workspace, setWorkspace] = useState<ChallengePeerReviewWorkspace | null>(null);
  const [form, setForm] = useState({ overallScore: "", summary: "", strengths: "", improvements: "" });
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (!challengeId) return;
    let ignore = false;
    getChallengePeerReview(challengeId)
      .then((data) => {
        if (ignore) return;
        setWorkspace(data);
        setForm({
          overallScore: data.review?.overallScore?.toString() ?? "",
          summary: data.review?.summary ?? "",
          strengths: data.review?.strengths ?? "",
          improvements: data.review?.improvements ?? ""
        });
        setError(null);
      })
      .catch((err: unknown) => {
        if (!ignore) setError(err instanceof Error ? err.message : "互评任务加载失败");
      });
    return () => { ignore = true; };
  }, [challengeId]);

  async function save(submit: boolean) {
    if (!challengeId) return;
    if (submit && !window.confirm("提交后评审内容将不可修改，确认提交吗？")) return;

    const payload: SaveChallengePeerReviewRequest = {
      overallScore: form.overallScore ? Number(form.overallScore) : null,
      summary: form.summary,
      strengths: form.strengths,
      improvements: form.improvements
    };
    try {
      setIsSaving(true);
      setError(null);
      const updated = submit
        ? await submitChallengePeerReview(challengeId, payload)
        : await saveChallengePeerReviewDraft(challengeId, payload);
      setWorkspace(updated);
      setNotice(submit ? "互评已提交并冻结。" : "草稿已保存，战队冻结成员将共享此草稿。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "互评保存失败");
    } finally {
      setIsSaving(false);
    }
  }

  if (error && !workspace) {
    return <section className="page-section narrow"><div className="alert error">{error}</div></section>;
  }
  if (!workspace) return <div className="state-line">正在加载互评任务...</div>;

  return (
    <section className="challenge-page ui-v2-page editor-v2-page">
      <div className="leaderboard-header ui-v2-page-header">
        <div><p className="eyebrow">TEAM PEER REVIEW</p><h1>战队项目互评</h1><p>冻结阵容共享一份草稿；正式提交后不可修改。</p></div>
        <Link className="button" to={`/challenges/${challengeId}`}>返回挑战</Link>
      </div>
      {workspace.peerReviewEndAt && <div className="quiet-note">互评截止：{formatDate(workspace.peerReviewEndAt)}</div>}
      {error && <div className="alert error">{error}</div>}
      {notice && <div className="quiet-note success">{notice}</div>}

      {!workspace.assignmentReady ? (
        <div className="empty-state">
          {workspace.insufficientTeams ? "报名战队不足 2 支，本场不生成互评任务。" : "互评任务正在生成，请稍后刷新。"}
        </div>
      ) : (
        <>
          <section className="admin-panel">
            <p className="eyebrow">REVIEW TARGET</p>
            <h2>{workspace.targetTeamName}</h2>
            <p>{workspace.targetProjectName}</p>
            <span className="context-chip">
              {workspace.review?.status === 2 ? "Submitted" : workspace.isExpired ? "Expired" : "Draft"}
            </span>
            {workspace.targetRepositoryUrl && (
              <a href={workspace.targetRepositoryUrl} target="_blank" rel="noreferrer noopener">打开项目仓库</a>
            )}
          </section>
          <form className="form-stack admin-panel" onSubmit={(event) => { event.preventDefault(); void save(false); }}>
            <label>
              综合评分（1–5）
              <select disabled={!workspace.canEdit} value={form.overallScore} onChange={(event) => setForm((current) => ({ ...current, overallScore: event.target.value }))}>
                <option value="">暂不评分</option>
                {[1, 2, 3, 4, 5].map((score) => <option key={score} value={score}>{score}</option>)}
              </select>
            </label>
            <label>总结<textarea maxLength={1000} disabled={!workspace.canEdit} value={form.summary} onChange={(event) => setForm((current) => ({ ...current, summary: event.target.value }))} /></label>
            <label>项目优点<textarea maxLength={2000} disabled={!workspace.canEdit} value={form.strengths} onChange={(event) => setForm((current) => ({ ...current, strengths: event.target.value }))} /></label>
            <label>改进建议<textarea maxLength={2000} disabled={!workspace.canEdit} value={form.improvements} onChange={(event) => setForm((current) => ({ ...current, improvements: event.target.value }))} /></label>
            <div className="button-row">
              {workspace.canEdit ? (
                <>
                  <button className="button" disabled={isSaving} type="submit">保存草稿</button>
                  <button className="button primary" disabled={isSaving} type="button" onClick={() => void save(true)}>正式提交</button>
                </>
              ) : <span className="context-chip">{workspace.review?.status === 2 ? "已提交" : workspace.isExpired ? "已截止" : "只读"}</span>}
            </div>
          </form>
        </>
      )}
    </section>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("zh-CN", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
}
