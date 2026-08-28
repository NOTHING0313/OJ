import { FormEvent, useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  acceptInvitation,
  cancelInvitation,
  createProject,
  createTeam,
  declineInvitation,
  deleteProject,
  dissolveTeam,
  getMyInvitations,
  getMyTeam,
  getTeam,
  getTeamInvitations,
  inviteMember,
  leaveTeam,
  removeMember,
  transferOwnership,
  updateProject,
  updateTeam,
  type TeamDto,
  type TeamInvitationDto
} from "../api/teamsApi";
import { useAuth } from "../auth/AuthContext";

export function TeamPage() {
  const { teamId } = useParams();
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const [team, setTeam] = useState<TeamDto | null>(null);
  const [invitations, setInvitations] = useState<TeamInvitationDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [inviteUserName, setInviteUserName] = useState("");
  const [projectName, setProjectName] = useState("");
  const [repositoryUrl, setRepositoryUrl] = useState("");
  const isAuditView = Boolean(teamId);
  const isOwner = Boolean(team && currentUser?.id === team.owner.id);

  const load = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const loadedTeam = teamId ? await getTeam(teamId) : await getMyTeam();
      setTeam(loadedTeam);
      if (loadedTeam) {
        setName(loadedTeam.name);
        setDescription(loadedTeam.description ?? "");
        setInvitations(isOwner || currentUser?.id === loadedTeam.owner.id ? await getTeamInvitations(loadedTeam.id) : []);
      } else {
        setInvitations(await getMyInvitations());
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "加载战队失败");
    } finally {
      setIsLoading(false);
    }
  }, [currentUser?.id, isOwner, teamId]);

  useEffect(() => { void load(); }, [load]);

  async function run(action: () => Promise<unknown>) {
    setError(null);
    try {
      await action();
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "操作失败");
    }
  }

  function handleCreate(event: FormEvent) {
    event.preventDefault();
    void run(() => createTeam(name, description));
  }

  function handleProject(event: FormEvent) {
    event.preventDefault();
    if (!team) return;
    void run(async () => {
      await createProject(team.id, projectName, repositoryUrl);
      setProjectName("");
      setRepositoryUrl("");
    });
  }

  if (isLoading) return <div className="state-line">正在加载战队...</div>;

  if (!team && currentUser?.role !== 1) {
    return (
      <section className="page-section narrow ui-v2-page team-page">
        <div className="page-header"><div><p className="eyebrow">TEAM AUDIT</p><h1>战队</h1><p>管理角色可查看全部战队及项目绑定信息。</p></div></div>
        {error && <div className="alert error">{error}</div>}
        <button className="button primary" onClick={() => navigate("/admin/teams")}>进入战队管理</button>
      </section>
    );
  }

  if (!team) {
    return (
      <section className="page-section narrow ui-v2-page team-page">
        <div className="page-header"><div><p className="eyebrow">TEAM</p><h1>战队</h1><p>创建战队，邀请成员并绑定公开 Git 项目。</p></div></div>
        {error && <div className="alert error">{error}</div>}
        <div className="admin-panel team-panel">
          <h2>创建战队</h2>
          <form className="form-stack" onSubmit={handleCreate}>
            <label>战队名称<input value={name} onChange={(event) => setName(event.target.value)} minLength={2} maxLength={40} required /></label>
            <label>战队简介<textarea value={description} onChange={(event) => setDescription(event.target.value)} maxLength={500} /></label>
            <button className="button primary" type="submit">创建战队</button>
          </form>
        </div>
        <div className="admin-panel team-panel">
          <h2>待处理邀请</h2>
          {invitations.length === 0 ? <p className="muted">暂无待处理邀请。</p> : invitations.map((invitation) => (
            <div className="team-list-row" key={invitation.id}>
              <span><strong>{invitation.teamName}</strong> · 邀请人 {invitation.invitedByUser.userName}</span>
              <div className="button-row"><button className="button primary" onClick={() => void run(() => acceptInvitation(invitation.id))}>接受</button><button className="button" onClick={() => void run(() => declineInvitation(invitation.id))}>拒绝</button></div>
            </div>
          ))}
        </div>
      </section>
    );
  }

  return (
    <section className="page-section ui-v2-page team-page">
      <div className="page-header"><div><p className="eyebrow">TEAM</p><h1>{team.name}</h1><p>{team.description || "这个战队暂未填写简介。"}</p></div>{isAuditView && <button className="button" onClick={() => navigate("/admin/teams")}>返回列表</button>}</div>
      {error && <div className="alert error">{error}</div>}

      <div className="team-grid">
        <div className="admin-panel team-panel">
          <div className="admin-panel-header"><h2>成员</h2><span>{team.members.length} / 10</span></div>
          {team.members.map((member) => (
            <div className="team-list-row" key={member.id}>
              <span><strong>{member.user.userName}</strong> <span className="team-badge">{member.role === 2 ? "队长" : "成员"}</span></span>
              {isOwner && member.user.id !== currentUser?.id && (
                <div className="button-row">
                  <button className="button" onClick={() => void run(() => transferOwnership(team.id, member.user.id))}>转让队长</button>
                  <button className="button danger" onClick={() => void run(() => removeMember(team.id, member.user.id))}>移除</button>
                </div>
              )}
            </div>
          ))}
          {!isOwner && !isAuditView && <button className="button danger" onClick={() => void run(() => leaveTeam(team.id))}>退出战队</button>}
        </div>

        <div className="admin-panel team-panel">
          <div className="admin-panel-header"><h2>项目</h2><span>{team.projects.length} / 5</span></div>
          {team.projects.length === 0 && <p className="muted">尚未绑定项目。</p>}
          {team.projects.map((project) => (
            <div className="team-project-card" key={project.id}>
              <strong>{project.name}</strong>
              <a href={project.repositoryUrl} target="_blank" rel="noreferrer noopener">{project.repositoryUrl}</a>
              {isOwner && <div className="button-row">
                <button className="button" onClick={() => {
                  const nextName = window.prompt("项目名称", project.name);
                  const nextUrl = window.prompt("Git 仓库地址", project.repositoryUrl);
                  if (nextName && nextUrl) void run(() => updateProject(team.id, project.id, nextName, nextUrl));
                }}>编辑</button>
                <button className="button danger" onClick={() => void run(() => deleteProject(team.id, project.id))}>删除</button>
              </div>}
            </div>
          ))}
          {isOwner && <form className="form-stack team-project-form" onSubmit={handleProject}>
            <h3>绑定项目</h3>
            <label>项目名称<input value={projectName} onChange={(event) => setProjectName(event.target.value)} maxLength={80} required /></label>
            <label>Git 仓库地址<input type="url" value={repositoryUrl} onChange={(event) => setRepositoryUrl(event.target.value)} placeholder="https://github.com/owner/repository.git" required /></label>
            <p className="muted">V1 仅支持配置允许的公开 HTTPS Git 仓库。</p>
            <button className="button primary" type="submit">绑定项目</button>
          </form>}
        </div>
      </div>

      {isOwner && <div className="team-grid">
        <div className="admin-panel team-panel">
          <h2>战队设置</h2>
          <form className="form-stack" onSubmit={(event) => { event.preventDefault(); void run(() => updateTeam(team.id, name, description)); }}>
            <label>战队名称<input value={name} onChange={(event) => setName(event.target.value)} minLength={2} maxLength={40} required /></label>
            <label>战队简介<textarea value={description} onChange={(event) => setDescription(event.target.value)} maxLength={500} /></label>
            <button className="button primary" type="submit">保存</button>
          </form>
        </div>
        <div className="admin-panel team-panel">
          <h2>邀请成员</h2>
          <form className="form-stack" onSubmit={(event) => { event.preventDefault(); void run(async () => { await inviteMember(team.id, inviteUserName); setInviteUserName(""); }); }}>
            <label>用户名<input value={inviteUserName} onChange={(event) => setInviteUserName(event.target.value)} required /></label>
            <button className="button primary" type="submit">发送邀请</button>
          </form>
          {invitations.filter((item) => item.status === 1).map((invitation) => (
            <div className="team-list-row" key={invitation.id}><span>{invitation.invitedUser.userName}</span><button className="button" onClick={() => void run(() => cancelInvitation(team.id, invitation.id))}>取消邀请</button></div>
          ))}
          <button className="button danger team-dissolve" onClick={() => { if (window.confirm("确定解散战队？成员历史和项目记录将保留。")) void run(() => dissolveTeam(team.id)); }}>解散战队</button>
        </div>
      </div>}
    </section>
  );
}
