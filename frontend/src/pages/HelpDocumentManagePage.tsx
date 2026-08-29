import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { deleteHelpDocument, getAdminHelpDocuments, publishHelpDocument, unpublishHelpDocument, type HelpDocumentListItem } from "../api/helpDocumentsApi";
import { getApiErrorMessage } from "../api/httpClient";

export function HelpDocumentManagePage() {
  const [documents, setDocuments] = useState<HelpDocumentListItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  async function load() {
    try {
      setDocuments(await getAdminHelpDocuments());
      setError(null);
    } catch (err) {
      setError(getApiErrorMessage(err, "加载文档失败，请稍后重试。"));
    }
  }

  useEffect(() => { void load(); }, []);

  async function togglePublished(item: HelpDocumentListItem) {
    setBusyId(item.id);
    try {
      await (item.isPublished ? unpublishHelpDocument(item.id) : publishHelpDocument(item.id));
      await load();
    } catch (err) {
      setError(getApiErrorMessage(err, item.isPublished ? "下架失败，请稍后重试。" : "发布失败，请稍后重试。"));
    } finally {
      setBusyId(null);
    }
  }

  async function remove(item: HelpDocumentListItem) {
    if (!window.confirm(`确认删除帮助文档“${item.title}”？此操作无法撤销。`)) return;
    setBusyId(item.id);
    try {
      await deleteHelpDocument(item.id);
      await load();
    } catch (err) {
      setError(getApiErrorMessage(err, "删除失败，请稍后重试。"));
    } finally {
      setBusyId(null);
    }
  }

  return (
    <section className="help-manage-page">
      <header className="page-header help-center-header">
        <div>
          <p className="eyebrow">HELP MANAGEMENT</p>
          <h1>文档管理</h1>
          <p>维护帮助中心的草稿与已发布内容。</p>
        </div>
        <div className="help-header-actions">
          <Link className="button" to="/help">返回帮助中心</Link>
          <Link className="button primary" to="/help/manage/new">新建文档</Link>
        </div>
      </header>
      {error && <div className="alert error" role="alert">{error}</div>}
      {documents.length === 0 ? (
        <div className="empty-state"><p>暂无文档</p><p>创建文档后即可向答题人发布平台使用说明。</p></div>
      ) : (
        <div className="table-scroll help-document-table-wrap">
          <table className="help-document-table">
            <thead><tr><th>标题</th><th>状态</th><th>更新时间</th><th>排序</th><th>操作</th></tr></thead>
            <tbody>
              {documents.map((item) => (
                <tr key={item.id}>
                  <td><strong>{item.title}</strong><small>/{item.slug}</small></td>
                  <td><span className={`help-status ${item.isPublished ? "published" : "draft"}`}>{item.isPublished ? "已发布" : "草稿"}</span></td>
                  <td>{new Date(item.updatedAt).toLocaleString()}</td>
                  <td>{item.sortOrder}</td>
                  <td><div className="table-actions">
                    <Link to={`/help/manage/${item.id}`}>编辑</Link>
                    <button type="button" disabled={busyId === item.id} onClick={() => void togglePublished(item)}>{item.isPublished ? "下架" : "发布"}</button>
                    <button className="danger-link" type="button" disabled={busyId === item.id} onClick={() => void remove(item)}>删除</button>
                  </div></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
