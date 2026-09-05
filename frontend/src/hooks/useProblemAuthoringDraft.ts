import { useEffect, useRef, useState } from "react";
import { problemDraftKey, readDraft, writeDraft } from "../utils/problemDrafts";
import { parseAuthoringDraft, type AuthoringFields } from "../utils/problemAuthoringDraft";

// Browser drafts are recoverable edits, never the authoritative server version.
export function useProblemAuthoringDraft(userId: string | undefined, problemId: string | undefined, version: number, ready: boolean, fields: AuthoringFields, apply: (fields: AuthoringFields) => void) {
  const key = problemDraftKey(userId, "authoring-v1", problemId ?? "new");
  const [pending, setPending] = useState(() => parseAuthoringDraft(readDraft(key)));
  const [warning, setWarning] = useState<string | null>(null);
  const baseline = useRef<string | null>(null);
  const saved = useRef(false);
  const snapshot = JSON.stringify(fields);
  const dirty = !saved.current && ready && baseline.current !== null && baseline.current !== snapshot;
  const conflict = Boolean(pending && pending.version !== version);
  const dirtyRef = useRef(dirty);
  dirtyRef.current = dirty;

  useEffect(() => {
    if (!ready) return;
    if (baseline.current === null || saved.current) {
      baseline.current = snapshot;
      saved.current = false;
      return;
    }
    if (pending) return;
    const ok = writeDraft(key, baseline.current === snapshot ? null : JSON.stringify({ schema: 1, version, fields: JSON.parse(snapshot) }));
    setWarning(ok ? null : "浏览器无法保存或清理草稿，请先保存到服务器，再离开页面。");
  }, [key, pending, ready, snapshot, version]);

  useEffect(() => {
    const beforeUnload = (event: BeforeUnloadEvent) => { if (dirtyRef.current && !saved.current) { event.preventDefault(); event.returnValue = ""; } };
    const captureNavigation = (event: MouseEvent) => {
      if (!dirtyRef.current || saved.current || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
      const link = (event.target as Element | null)?.closest<HTMLAnchorElement>("a[href]");
      if (!link || link.target === "_blank" || link.href === window.location.href) return;
      if (!window.confirm("题目修改尚未保存到服务器，确定离开？")) { event.preventDefault(); event.stopPropagation(); }
    };
    window.addEventListener("beforeunload", beforeUnload);
    document.addEventListener("click", captureNavigation, true);
    return () => { window.removeEventListener("beforeunload", beforeUnload); document.removeEventListener("click", captureNavigation, true); };
  }, []);

  function discard() {
    if (!writeDraft(key, null)) { setWarning("无法清理本地草稿，请检查浏览器存储设置。"); return; }
    setPending(null);
    setWarning(null);
  }
  function markSaved() {
    saved.current = true;
    dirtyRef.current = false;
    setPending(null);
    setWarning(writeDraft(key, null) ? null : "题目已保存，但本地旧草稿未能清理。");
  }
  function download() {
    if (!pending) return;
    const url = URL.createObjectURL(new Blob([JSON.stringify(pending, null, 2)], { type: "application/json" }));
    const link = document.createElement("a"); link.href = url; link.download = "problem-draft.json"; link.click(); URL.revokeObjectURL(url);
  }
  return { pending, warning, dirty, conflict, discard, markSaved, download, restore() { if (!pending || conflict) return; apply(pending.fields); setPending(null); } };
}
