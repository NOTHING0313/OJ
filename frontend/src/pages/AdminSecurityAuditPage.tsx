import { useEffect, useState } from "react";
import { getSecurityAuditDetail, querySecurityAudit, type SecurityAuditLog } from "../api/securityAuditApi";

const pageSize = 20;

export function AdminSecurityAuditPage() {
  const [items, setItems] = useState<SecurityAuditLog[]>([]);
  const [actor, setActor] = useState("");
  const [action, setAction] = useState("");
  const [result, setResult] = useState("");
  const [target, setTarget] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [selected, setSelected] = useState<SecurityAuditLog | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const handle = window.setTimeout(() => void load(), 180);
    return () => window.clearTimeout(handle);
  }, [actor, action, result, target, from, to, page]);

  async function load() {
    try {
      setLoading(true);
      const response = await querySecurityAudit({
        actor, action, result, targetId: target,
        from: toIso(from), to: toIso(to), page, pageSize
      });
      setItems(response.items);
      setTotalCount(response.totalCount);
      setError(null);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "安全审计加载失败");
    } finally {
      setLoading(false);
    }
  }

  async function showDetail(id: string) {
    try {
      setSelected(await getSecurityAuditDetail(id));
    } catch (detailError) {
      setError(detailError instanceof Error ? detailError.message : "审计详情加载失败");
    }
  }

  function reset() {
    setActor(""); setAction(""); setResult(""); setTarget(""); setFrom(""); setTo(""); setPage(1);
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <section className="admin-security-audit-page">
      <header className="leaderboard-header">
        <div><p className="eyebrow">ROOT ADMIN</p><h1>安全审计</h1><p>查看高风险管理操作的不可变审计记录。</p></div>
        <span className="submission-total">共 {totalCount} 条记录</span>
      </header>

      {error && <div className="alert error">{error}</div>}
      <div className="security-audit-toolbar">
        <label><span>操作者</span><input value={actor} placeholder="用户名" onChange={(event) => { setActor(event.target.value); setPage(1); }} /></label>
        <label><span>操作</span><input value={action} placeholder="如 User.Blacklisted" onChange={(event) => { setAction(event.target.value); setPage(1); }} /></label>
        <label><span>结果</span><select value={result} onChange={(event) => { setResult(event.target.value); setPage(1); }}><option value="">全部</option><option value="Succeeded">成功</option><option value="Failed">失败</option><option value="Denied">拒绝</option><option value="Requested">已请求</option></select></label>
        <label><span>目标</span><input value={target} placeholder="目标 ID" onChange={(event) => { setTarget(event.target.value); setPage(1); }} /></label>
        <label><span>开始</span><input type="datetime-local" value={from} onChange={(event) => { setFrom(event.target.value); setPage(1); }} /></label>
        <label><span>结束</span><input type="datetime-local" value={to} onChange={(event) => { setTo(event.target.value); setPage(1); }} /></label>
        <button className="button" type="button" onClick={reset}>重置</button>
      </div>

      <div className="security-audit-table-wrap">
        {loading ? <div className="submission-state-panel">正在加载安全审计...</div> : items.length === 0 ? <div className="submission-state-panel">未找到匹配的审计记录</div> : (
          <table className="security-audit-table"><thead><tr><th>时间</th><th>操作者</th><th>操作</th><th>目标</th><th>结果</th><th>操作</th></tr></thead><tbody>
            {items.map((item) => <tr key={item.id}><td>{formatDate(item.createdAt)}</td><td>{item.actorNameSnapshot ?? "系统"}</td><td><code>{item.action}</code></td><td>{item.targetType}{item.targetId ? ` · ${item.targetId}` : ""}</td><td><span className={`security-audit-result ${item.result.toLowerCase()}`}>{resultLabel(item.result)}</span></td><td><button className="button" type="button" onClick={() => void showDetail(item.id)}>查看详情</button></td></tr>)}
          </tbody></table>
        )}
      </div>

      <div className="admin-user-pagination"><button className="button" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>上一页</button><span>第 {page} / {totalPages} 页</span><button className="button" disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)}>下一页</button></div>

      {selected && <div className="security-audit-backdrop" role="presentation" onMouseDown={() => setSelected(null)}><div className="security-audit-detail" role="dialog" aria-modal="true" aria-label="安全审计详情" onMouseDown={(event) => event.stopPropagation()}><header><div><p className="eyebrow">AUDIT DETAIL</p><h2>审计详情</h2></div><button className="button" type="button" onClick={() => setSelected(null)}>关闭</button></header><dl><dt>时间</dt><dd>{formatDate(selected.createdAt)}</dd><dt>操作者</dt><dd>{selected.actorNameSnapshot ?? "系统"}</dd><dt>操作</dt><dd>{selected.action}</dd><dt>目标</dt><dd>{selected.targetType}{selected.targetId ? ` · ${selected.targetId}` : ""}</dd><dt>结果</dt><dd>{resultLabel(selected.result)}</dd><dt>客户端 IP</dt><dd>{selected.clientIp ?? "未记录"}</dd><dt>安全元数据</dt><dd><pre>{formatMetadata(selected.metadataJson)}</pre></dd></dl></div></div>}
    </section>
  );
}

function toIso(value: string) { return value ? new Date(value).toISOString() : undefined; }
function formatDate(value: string) { return new Date(value).toLocaleString("zh-CN", { hour12: false }); }
function resultLabel(value: string) { return ({ Succeeded: "成功", Failed: "失败", Denied: "拒绝", Requested: "已请求" } as Record<string, string>)[value] ?? value; }
function formatMetadata(value?: string) { if (!value) return "无"; try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return "不可解析"; } }
