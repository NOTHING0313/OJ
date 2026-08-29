import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { getPublishedHelpDocument, getPublishedHelpDocuments, type HelpDocument, type HelpDocumentListItem } from "../api/helpDocumentsApi";
import { getApiErrorMessage } from "../api/httpClient";
import { canManageContent, useAuth } from "../auth/AuthContext";
import { HelpMarkdown } from "../components/help/HelpMarkdown";

export function HelpCenterPage() {
  const { slug } = useParams();
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const [documents, setDocuments] = useState<HelpDocumentListItem[]>([]);
  const [document, setDocument] = useState<HelpDocument | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const canManage = canManageContent(currentUser?.role);

  useEffect(() => {
    let ignore = false;
    setIsLoading(true);
    setError(null);

    async function load() {
      try {
        const list = await getPublishedHelpDocuments();
        if (ignore) return;
        setDocuments(list);
        if (list.length === 0) {
          setDocument(null);
          return;
        }

        const selectedSlug = slug ?? list[0].slug;
        if (!slug) navigate(`/help/${selectedSlug}`, { replace: true });
        const detail = await getPublishedHelpDocument(selectedSlug);
        if (!ignore) setDocument(detail);
      } catch (err) {
        if (!ignore) {
          setDocument(null);
          setError(getApiErrorMessage(err, "加载文档失败，请稍后重试。"));
        }
      } finally {
        if (!ignore) setIsLoading(false);
      }
    }

    void load();
    return () => { ignore = true; };
  }, [navigate, slug]);

  return (
    <section className="help-center-page">
      <header className="page-header help-center-header">
        <div>
          <p className="eyebrow">HELP CENTER</p>
          <h1>帮助中心</h1>
          <p>平台使用说明与功能文档</p>
        </div>
        {canManage && <Link className="button" to="/help/manage">文档管理</Link>}
      </header>

      {isLoading ? <div className="state-line">正在加载帮助文档...</div> : documents.length === 0 ? (
        <div className="empty-state help-empty-state">
          <p>暂无帮助文档</p>
          {canManage && <Link className="button primary" to="/help/manage/new">新建文档</Link>}
        </div>
      ) : (
        <div className="help-center-layout">
          <aside className="help-directory" aria-label="文档目录">
            <strong>文档目录</strong>
            <nav>
              {documents.map((item) => (
                <Link key={item.id} className={item.slug === document?.slug ? "active" : ""} to={`/help/${item.slug}`}>
                  <span>{item.title}</span>
                  {item.summary && <small>{item.summary}</small>}
                </Link>
              ))}
            </nav>
          </aside>
          <article className="help-document-panel">
            {error ? <div className="alert error" role="alert">{error}</div> : document && (
              <>
                <header>
                  <h1>{document.title}</h1>
                  {document.summary && <p>{document.summary}</p>}
                </header>
                <HelpMarkdown>{document.markdownContent}</HelpMarkdown>
              </>
            )}
          </article>
        </div>
      )}
    </section>
  );
}
