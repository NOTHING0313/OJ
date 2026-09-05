import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getAllTeams, type TeamListItemDto } from "../api/teamsApi";
import { formatDate } from "../utils/labels";

export function AdminTeamListPage() {
  const [teams, setTeams] = useState<TeamListItemDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAllTeams().then(setTeams).catch((err: unknown) => setError(err instanceof Error ? err.message : "加载战队失败"));
  }, []);

  return <section className="page-section ui-v2-page team-page">
    <div className="page-header"><div><h1>战队管理</h1></div></div>
    {error && <div className="alert error">{error}</div>}
    <div className="table-wrap"><table><thead><tr><th>战队</th><th>队长</th><th>成员</th><th>项目</th><th>创建时间</th><th>操作</th></tr></thead><tbody>
      {teams.map((team) => <tr key={team.id}><td>{team.name}</td><td>{team.owner.userName}</td><td>{team.memberCount}</td><td>{team.projectCount}</td><td>{formatDate(team.createdAt)}</td><td><Link to={`/admin/teams/${team.id}`}>查看</Link></td></tr>)}
    </tbody></table></div>
    {!error && teams.length === 0 && <div className="empty-state">暂无战队。</div>}
  </section>;
}
