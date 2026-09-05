import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { getTeam, getTeamProjectHistory, syncTeamProject, type TeamDto, type TeamProjectGitHistoryDto } from "../api/teamsApi";
import { useAuth } from "../auth/AuthContext";

export function TeamProjectHistoryPage() {
  const { teamId = "", projectId = "" } = useParams();
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const [team, setTeam] = useState<TeamDto | null>(null);
  const [history, setHistory] = useState<TeamProjectGitHistoryDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [syncing, setSyncing] = useState(false);

  const load = useCallback(async () => {
    try {
      const [loadedTeam, loadedHistory] = await Promise.all([getTeam(teamId), getTeamProjectHistory(teamId, projectId)]);
        setTeam(loadedTeam);
      setHistory(loadedHistory);
    } catch (err) {
      setError(err instanceof Error ? err.message : "加载提交历史失败");
    }
  }, [projectId, teamId]);

  useEffect(() => { void load(); }, [load]);

  async function handleSync() {
    if (syncing) return;
    setSyncing(true);
    setError(null);
    let syncError: string | null = null;
    try {
      await syncTeamProject(teamId, projectId);
    } catch (err) {
      syncError = err instanceof Error ? err.message : "同步仓库失败";
    }
    await load();
    setError(syncError);
    setSyncing(false);
  }

  const project = team?.projects.find((item) => item.id === projectId);
  const isOwner = Boolean(team && currentUser?.id === team.owner.id);
  const commits = history?.commits ?? [];
  const neverSynced = history?.lastSyncStatus === 1;
  const syncFailed = history?.lastSyncStatus === 3;
  return (
    <section className="page-section ui-v2-page team-page team-history-page">
      <div className="page-header">
        <div><h1>{project?.name ?? "项目提交历史"}</h1><p>{team?.name ?? "战队项目"}</p></div>
        <button className="button" onClick={() => navigate("/teams")}>返回聊天</button>
      </div>
      {error && <div className="alert error">{error}</div>}
      {!error && (!team || !history) && <div className="state-line">正在加载项目...</div>}
      {team && history && neverSynced && <div className="admin-panel team-panel team-history-state"><span className="team-sync-status status-1">未同步</span><strong>仓库尚未同步</strong><p className="muted">{isOwner ? "同步后即可查看提交历史。" : "等待队长同步。"}</p>{isOwner && <button className="button primary" type="button" disabled={syncing} onClick={() => void handleSync()}>{syncing ? "同步中..." : "同步仓库"}</button>}</div>}
      {team && history && syncFailed && <div className="admin-panel team-panel team-history-state"><span className="team-sync-status status-3">同步失败</span><strong>最近一次同步未完成</strong>{history.lastSyncError && <p className="muted">{history.lastSyncError}</p>}{isOwner && <button className="button primary" type="button" disabled={syncing} onClick={() => void handleSync()}>{syncing ? "同步中..." : "重新同步"}</button>}</div>}
      {team && history && !neverSynced && commits.length === 0 && <div className="admin-panel team-panel"><p className="muted">暂无可显示的提交记录。</p></div>}
      {commits.length > 0 && <div className="admin-panel team-panel team-commit-history table-wrap"><table><thead><tr><th>SHA</th><th>作者</th><th>时间</th><th>提交信息</th></tr></thead><tbody>
        {commits.map((commit) => <tr key={commit.sha}><td><code>{commit.shortSha}</code></td><td>{commit.authorName}</td><td>{new Date(commit.committedAt).toLocaleString()}</td><td>{commit.subject}</td></tr>)}
      </tbody></table></div>}
    </section>
  );
}
