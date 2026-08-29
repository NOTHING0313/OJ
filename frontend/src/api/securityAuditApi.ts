import { request } from "./httpClient";

export interface SecurityAuditLog {
  id: string;
  actorUserId?: string;
  actorNameSnapshot?: string;
  action: string;
  targetType: string;
  targetId?: string;
  result: string;
  metadataJson?: string;
  createdAt: string;
  clientIp?: string;
}

export interface SecurityAuditPage {
  items: SecurityAuditLog[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SecurityAuditFilters {
  actor?: string;
  action?: string;
  result?: string;
  targetType?: string;
  targetId?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export function querySecurityAudit(filters: SecurityAuditFilters) {
  const params = new URLSearchParams();
  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined && value !== "") params.set(key, String(value));
  });
  return request<SecurityAuditPage>(`/api/admin/security-audit?${params.toString()}`);
}

export function getSecurityAuditDetail(id: string) {
  return request<SecurityAuditLog>(`/api/admin/security-audit/${id}`);
}
