import { request } from "./httpClient";

export type UserRole = 1 | 2 | 3;

export interface AdminUserDto {
  id: string;
  userName: string;
  email: string;
  avatarUrl: string | null;
  role: UserRole;
  isBlacklisted: boolean;
  createdAt: string;
}

export interface PagedUsersResult {
  items: AdminUserDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface GetUsersParams {
  keyword?: string;
  role?: UserRole | "all";
  isBlacklisted?: boolean | "all";
  page?: number;
  pageSize?: number;
}

export function getUsers(params: GetUsersParams = {}) {
  const search = new URLSearchParams();

  if (params.keyword?.trim()) {
    search.set("keyword", params.keyword.trim());
  }

  if (params.role && params.role !== "all") {
    search.set("role", String(params.role));
  }

  if (params.isBlacklisted !== undefined && params.isBlacklisted !== "all") {
    search.set("isBlacklisted", String(params.isBlacklisted));
  }

  if (params.page) {
    search.set("page", String(params.page));
  }

  if (params.pageSize) {
    search.set("pageSize", String(params.pageSize));
  }

  const query = search.toString();
  return request<PagedUsersResult>(`/api/users${query ? `?${query}` : ""}`);
}

export function promoteToProblemSetter(userId: string) {
  return request<AdminUserDto>(`/api/users/${userId}/promote-to-problem-setter`, {
    method: "POST"
  });
}

export function demoteToAnswerer(userId: string) {
  return request<AdminUserDto>(`/api/users/${userId}/demote-to-answerer`, {
    method: "POST"
  });
}

export function blacklistUser(userId: string) {
  return request<void>(`/api/users/${userId}/blacklist`, {
    method: "POST"
  });
}

export function unblacklistUser(userId: string) {
  return request<void>(`/api/users/${userId}/unblacklist`, {
    method: "POST"
  });
}
