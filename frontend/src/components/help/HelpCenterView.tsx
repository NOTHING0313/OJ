import { Link } from "react-router-dom";
import type { HelpDocument, HelpDocumentListItem } from "../../api/helpDocumentsApi";
import { HelpMarkdown } from "./HelpMarkdown";

interface HelpCenterViewProps {
  documents: HelpDocumentListItem[];
  document: HelpDocument | null;
  isLoading: boolean;
  error: string | null;
  canManage: boolean;
}

export function HelpCenterView({ documents, document, isLoading, error, canManage }: HelpCenterViewProps) {
  return <section className="help-center-page"><header className="page-header help-center-header" data-surface="decoration.pageHeader"><div><p className="eyebrow">HELP CENTER</p><h1>帮助中心</h1><p>平台使用说明与功能文档</p></div>{canManage && <Link className="button" to="/help/manage">文档管理</Link>}</header>{isLoading ? <div className="state-line">正在加载帮助文档...</div> : documents.length === 0 ? <div className="empty-state help-empty-state"><p>暂无帮助文档</p>{canManage && <Link className="button primary" to="/help/manage/new">新建文档</Link>}</div> : <div className="help-center-layout" data-surface="panel.primary"><aside className="help-directory" aria-label="文档目录"><strong>文档目录</strong><nav>{documents.map((item) => <Link key={item.id} className={item.slug === document?.slug ? "active" : ""} to={`/help/${item.slug}`}><span>{item.title}</span>{item.summary && <small>{item.summary}</small>}</Link>)}</nav></aside><article className="help-document-panel" data-surface="panel.primary">{error ? <div className="alert error" role="alert">{error}</div> : document && <><header><h1>{document.title}</h1>{document.summary && <p>{document.summary}</p>}</header><HelpMarkdown>{document.markdownContent}</HelpMarkdown></>}</article></div>}</section>;
}
