import { request } from "./httpClient";

export interface HelpDocumentListItem {
  id: string;
  title: string;
  slug: string;
  summary: string | null;
  isPublished: boolean;
  sortOrder: number;
  updatedAt: string;
}

export interface HelpDocument extends HelpDocumentListItem {
  markdownContent: string;
  createdAt: string;
}

export interface HelpDocumentRequest {
  title: string;
  slug: string;
  summary: string | null;
  markdownContent: string;
  sortOrder: number;
}

export function getPublishedHelpDocuments() {
  return request<HelpDocumentListItem[]>("/api/help-documents");
}

export function getPublishedHelpDocument(slug: string) {
  return request<HelpDocument>(`/api/help-documents/${encodeURIComponent(slug)}`);
}

export function getAdminHelpDocuments() {
  return request<HelpDocumentListItem[]>("/api/admin/help-documents");
}

export function getAdminHelpDocument(id: string) {
  return request<HelpDocument>(`/api/admin/help-documents/${id}`);
}

export function createHelpDocument(payload: HelpDocumentRequest) {
  return request<HelpDocument>("/api/admin/help-documents", { method: "POST", body: JSON.stringify(payload) });
}

export function updateHelpDocument(id: string, payload: HelpDocumentRequest) {
  return request<HelpDocument>(`/api/admin/help-documents/${id}`, { method: "PUT", body: JSON.stringify(payload) });
}

export function publishHelpDocument(id: string) {
  return request<HelpDocument>(`/api/admin/help-documents/${id}/publish`, { method: "POST" });
}

export function unpublishHelpDocument(id: string) {
  return request<HelpDocument>(`/api/admin/help-documents/${id}/unpublish`, { method: "POST" });
}

export function deleteHelpDocument(id: string) {
  return request<void>(`/api/admin/help-documents/${id}`, { method: "DELETE" });
}
