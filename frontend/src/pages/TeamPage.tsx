import { FormEvent, KeyboardEvent, useCallback, useEffect, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  acceptInvitation, cancelInvitation, createProject, createTeam, declineInvitation, deleteProject,
  dissolveTeam, getAuditProjects, getMyInvitations, getMyTeam, getProjectCommits, getTeam,
  getTeamChallengeAnnouncements, getTeamChat, getTeamInvitations, inviteMember, leaveTeam,
  removeMember, sendTeamChat, syncProject, transferOwnership, updateProject, updateTeam,
  type TeamChallengeAnnouncementDto, type TeamChatMessageDto, type TeamDto, type TeamGitCommitDto,
  type TeamInvitationDto, type TeamProjectAuditDto, type TeamProjectDto
} from "../api/teamsApi";
import { useAuth } from "../auth/AuthContext";
import { ThemeIcon } from "../components/ThemeIcon";

const MAX_CHAT_LENGTH = 2000;

function projectStatus(project: TeamProjectDto) {
  if (project.lastSyncStatus === 2) return "同步成功";
  if (project.lastSyncStatus === 3) return "同步失败";
  return "尚未同步";
}

function announcementStatus(status: TeamChallengeAnnouncementDto["status"]) {
  if (status === "scheduled") return "已报名";
  if (status === "active") return "进行中";
  if (status === "peerReview") return "互评中";
  return "已结束";
}

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
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const [nameError, setNameError] = useState<string | null>(null);
  const [descriptionError, setDescriptionError] = useState<string | null>(null);
  const [inviteUserName, setInviteUserName] = useState("");
  const [projectName, setProjectName] = useState("");
  const [repositoryUrl, setRepositoryUrl] = useState("");
  const [auditProjects, setAuditProjects] = useState<TeamProjectAuditDto[]>([]);
  const [commits, setCommits] = useState<TeamGitCommitDto[]>([]);
  const [historyProjectName, setHistoryProjectName] = useState<string | null>(null);
  const [syncingProjectId, setSyncingProjectId] = useState<string | null>(null);
  const [messages, setMessages] = useState<TeamChatMessageDto[]>([]);
  const [announcements, setAnnouncements] = useState<TeamChallengeAnnouncementDto[]>([]);
  const [chatDraft, setChatDraft] = useState("");
  const [chatHasMore, setChatHasMore] = useState(false);
  const [chatSending, setChatSending] = useState(false);
  const [showNewMessages, setShowNewMessages] = useState(false);
  const [showProjectEditor, setShowProjectEditor] = useState(false);
  const [showMobileSidebar, setShowMobileSidebar] = useState(false);
  const chatViewportRef = useRef<HTMLDivElement | null>(null);
  const nearBottomRef = useRef(true);
  const initialChatLoadedRef = useRef(false);
  const messagesRef = useRef<TeamChatMessageDto[]>([]);
  const loadedTeamIdRef = useRef<string | null>(null);
  const isAuditView = Boolean(teamId);
  const isOwner = Boolean(team && currentUser?.id === team.owner.id);

  const load = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const loadedTeam = teamId ? await getTeam(teamId) : await getMyTeam();
      if ((loadedTeam?.id ?? null) !== loadedTeamIdRef.current) {
        loadedTeamIdRef.current = loadedTeam?.id ?? null;
        messagesRef.current = [];
        setMessages([]);
        setAnnouncements([]);
        setChatHasMore(false);
        setShowNewMessages(false);
      }
      setTeam(loadedTeam);
      if (loadedTeam) {
        setName(loadedTeam.name);
        setDescription(loadedTeam.description ?? "");
        setInvitations(currentUser?.id === loadedTeam.owner.id ? await getTeamInvitations(loadedTeam.id) : []);
        setAuditProjects(teamId ? await getAuditProjects(loadedTeam.id) : []);
      } else {
        setInvitations(await getMyInvitations());
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "加载战队失败");
    } finally {
      setIsLoading(false);
    }
  }, [currentUser?.id, teamId]);

  useEffect(() => { void load(); }, [load]);

  function scrollToBottom() {
    const viewport = chatViewportRef.current;
    if (!viewport) return;
    viewport.scrollTop = viewport.scrollHeight;
    nearBottomRef.current = true;
    setShowNewMessages(false);
  }

  const refreshChat = useCallback(async (activeTeam: TeamDto) => {
    const [page, notices] = await Promise.all([getTeamChat(activeTeam.id), getTeamChallengeAnnouncements(activeTeam.id)]);
    setAnnouncements(notices);
    setChatHasMore(page.hasMore);
    const existingIds = new Set(messagesRef.current.map((message) => message.id));
    const hasNew = page.messages.some((message) => !existingIds.has(message.id));
    const merged = new Map(messagesRef.current.map((message) => [message.id, message]));
    page.messages.forEach((message) => merged.set(message.id, message));
    const next = Array.from(merged.values()).sort((left, right) => {
      const time = new Date(left.createdAt).getTime() - new Date(right.createdAt).getTime();
      return time || left.id.localeCompare(right.id);
    });
    messagesRef.current = next;
    setMessages(next);
    if (hasNew && initialChatLoadedRef.current && !nearBottomRef.current) setShowNewMessages(true);
    if (!initialChatLoadedRef.current || nearBottomRef.current) {
      initialChatLoadedRef.current = true;
      requestAnimationFrame(scrollToBottom);
    }
  }, []);

  useEffect(() => {
    if (!team || isAuditView) return;
    let disposed = false;
    const refresh = async () => {
      if (disposed) return;
      try { await refreshChat(team); }
      catch (err) { if (!disposed) setError(err instanceof Error ? err.message : "加载聊天失败"); }
    };
    void refresh();
    const interval = window.setInterval(() => { if (document.visibilityState === "visible") void refresh(); }, 3000);
    const handleVisibility = () => { if (document.visibilityState === "visible") void refresh(); };
    document.addEventListener("visibilitychange", handleVisibility);
    return () => {
      disposed = true;
      window.clearInterval(interval);
      document.removeEventListener("visibilitychange", handleVisibility);
      initialChatLoadedRef.current = false;
    };
  }, [isAuditView, refreshChat, team]);

  async function run(action: () => Promise<unknown>) {
    setError(null);
    try { await action(); await load(); }
    catch (err) { setError(err instanceof Error ? err.message : "操作失败"); }
  }

  async function handleCreate(event: FormEvent) {
    event.preventDefault();
    if (isCreating) return;

    const trimmedName = name.trim();
    const trimmedDescription = description.trim();
    const nextNameError = trimmedName.length < 2 || trimmedName.length > 40 ? "战队名称需为 2 至 40 个字符。" : null;
    const nextDescriptionError = trimmedDescription.length > 500 ? "战队简介不能超过 500 个字符。" : null;
    setNameError(nextNameError);
    setDescriptionError(nextDescriptionError);
    setCreateError(null);
    if (nextNameError || nextDescriptionError) return;

    setIsCreating(true);
    try {
      const createdTeam = await createTeam(trimmedName, trimmedDescription);
      loadedTeamIdRef.current = createdTeam.id;
      setTeam(createdTeam);
      setInvitations([]);
      navigate("/teams", { replace: true });
    } catch (err) {
      const message = err instanceof Error ? err.message : "创建战队失败";
      if (message.includes("name already exists")) setNameError("该战队名称已被使用。");
      else if (message.includes("between 2 and 40")) setNameError("战队名称需为 2 至 40 个字符。");
      else if (message.includes("description") && message.includes("500")) setDescriptionError("战队简介不能超过 500 个字符。");
      else setCreateError(message);
    } finally {
      setIsCreating(false);
    }
  }

  function handleProject(event: FormEvent) {
    event.preventDefault();
    if (!team) return;
    void run(async () => {
      await createProject(team.id, projectName, repositoryUrl);
      setProjectName(""); setRepositoryUrl(""); setShowProjectEditor(false);
    });
  }

  async function handleSync(projectId: string) {
    if (!team) return;
    setSyncingProjectId(projectId);
    try { await syncProject(team.id, projectId); await load(); }
    catch (err) { setError(err instanceof Error ? err.message : "同步仓库失败"); }
    finally { setSyncingProjectId(null); }
  }

  async function handleHistory(project: TeamProjectAuditDto) {
    if (!team) return;
    try { setCommits(await getProjectCommits(team.id, project.id)); setHistoryProjectName(project.name); }
    catch (err) { setError(err instanceof Error ? err.message : "加载提交历史失败"); }
  }

  async function handleSend() {
    const content = chatDraft.trim();
    if (!team || !content || content.length > MAX_CHAT_LENGTH || chatSending) return;
    setChatSending(true); setError(null);
    try {
      const message = await sendTeamChat(team.id, content);
      if (!messagesRef.current.some((item) => item.id === message.id)) {
        messagesRef.current = [...messagesRef.current, message];
        setMessages(messagesRef.current);
      }
      setChatDraft(""); nearBottomRef.current = true; requestAnimationFrame(scrollToBottom);
    } catch (err) { setError(err instanceof Error ? err.message : "发送消息失败"); }
    finally { setChatSending(false); }
  }

  async function loadOlderMessages() {
    if (!team || messages.length === 0) return;
    const oldest = messages[0];
    try {
      const page = await getTeamChat(team.id, oldest.createdAt, oldest.id);
      setChatHasMore(page.hasMore);
      const ids = new Set(messagesRef.current.map((message) => message.id));
      messagesRef.current = [...page.messages.filter((message) => !ids.has(message.id)), ...messagesRef.current];
      setMessages(messagesRef.current);
    } catch (err) { setError(err instanceof Error ? err.message : "加载历史消息失败"); }
  }

  function handleChatScroll() {
    const viewport = chatViewportRef.current;
    if (!viewport) return;
    nearBottomRef.current = viewport.scrollHeight - viewport.scrollTop - viewport.clientHeight < 80;
    if (nearBottomRef.current) setShowNewMessages(false);
  }

  function handleChatKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === "Enter" && !event.shiftKey) { event.preventDefault(); void handleSend(); }
  }

  if (isLoading) return <div className="state-line">正在加载战队...</div>;

  if (!team && currentUser?.role !== 1) {
    return <section className="page-section narrow ui-v2-page team-page"><div className="page-header"><div><p className="eyebrow">TEAM AUDIT</p><h1>战队</h1><p>管理角色可查看全部战队及项目绑定信息。</p></div></div>{error && <div className="alert error">{error}</div>}<button className="button primary" onClick={() => navigate("/admin/teams")}>进入战队管理</button></section>;
  }

  if (!team) {
    return (
      <section className="page-section ui-v2-page team-page team-onboarding-page">
        <header className="team-onboarding-hero">
          <p className="eyebrow">TEAM</p>
          <h1>战队</h1>
          <p>与队友协作挑战、共享项目与代码历史。</p>
        </header>

        {error && <div className="alert error">{error}</div>}

        {invitations.length > 0 && (
          <section className="team-pending-invitations" aria-labelledby="pending-invitations-title">
            <h2 id="pending-invitations-title">待处理邀请</h2>
            <div className="team-invitation-list">
              {invitations.map((invitation) => (
                <div className="team-invitation-row" key={invitation.id}>
                  <span className="team-invitation-avatar" aria-hidden="true">{invitation.teamName.slice(0, 1).toUpperCase()}</span>
                  <span className="team-invitation-copy">
                    <strong>{invitation.teamName}</strong>
                    <small>邀请人 {invitation.invitedByUser.userName}</small>
                  </span>
                  <div className="button-row">
                    <button className="button primary" type="button" onClick={() => void run(() => acceptInvitation(invitation.id))}>加入</button>
                    <button className="button" type="button" onClick={() => void run(() => declineInvitation(invitation.id))}>拒绝</button>
                  </div>
                </div>
              ))}
            </div>
          </section>
        )}

        <section className="team-create-card" aria-labelledby="team-create-title">
          <div className="team-create-heading">
            <h2 id="team-create-title">创建属于你的战队</h2>
            <p>创建后即可邀请成员并进入战队工作区。</p>
          </div>
          <form className="form-stack team-create-form" onSubmit={(event) => void handleCreate(event)} noValidate>
            <label>
              战队名称
              <input
                value={name}
                onChange={(event) => { setName(event.target.value); setNameError(null); setCreateError(null); }}
                aria-invalid={Boolean(nameError)}
                aria-describedby={nameError ? "team-name-error" : undefined}
                maxLength={40}
                placeholder="2 至 40 个字符"
              />
              {nameError && <span className="team-field-error" id="team-name-error">{nameError}</span>}
            </label>
            <label>
              战队简介
              <textarea
                value={description}
                onChange={(event) => { setDescription(event.target.value); setDescriptionError(null); setCreateError(null); }}
                aria-invalid={Boolean(descriptionError)}
                aria-describedby={descriptionError ? "team-description-error" : undefined}
                maxLength={500}
                rows={4}
                placeholder="介绍战队方向与协作目标（可选）"
              />
              {descriptionError && <span className="team-field-error" id="team-description-error">{descriptionError}</span>}
            </label>
            {createError && <div className="team-create-error" role="alert">{createError}</div>}
            <button className="button primary team-create-submit" type="submit" disabled={isCreating}>
              {isCreating ? "正在创建..." : "创建战队"}
            </button>
          </form>
        </section>
      </section>
    );
  }

  if (isAuditView) {
    return <section className="page-section ui-v2-page team-page team-audit-page"><div className="page-header"><div><p className="eyebrow">TEAM AUDIT</p><h1>{team.name}</h1><p>{team.description || "这个战队暂未填写简介。"}</p></div><button className="button" onClick={() => navigate("/admin/teams")}>返回列表</button></div>{error && <div className="alert error">{error}</div>}<div className="team-audit-summary"><span>成员 <strong>{team.members.length} / 10</strong></span><span>项目 <strong>{auditProjects.length} / 5</strong></span><span>状态 <strong>正常</strong></span></div><section className="workspace-section audit-section"><div className="workspace-section-header static"><div><h2>成员管理</h2><span>{team.members.length} 名成员</span></div></div><div className="workspace-section-body"><div className="table-wrap"><table className="season-compact-table"><thead><tr><th>用户名</th><th>角色</th><th>状态</th></tr></thead><tbody>{team.members.map((member) => <tr key={member.id}><td><strong>{member.user.userName}</strong></td><td>{member.role === 2 ? "队长" : "成员"}</td><td><span className="season-row-status enabled">活跃</span></td></tr>)}</tbody></table></div></div></section><section className="workspace-section audit-section"><div className="workspace-section-header static"><div><h2>项目审计</h2><span>{auditProjects.length} 个项目</span></div></div><div className="workspace-section-body">{auditProjects.length === 0 ? <div className="compact-empty">暂无绑定项目</div> : <div className="table-wrap"><table className="season-compact-table"><thead><tr><th>项目名</th><th>Repository Host</th><th>同步状态</th><th>最近同步时间</th><th>操作</th></tr></thead><tbody>{auditProjects.map((project) => <tr key={project.id}><td><strong>{project.name}</strong></td><td><code>{new URL(project.repositoryUrl).host}</code></td><td><span className={`team-sync-status status-${project.lastSyncStatus}`}>{projectStatus(project)}</span></td><td>{project.lastSyncedAt ? new Date(project.lastSyncedAt).toLocaleString() : "—"}</td><td><div className="table-actions"><button type="button" disabled={!project.lastSyncedAt} onClick={() => void handleHistory(project)}>查看提交历史</button><button type="button" disabled={syncingProjectId === project.id} onClick={() => void handleSync(project.id)}>{syncingProjectId === project.id ? "同步中..." : "重新同步"}</button></div></td></tr>)}</tbody></table></div>}</div></section>{historyProjectName && <div className="admin-panel team-panel team-commit-history"><div className="admin-panel-header"><h2>{historyProjectName} · 提交历史</h2><button className="button" onClick={() => { setHistoryProjectName(null); setCommits([]); }}>关闭</button></div><div className="table-wrap"><table><thead><tr><th>SHA</th><th>作者</th><th>时间</th><th>提交信息</th></tr></thead><tbody>{commits.map((commit) => <tr key={commit.sha}><td><code>{commit.shortSha}</code></td><td>{commit.authorName}</td><td>{new Date(commit.committedAt).toLocaleString()}</td><td>{commit.subject}</td></tr>)}</tbody></table></div></div>}</section>;
  }

  return (
    <section className="page-section ui-v2-page team-page team-workspace-page">
      <header className="team-workspace-header"><div><p className="eyebrow">TEAM WORKSPACE</p><h1>{team.name}</h1><p>{team.description || "这个战队暂未填写简介。"}</p></div><div className="team-header-actions"><div className="team-header-metrics"><span>成员 <strong>{team.members.length} / 10</strong></span><span>项目 <strong>{team.projects.length} / 5</strong></span></div><button className="button team-mobile-info-toggle" type="button" onClick={() => setShowMobileSidebar((value) => !value)}>战队信息</button></div></header>
      {error && <div className="alert error">{error}</div>}
      <div className="team-workspace-layout">
        <main className="team-chat-workspace">
          <nav className="team-workspace-tabs" aria-label="战队工作区"><button className="active" type="button"><ThemeIcon slot="chat" />聊天</button>{team.projects.map((project) => <button key={project.id} type="button" onClick={() => navigate(`/teams/${team.id}/projects/${project.id}/history`)}><ThemeIcon slot="git" />{project.name}</button>)}</nav>
          {announcements.length > 0 && <div className="team-announcements">{announcements.map((announcement) => <button key={announcement.challengeId} type="button" onClick={() => navigate(`/challenges/${announcement.challengeId}`)}><span><small>挑战</small><strong>{announcement.title}</strong></span><span><b>{announcementStatus(announcement.status)}</b><small>截止 {new Date(announcement.endAt).toLocaleString()}</small></span><em>查看挑战</em></button>)}</div>}
          <div className="team-chat-viewport" ref={chatViewportRef} onScroll={handleChatScroll}>
            {chatHasMore && <button className="team-load-history" type="button" onClick={() => void loadOlderMessages()}>加载更早消息</button>}
            {messages.length === 0 && <div className="team-chat-empty"><strong>开始战队聊天</strong><span>消息仅当前战队成员可见。</span></div>}
            {messages.map((message, index) => {
              const previous = messages[index - 1];
              const showTime = !previous || new Date(message.createdAt).getTime() - new Date(previous.createdAt).getTime() > 5 * 60 * 1000;
              if (message.type === 2) return <div className="team-system-message" key={message.id}>{showTime && <time>{new Date(message.createdAt).toLocaleString()}</time>}<span>{message.content}</span>{message.relatedChallengeId && <button type="button" onClick={() => navigate(message.relatedPeerReviewAssignmentId ? `/challenges/${message.relatedChallengeId}/peer-review` : `/challenges/${message.relatedChallengeId}`)}>{message.relatedPeerReviewAssignmentId ? "进入互评" : "查看挑战"}</button>}</div>;
              const mine = message.sender?.id === currentUser?.id;
              return <div className={`team-chat-message ${mine ? "mine" : "other"}`} key={message.id}>{showTime && <time>{new Date(message.createdAt).toLocaleString()}</time>}<div className="team-chat-message-row">{!mine && (message.sender?.avatarUrl ? <img className="team-chat-avatar" src={message.sender.avatarUrl} alt={message.sender.userName} /> : <span className="team-chat-avatar fallback">{message.sender?.userName.slice(0, 1).toUpperCase()}</span>)}<div><small>{!mine && message.sender?.userName}</small><p title={new Date(message.createdAt).toLocaleString()}>{message.content}</p></div></div></div>;
            })}
          </div>
          {showNewMessages && <button className="team-new-message-indicator" type="button" onClick={scrollToBottom}>有新消息</button>}
          <div className="team-chat-composer"><textarea value={chatDraft} onChange={(event) => setChatDraft(event.target.value)} onKeyDown={handleChatKeyDown} maxLength={MAX_CHAT_LENGTH} placeholder="发送战队消息…" rows={2} /><div><span className={chatDraft.length >= 1800 ? "near-limit" : ""}>{chatDraft.length} / {MAX_CHAT_LENGTH}</span><button className="button primary" type="button" disabled={!chatDraft.trim() || chatSending} onClick={() => void handleSend()}>{chatSending ? "发送中" : "发送"}</button></div></div>
        </main>
        <aside className={`team-workspace-sidebar ${showMobileSidebar ? "mobile-open" : ""}`}>
          <section><h2>战队信息</h2><p>{team.description || "暂无简介"}</p><div className="team-sidebar-counts"><span>成员 {team.members.length} / 10</span><span>项目 {team.projects.length} / 5</span></div></section>
          <details open><summary>成员 <span>{team.members.length} / 10</span></summary><div className="team-compact-list">{team.members.map((member) => <div key={member.id}>{member.user.avatarUrl ? <img src={member.user.avatarUrl} alt={member.user.userName} /> : <span className="team-member-avatar">{member.user.userName.slice(0, 1).toUpperCase()}</span>}<strong>{member.user.userName}</strong><small>{member.role === 2 ? "Owner" : "Member"}</small>{isOwner && member.user.id !== currentUser?.id && <div className="team-member-actions"><button onClick={() => void run(() => transferOwnership(team.id, member.user.id))}>转让</button><button onClick={() => void run(() => removeMember(team.id, member.user.id))}>移除</button></div>}</div>)}</div>{!isOwner && <button className="button danger compact" type="button" onClick={() => void run(() => leaveTeam(team.id))}>退出战队</button>}</details>
          <details open><summary>项目 <span>{team.projects.length} / 5</span></summary><div className="team-compact-projects">{team.projects.length === 0 && <p className="muted">尚未绑定项目。</p>}{team.projects.map((project) => <div key={project.id}><button type="button" onClick={() => navigate(`/teams/${team.id}/projects/${project.id}/history`)}><strong>{project.name}</strong><small>{new URL(project.repositoryUrl).host}</small><span className={`team-sync-status status-${project.lastSyncStatus}`}>{projectStatus(project)}</span></button>{isOwner && <div><button type="button" onClick={() => { const nextName = window.prompt("项目名称", project.name); const nextUrl = window.prompt("Git 仓库地址", project.repositoryUrl); if (nextName && nextUrl) void run(() => updateProject(team.id, project.id, nextName, nextUrl)); }}>编辑</button><button type="button" onClick={() => void run(() => deleteProject(team.id, project.id))}>删除</button></div>}</div>)}</div>{isOwner && <button className="button compact" type="button" onClick={() => setShowProjectEditor((value) => !value)}>管理项目</button>}{showProjectEditor && isOwner && <form className="form-stack team-project-form compact-editor" onSubmit={handleProject}><label>项目名称<input value={projectName} onChange={(event) => setProjectName(event.target.value)} maxLength={80} required /></label><label>Git URL<input type="url" value={repositoryUrl} onChange={(event) => setRepositoryUrl(event.target.value)} required /></label><button className="button primary" type="submit">绑定项目</button></form>}</details>
          {isOwner && <details><summary>邀请</summary><form className="form-stack compact-editor" onSubmit={(event) => { event.preventDefault(); void run(async () => { await inviteMember(team.id, inviteUserName); setInviteUserName(""); }); }}><label>用户名<input value={inviteUserName} onChange={(event) => setInviteUserName(event.target.value)} required /></label><button className="button primary" type="submit">邀请成员</button></form>{invitations.filter((item) => item.status === 1).map((invitation) => <div className="team-pending-invite" key={invitation.id}><span>{invitation.invitedUser.userName}</span><button type="button" onClick={() => void run(() => cancelInvitation(team.id, invitation.id))}>取消</button></div>)}</details>}
          {isOwner && <details><summary>设置</summary><form className="form-stack compact-editor" onSubmit={(event) => { event.preventDefault(); void run(() => updateTeam(team.id, name, description)); }}><label>战队名称<input value={name} onChange={(event) => setName(event.target.value)} minLength={2} maxLength={40} required /></label><label>简介<textarea value={description} onChange={(event) => setDescription(event.target.value)} maxLength={500} /></label><button className="button primary" type="submit">保存</button></form></details>}
          {isOwner && <details className="team-danger-zone"><summary>危险操作</summary><p>解散后成员将无法进入工作空间，聊天历史保留。</p><button className="button danger" type="button" onClick={() => { if (window.confirm("确定解散战队？成员历史、聊天和项目记录将保留。") && window.confirm("此操作不可撤销，确认继续解散？")) void run(() => dissolveTeam(team.id)); }}>解散战队</button></details>}
        </aside>
      </div>
    </section>
  );
}
