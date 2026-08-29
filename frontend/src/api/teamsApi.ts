import { request } from "./httpClient";

export interface TeamUserDto {
  id: string;
  userName: string;
  avatarUrl: string | null;
}

export interface TeamMemberDto {
  id: string;
  user: TeamUserDto;
  role: number;
  joinedAt: string;
}

export interface TeamProjectDto {
  id: string;
  name: string;
  repositoryUrl: string;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
  lastSyncedAt: string | null;
  lastSyncStatus: number;
}

export interface TeamProjectAuditDto extends TeamProjectDto {
  lastSyncAttemptAt: string | null;
  lastSyncError: string | null;
  defaultBranch: string | null;
}

export interface TeamGitCommitDto {
  sha: string;
  shortSha: string;
  authorName: string;
  authorEmail: string;
  authoredAt: string;
  committerName: string;
  committerEmail: string;
  committedAt: string;
  subject: string;
}

export interface TeamProjectGitHistoryDto {
  lastSyncStatus: number;
  lastSyncedAt: string | null;
  lastSyncError: string | null;
  commits: TeamGitCommitDto[];
}

export interface TeamInvitationDto {
  id: string;
  teamId: string;
  teamName: string;
  invitedUser: TeamUserDto;
  invitedByUser: TeamUserDto;
  status: number;
  createdAt: string;
  respondedAt: string | null;
}

export interface TeamDto {
  id: string;
  name: string;
  description: string | null;
  owner: TeamUserDto;
  members: TeamMemberDto[];
  projects: TeamProjectDto[];
  createdAt: string;
}

export interface TeamListItemDto {
  id: string;
  name: string;
  owner: TeamUserDto;
  memberCount: number;
  projectCount: number;
  createdAt: string;
}

export interface TeamChatMessageDto {
  id: string;
  type: number;
  content: string | null;
  sender: TeamUserDto | null;
  relatedChallengeId: string | null;
  relatedPeerReviewAssignmentId: string | null;
  createdAt: string;
}

export interface TeamChatPageDto {
  messages: TeamChatMessageDto[];
  hasMore: boolean;
}

export interface TeamChallengeAnnouncementDto {
  challengeId: string;
  title: string;
  status: "scheduled" | "active" | "peerReview" | "ended";
  startAt: string;
  endAt: string;
}

export const getMyTeam = () => request<TeamDto | null>("/api/teams/my");
export const getTeam = (teamId: string) => request<TeamDto>(`/api/teams/${teamId}`);
export const getAllTeams = () => request<TeamListItemDto[]>("/api/admin/teams");
export const getMyInvitations = () => request<TeamInvitationDto[]>("/api/team-invitations/my");
export const getTeamInvitations = (teamId: string) => request<TeamInvitationDto[]>(`/api/teams/${teamId}/invitations`);
export const getAuditProjects = (teamId: string) => request<TeamProjectAuditDto[]>(`/api/admin/teams/${teamId}/projects`);
export const syncProject = (teamId: string, projectId: string) => request<TeamProjectAuditDto>(`/api/admin/teams/${teamId}/projects/${projectId}/sync`, { method: "POST" });
export const getProjectCommits = (teamId: string, projectId: string, skip = 0, limit = 50) => request<TeamGitCommitDto[]>(`/api/admin/teams/${teamId}/projects/${projectId}/commits?skip=${skip}&limit=${limit}`);

export function createTeam(name: string, description: string) {
  return request<TeamDto>("/api/teams", { method: "POST", body: JSON.stringify({ name, description: description || null }) });
}

export function updateTeam(teamId: string, name: string, description: string) {
  return request<TeamDto>(`/api/teams/${teamId}`, { method: "PUT", body: JSON.stringify({ name, description: description || null }) });
}

export const dissolveTeam = (teamId: string) => request<void>(`/api/teams/${teamId}`, { method: "DELETE" });
export const leaveTeam = (teamId: string) => request<void>(`/api/teams/${teamId}/leave`, { method: "POST" });
export const removeMember = (teamId: string, userId: string) => request<void>(`/api/teams/${teamId}/members/${userId}`, { method: "DELETE" });
export const transferOwnership = (teamId: string, userId: string) => request<void>(`/api/teams/${teamId}/transfer-ownership`, { method: "POST", body: JSON.stringify({ userId }) });
export const inviteMember = (teamId: string, userName: string) => request<TeamInvitationDto>(`/api/teams/${teamId}/invitations`, { method: "POST", body: JSON.stringify({ userName }) });
export const acceptInvitation = (invitationId: string) => request<void>(`/api/team-invitations/${invitationId}/accept`, { method: "POST" });
export const declineInvitation = (invitationId: string) => request<void>(`/api/team-invitations/${invitationId}/decline`, { method: "POST" });
export const cancelInvitation = (teamId: string, invitationId: string) => request<void>(`/api/teams/${teamId}/invitations/${invitationId}`, { method: "DELETE" });

export function createProject(teamId: string, name: string, repositoryUrl: string) {
  return request<TeamProjectDto>(`/api/teams/${teamId}/projects`, { method: "POST", body: JSON.stringify({ name, repositoryUrl }) });
}

export function updateProject(teamId: string, projectId: string, name: string, repositoryUrl: string) {
  return request<TeamProjectDto>(`/api/teams/${teamId}/projects/${projectId}`, { method: "PUT", body: JSON.stringify({ name, repositoryUrl }) });
}

export const deleteProject = (teamId: string, projectId: string) => request<void>(`/api/teams/${teamId}/projects/${projectId}`, { method: "DELETE" });

export function getTeamChat(teamId: string, beforeCreatedAt?: string, beforeId?: string) {
  const cursor = beforeCreatedAt && beforeId
    ? `?beforeCreatedAt=${encodeURIComponent(beforeCreatedAt)}&beforeId=${encodeURIComponent(beforeId)}`
    : "";
  return request<TeamChatPageDto>(`/api/teams/${teamId}/chat${cursor}`);
}

export const sendTeamChat = (teamId: string, content: string) => request<TeamChatMessageDto>(`/api/teams/${teamId}/chat`, {
  method: "POST",
  body: JSON.stringify({ content })
});

export const getTeamChallengeAnnouncements = (teamId: string) => request<TeamChallengeAnnouncementDto[]>(`/api/teams/${teamId}/challenge-announcements`);
export const getTeamProjectHistory = (teamId: string, projectId: string, skip = 0, limit = 50) => request<TeamProjectGitHistoryDto>(`/api/teams/${teamId}/projects/${projectId}/commits?skip=${skip}&limit=${limit}`);
export const syncTeamProject = (teamId: string, projectId: string) => request<TeamProjectAuditDto>(`/api/teams/${teamId}/projects/${projectId}/sync`, { method: "POST" });
