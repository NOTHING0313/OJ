import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { getPublishedHelpDocument, getPublishedHelpDocuments, type HelpDocument, type HelpDocumentListItem } from "../api/helpDocumentsApi";
import { getApiErrorMessage } from "../api/httpClient";
import { useAuth } from "../auth/AuthContext";
import { canManageContent } from "../auth/roles";
import { HelpCenterView } from "../components/help/HelpCenterView";

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

  return <HelpCenterView documents={documents} document={document} isLoading={isLoading} error={error} canManage={canManage} />;
}
