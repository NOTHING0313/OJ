import { useCallback, useEffect, useMemo, useState, type Dispatch, type FormEvent, type ReactNode, type SetStateAction } from "react";
import { Link } from "react-router-dom";
import {
  addLeaderboardSeasonProblems, archiveLeaderboardSeason, createLeaderboardSeason, finalizeLeaderboardSeason,
  freezeLeaderboardSeason, getAdminLeaderboardSeasons, getCurrentSeasonAuditLeaderboard, getLeaderboardSeasonHistory,
  removeLeaderboardSeasonProblems, updateLeaderboardSeasonProblemBenchmark, updateLeaderboardSeason,
  type LeaderboardJudgeLanguage, type LeaderboardSeason, type LeaderboardSeasonHistorySummary,
  type LeaderboardSeasonProblem, type SeasonLeaderboard
} from "../api/leaderboardsApi";
import { getChallenges, type ChallengeListItemDto } from "../api/challengesApi";
import { getProblems, type ProblemListItemDto } from "../api/problemsApi";
import { useAuth } from "../auth/AuthContext";

type ProblemFilter = "joined" | "available" | "all";
type SectionKey = "boards" | "challenges" | "problems" | "rewards";
type BenchmarkEditor = { problem: LeaderboardSeasonProblem; language: LeaderboardJudgeLanguage } | null;

export function AdminLeaderboardSeasonPage() {
  const { currentUser } = useAuth();
  const isRoot = currentUser?.role === 3;
  const [seasons, setSeasons] = useState<LeaderboardSeason[]>([]);
  const [leaderboard, setLeaderboard] = useState<SeasonLeaderboard | null>(null);
  const [history, setHistory] = useState<LeaderboardSeasonHistorySummary[]>([]);
  const [problems, setProblems] = useState<ProblemListItemDto[]>([]);
  const [challenges, setChallenges] = useState<ChallengeListItemDto[]>([]);
  const [selectedProblems, setSelectedProblems] = useState<string[]>([]);
  const [problemSearch, setProblemSearch] = useState("");
  const [problemFilter, setProblemFilter] = useState<ProblemFilter>("joined");
  const [openSections, setOpenSections] = useState<Record<SectionKey, boolean>>({ boards: false, challenges: true, problems: true, rewards: false });
  const [form, setForm] = useState(() => defaultForm());
  const [showEditor, setShowEditor] = useState(false);
  const [benchmarkEditor, setBenchmarkEditor] = useState<BenchmarkEditor>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const currentSeason = useMemo(() => seasons.find((season) => season.isCurrent) ?? null, [seasons]);
  const scheduled = currentSeason?.effectiveStatus === 1;

  const reload = useCallback(async () => {
    try {
      const [seasonData, leaderboardData, problemData, challengeData, historyData] = await Promise.all([
        getAdminLeaderboardSeasons(), getCurrentSeasonAuditLeaderboard(), getProblems(), getChallenges(), getLeaderboardSeasonHistory()
      ]);
      setSeasons(seasonData); setLeaderboard(leaderboardData); setProblems(problemData); setChallenges(challengeData); setHistory(historyData); setError(null);
    } catch (err) { setError(err instanceof Error ? err.message : "榜单管理数据加载失败"); }
  }, []);

  useEffect(() => { void reload(); }, [reload]);

  async function run(action: () => Promise<unknown>, message: string) {
    setIsBusy(true); setError(null); setNotice(null);
    try { await action(); await reload(); setSelectedProblems([]); setNotice(message); }
    catch (err) { setError(err instanceof Error ? err.message : "操作失败"); }
    finally { setIsBusy(false); }
  }

  function openEditor() { setForm(currentSeason && scheduled ? toForm(currentSeason) : defaultForm()); setShowEditor(true); }

  function submitSeason(event: FormEvent) {
    event.preventDefault();
    const payload = {
      name: form.name.trim(), startAt: new Date(form.startAt).toISOString(), freezeAt: new Date(form.freezeAt).toISOString(),
      publicUntil: new Date(form.publicUntil).toISOString(), includeGlobalBoard: form.includeGlobalBoard,
      challengeIds: currentSeason?.boards.flatMap((board) => board.challengeId ? [board.challengeId] : []) ?? [],
      firstCompletionBonusEnabled: currentSeason?.scoringRules.firstCompletionBonusEnabled ?? false,
      runtimeBonusEnabled: currentSeason?.scoringRules.runtimeBonusEnabled ?? false,
      memoryBonusEnabled: currentSeason?.scoringRules.memoryBonusEnabled ?? false
    };
    void run(() => currentSeason ? updateLeaderboardSeason(currentSeason.id, payload) : createLeaderboardSeason(payload), currentSeason ? "赛季配置已更新" : "赛季已创建").then(() => setShowEditor(false));
  }

  function updateSeasonSettings(patch: { includeGlobalBoard?: boolean; challengeIds?: string[]; firstCompletionBonusEnabled?: boolean; runtimeBonusEnabled?: boolean; memoryBonusEnabled?: boolean }, message: string) {
    if (!currentSeason) return;
    const rules = currentSeason.scoringRules;
    void run(() => updateLeaderboardSeason(currentSeason.id, {
      name: currentSeason.name, startAt: currentSeason.startAt, freezeAt: currentSeason.freezeAt, publicUntil: currentSeason.publicUntil,
      includeGlobalBoard: patch.includeGlobalBoard ?? currentSeason.boards.some((board) => board.boardType === 1),
      challengeIds: patch.challengeIds ?? currentSeason.boards.flatMap((board) => board.challengeId ? [board.challengeId] : []),
      firstCompletionBonusEnabled: patch.firstCompletionBonusEnabled ?? rules.firstCompletionBonusEnabled,
      runtimeBonusEnabled: patch.runtimeBonusEnabled ?? rules.runtimeBonusEnabled,
      memoryBonusEnabled: patch.memoryBonusEnabled ?? rules.memoryBonusEnabled
    }), message);
  }

  const joinedById = useMemo(() => new Map(currentSeason?.problems.map((problem) => [problem.problemId, problem]) ?? []), [currentSeason]);
  const visibleProblems = useMemo(() => problems.filter((problem) => {
    const joined = joinedById.has(problem.id);
    const matchesFilter = problemFilter === "all" || (problemFilter === "joined" ? joined : !joined);
    return matchesFilter && problem.title.toLocaleLowerCase().includes(problemSearch.trim().toLocaleLowerCase());
  }), [joinedById, problemFilter, problemSearch, problems]);
  const linkedChallenges = useMemo(() => currentSeason?.boards.flatMap((board) => {
    if (!board.challengeId) return [];
    const challenge = challenges.find((item) => item.id === board.challengeId);
    return challenge ? [challenge] : [];
  }) ?? [], [challenges, currentSeason]);
  const eligibleChallenges = useMemo(() => currentSeason ? challenges.filter((challenge) => new Date(challenge.startAt) >= new Date(currentSeason.startAt) && new Date(challenge.endAt) <= new Date(currentSeason.freezeAt)) : [], [challenges, currentSeason]);
  const selectedVisible = visibleProblems.length > 0 && visibleProblems.every((problem) => selectedProblems.includes(problem.id));

  return <section className="admin-page leaderboard-season-admin-page season-workspace">
    <div className="page-header ui-v2-page-header"><div><p className="eyebrow">SEASON MANAGEMENT</p><h1>榜单管理</h1><p>集中配置赛季结构、挑战榜、题目快照与奖励基准。</p></div>{isRoot && (!currentSeason || scheduled) && <button className="button primary" type="button" onClick={openEditor}>{currentSeason ? "编辑赛季" : "创建赛季"}</button>}</div>
    {error && <div className="alert error">{error}</div>}{notice && <div className="quiet-note success">{notice}</div>}
    {showEditor && <SeasonEditorModal form={form} setForm={setForm} isBusy={isBusy} isEdit={Boolean(currentSeason)} onClose={() => setShowEditor(false)} onSubmit={submitSeason} />}

    {currentSeason ? <>
      <section className="season-overview"><div><p className="eyebrow">SEASON OVERVIEW</p><div className="season-overview-title"><h2>{currentSeason.name}</h2><span className={`season-status status-${currentSeason.effectiveStatus}`}>{statusLabel(currentSeason.effectiveStatus)}</span></div></div><dl><div><dt>开始时间</dt><dd>{formatDate(currentSeason.startAt)}</dd></div><div><dt>冻结时间</dt><dd>{formatDate(currentSeason.freezeAt)}</dd></div><div><dt>公示结束</dt><dd>{formatDate(currentSeason.publicUntil)}</dd></div></dl><div className="button-row">{isRoot && scheduled && <><button className="button" type="button" onClick={openEditor}>编辑赛季</button><Link className="button primary" to={`/admin/challenges/new?seasonId=${currentSeason.id}`}>创建并关联挑战</Link></>}{isRoot && currentSeason.effectiveStatus === 2 && <button className="button" disabled={isBusy} onClick={() => void run(() => freezeLeaderboardSeason(currentSeason.id), "赛季已提前冻结")}>提前冻结</button>}{isRoot && (currentSeason.effectiveStatus === 3 || currentSeason.status === 4) && <button className="button primary" disabled={isBusy} onClick={() => void run(() => finalizeLeaderboardSeason(currentSeason.id), "最终榜快照已生成")}>定榜</button>}{isRoot && currentSeason.status === 4 && <button className="button danger" disabled={isBusy} onClick={() => void run(() => archiveLeaderboardSeason(currentSeason.id), "赛季已归档")}>归档</button>}</div></section>

      <WorkspaceSection title="榜单设置" summary={`${currentSeason.boards.length} 个榜单已启用`} open={openSections.boards} onToggle={() => toggleSection("boards", setOpenSections)}><div className="season-setting-list"><SettingToggle label="全局榜" checked={currentSeason.boards.some((board) => board.boardType === 1)} disabled={!isRoot || !scheduled || isBusy} onChange={(checked) => updateSeasonSettings({ includeGlobalBoard: checked }, checked ? "全局榜已启用" : "全局榜已关闭")} /><div className="season-setting-divider"><strong>挑战榜</strong><span>仅显示生命周期完整位于赛季内的挑战</span></div>{eligibleChallenges.map((challenge) => { const checked = currentSeason.boards.some((board) => board.challengeId === challenge.id); const linkedIds = currentSeason.boards.flatMap((board) => board.challengeId ? [board.challengeId] : []); return <SettingToggle key={challenge.id} label={challenge.title} checked={checked} disabled={!isRoot || !scheduled || isBusy} onChange={(enabled) => updateSeasonSettings({ challengeIds: enabled ? [...new Set([...linkedIds, challenge.id])] : linkedIds.filter((id) => id !== challenge.id) }, enabled ? "挑战榜已关联" : "榜单关联已移除，挑战本身保持不变")} />; })}{eligibleChallenges.length === 0 && <div className="compact-empty">暂无符合赛季时间范围的挑战</div>}</div></WorkspaceSection>

      <WorkspaceSection title="挑战管理" summary={`${linkedChallenges.length} 个关联挑战`} open={openSections.challenges} onToggle={() => toggleSection("challenges", setOpenSections)}>{linkedChallenges.length === 0 ? <div className="compact-empty">尚未关联挑战，可从当前赛季创建或在榜单设置中关联。</div> : <div className="table-wrap"><table className="season-compact-table"><thead><tr><th>挑战名称</th><th>模式</th><th>开始时间</th><th>结束时间</th><th>参赛</th><th>状态</th><th>榜单</th><th>操作</th></tr></thead><tbody>{linkedChallenges.map((challenge) => <tr key={challenge.id}><td><strong>{challenge.title}</strong></td><td>{challenge.participationMode === 2 ? "战队" : "个人"}</td><td>{formatDate(challenge.startAt)}</td><td>{formatDate(challenge.endAt)}</td><td>{challenge.participantCount}</td><td><span className="season-row-status">{challengeStatus(challenge)}</span></td><td><span className="season-row-status enabled">已启用</span></td><td><div className="table-actions"><Link to={`/challenges/${challenge.id}`}>查看</Link>{challenge.canManage && <Link to={`/admin/challenges/${challenge.id}/edit`}>编辑</Link>}</div></td></tr>)}</tbody></table></div>}</WorkspaceSection>

      <WorkspaceSection title="题目管理" summary={`${currentSeason.problems.length} 题 · 分数自动取有效测试用例总和`} open={openSections.problems} onToggle={() => toggleSection("problems", setOpenSections)}><div className="season-problem-toolbar"><input placeholder="搜索题目" value={problemSearch} onChange={(event) => setProblemSearch(event.target.value)} /><div className="segmented-control" aria-label="题目筛选">{(["joined", "available", "all"] as ProblemFilter[]).map((filter) => <button className={problemFilter === filter ? "active" : ""} key={filter} type="button" onClick={() => setProblemFilter(filter)}>{filter === "joined" ? "已加入" : filter === "available" ? "未加入" : "全部"}</button>)}</div>{isRoot && scheduled && <div className="button-row"><button className="button" type="button" onClick={() => setSelectedProblems(selectedVisible ? selectedProblems.filter((id) => !visibleProblems.some((problem) => problem.id === id)) : [...new Set([...selectedProblems, ...visibleProblems.map((problem) => problem.id)])])}>{selectedVisible ? "取消当前页" : "选择当前结果"}</button><button className="button primary" disabled={isBusy || !selectedProblems.some((id) => !joinedById.has(id))} onClick={() => void run(() => addLeaderboardSeasonProblems(currentSeason.id, selectedProblems.filter((id) => !joinedById.has(id))), "题目已批量加入")}>批量加入</button><button className="button danger" disabled={isBusy || !selectedProblems.some((id) => joinedById.has(id))} onClick={() => void run(() => removeLeaderboardSeasonProblems(currentSeason.id, selectedProblems.filter((id) => joinedById.has(id))), "题目已批量移除")}>批量移除</button></div>}</div>{visibleProblems.length === 0 ? <div className="compact-empty">未找到匹配的题目</div> : <div className="table-wrap"><table className="season-compact-table season-problem-table"><thead><tr><th></th><th>题目名称</th><th>题型</th><th>允许语言</th><th>题目总分</th><th>状态</th><th>操作</th></tr></thead><tbody>{visibleProblems.map((problem) => { const joined = joinedById.get(problem.id); return <tr key={problem.id}><td>{isRoot && scheduled && <input aria-label={`选择 ${problem.title}`} type="checkbox" checked={selectedProblems.includes(problem.id)} onChange={(event) => setSelectedProblems(event.target.checked ? [...selectedProblems, problem.id] : selectedProblems.filter((id) => id !== problem.id))} />}</td><td><strong>{problem.title}</strong></td><td>{problem.judgeMode === 2 ? "函数判题" : "标准输入输出"}</td><td>{languageMaskLabel(joined?.allowedLanguagesMask ?? problem.allowedLanguagesMask)}</td><td><strong>{joined?.baseScore ?? problem.totalScore} 分</strong></td><td><span className={`season-row-status ${joined ? "enabled" : ""}`}>{joined ? "已加入" : "未加入"}</span></td><td><div className="table-actions"><Link to={`/problems/${problem.id}`}>查看题目</Link>{isRoot && scheduled && (joined ? <button type="button" onClick={() => void run(() => removeLeaderboardSeasonProblems(currentSeason.id, [problem.id]), "题目已移出赛季")}>移出赛季</button> : <button type="button" onClick={() => void run(() => addLeaderboardSeasonProblems(currentSeason.id, [problem.id]), "题目已加入赛季")}>加入赛季</button>)}</div></td></tr>; })}</tbody></table></div>}</WorkspaceSection>

      <WorkspaceSection title="奖励与性能基准" summary={rewardSummary(currentSeason)} open={openSections.rewards} onToggle={() => toggleSection("rewards", setOpenSections)}><div className="season-reward-toggles"><SettingToggle label="抢先奖励" checked={currentSeason.scoringRules.firstCompletionBonusEnabled} disabled={!isRoot || !scheduled || isBusy} onChange={(checked) => updateSeasonSettings({ firstCompletionBonusEnabled: checked }, "抢先奖励设置已更新")} /><SettingToggle label="运行时间奖励" checked={currentSeason.scoringRules.runtimeBonusEnabled} disabled={!isRoot || !scheduled || isBusy} onChange={(checked) => updateSeasonSettings({ runtimeBonusEnabled: checked }, "运行时间奖励设置已更新")} /><SettingToggle label="内存奖励" checked={currentSeason.scoringRules.memoryBonusEnabled} disabled={!isRoot || !scheduled || isBusy} onChange={(checked) => updateSeasonSettings({ memoryBonusEnabled: checked }, "内存奖励设置已更新")} /></div>{!currentSeason.scoringRules.runtimeBonusEnabled && !currentSeason.scoringRules.memoryBonusEnabled ? <div className="compact-empty">未启用额外性能奖励</div> : <BenchmarkTable season={currentSeason} canEdit={Boolean(isRoot && scheduled)} onEdit={setBenchmarkEditor} />}</WorkspaceSection>
    </> : <div className="empty-state">当前没有赛季</div>}

    <AuditSection title="当前赛季审计榜" summary="ProblemSetter 与 Root 可查看匿名用户真实身份">{!leaderboard?.season || leaderboard.entries.length === 0 ? <div className="compact-empty">暂无赛季榜数据</div> : <div className="table-wrap"><table className="season-compact-table"><thead><tr><th>排名</th><th>用户</th><th>Alias</th><th>完成题目</th><th>总分</th></tr></thead><tbody>{leaderboard.entries.map((entry) => <tr key={`${entry.rank}-${entry.alias}`}><td>{entry.rank}</td><td>{entry.userName ?? entry.displayName}</td><td>{entry.alias}</td><td>{entry.solvedCount}</td><td>{entry.totalScore}</td></tr>)}</tbody></table></div>}</AuditSection>
    <AuditSection title="历史赛季" summary="管理端只读审计归档身份快照">{history.length === 0 ? <div className="compact-empty">暂无已归档赛季</div> : <div className="season-history-compact">{history.map((season) => <Link to={`/leaderboards/history/${season.seasonId}`} key={season.seasonId}><strong>{season.name}</strong><span>{season.participantCount} 人 · 冠军 {season.top3[0]?.displayName ?? "—"} · {season.top3[0]?.finalScore ?? 0} 分</span></Link>)}</div>}</AuditSection>
    {benchmarkEditor && currentSeason && <BenchmarkModal season={currentSeason} editor={benchmarkEditor} busy={isBusy} onClose={() => setBenchmarkEditor(null)} onSave={(runtime, memory) => void run(() => updateLeaderboardSeasonProblemBenchmark(currentSeason.id, benchmarkEditor.problem.problemId, benchmarkEditor.language, runtime, memory), `${benchmarkEditor.problem.problemTitle} 基准已保存`).then(() => setBenchmarkEditor(null))} />}
  </section>;
}

function WorkspaceSection({ title, summary, open, onToggle, children }: { title: string; summary: string; open: boolean; onToggle: () => void; children: ReactNode }) { return <section className={`workspace-section ${open ? "open" : ""}`}><button className="workspace-section-header" type="button" aria-expanded={open} onClick={onToggle}><div><h2>{title}</h2><span>{summary}</span></div><b aria-hidden="true">{open ? "−" : "+"}</b></button>{open && <div className="workspace-section-body">{children}</div>}</section>; }
function AuditSection({ title, summary, children }: { title: string; summary: string; children: ReactNode }) { return <section className="workspace-section audit-section"><div className="workspace-section-header static"><div><h2>{title}</h2><span>{summary}</span></div></div><div className="workspace-section-body">{children}</div></section>; }
function SettingToggle({ label, checked, disabled, onChange }: { label: string; checked: boolean; disabled: boolean; onChange: (checked: boolean) => void }) { return <label className="season-setting-row"><span>{label}</span><input type="checkbox" role="switch" checked={checked} disabled={disabled} onChange={(event) => onChange(event.target.checked)} /></label>; }

function SeasonEditorModal({ form, setForm, isBusy, isEdit, onClose, onSubmit }: { form: ReturnType<typeof defaultForm>; setForm: (value: ReturnType<typeof defaultForm>) => void; isBusy: boolean; isEdit: boolean; onClose: () => void; onSubmit: (event: FormEvent) => void }) { return <div className="season-editor-backdrop" role="presentation"><form className="admin-panel season-editor-modal compact" onSubmit={onSubmit}><div className="admin-panel-header"><div><p className="eyebrow">SEASON</p><h2>{isEdit ? "编辑赛季" : "创建赛季"}</h2></div></div><label>名称<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required /></label><div className="season-editor-time-row"><label>开始时间<input type="datetime-local" value={form.startAt} onChange={(event) => setForm({ ...form, startAt: event.target.value })} required /></label><label>冻结时间<input type="datetime-local" value={form.freezeAt} onChange={(event) => setForm({ ...form, freezeAt: event.target.value })} required /></label></div><label>公示结束<input type="datetime-local" value={form.publicUntil} onChange={(event) => setForm({ ...form, publicUntil: event.target.value })} required /></label><label className="season-setting-row modal-toggle"><span><strong>默认榜单</strong><small>全局榜</small></span><input type="checkbox" role="switch" checked={form.includeGlobalBoard} onChange={(event) => setForm({ ...form, includeGlobalBoard: event.target.checked })} /></label><p className="muted">创建完成后可在赛季详情配置挑战榜与奖励。</p><div className="season-modal-actions"><button className="button" type="button" onClick={onClose}>取消</button><button className="button primary" disabled={isBusy}>{isEdit ? "保存赛季" : "创建赛季"}</button></div></form></div>; }

function BenchmarkTable({ season, canEdit, onEdit }: { season: LeaderboardSeason; canEdit: boolean; onEdit: (editor: BenchmarkEditor) => void }) { const rows = season.problems.flatMap((problem) => allowedLanguages(problem.allowedLanguagesMask).map((language) => ({ problem, language, benchmark: problem.benchmarks.find((item) => item.language === language) }))); return rows.length === 0 ? <div className="compact-empty">加入题目后可配置性能基准</div> : <div className="table-wrap"><table className="season-compact-table benchmark-compact-table"><thead><tr><th>题目</th><th>语言</th>{season.scoringRules.runtimeBonusEnabled && <th>运行基准</th>}{season.scoringRules.memoryBonusEnabled && <th>内存基准</th>}<th>状态</th><th>操作</th></tr></thead><tbody>{rows.map(({ problem, language, benchmark }) => { const configured = (!season.scoringRules.runtimeBonusEnabled || Boolean(benchmark?.runtimeBaselineMs)) && (!season.scoringRules.memoryBonusEnabled || Boolean(benchmark?.memoryBaselineKb)); return <tr key={`${problem.problemId}-${language}`}><td>{problem.problemTitle}</td><td>{languageLabel(language)}</td>{season.scoringRules.runtimeBonusEnabled && <td>{benchmark?.runtimeBaselineMs ? `${benchmark.runtimeBaselineMs} ms` : "—"}</td>}{season.scoringRules.memoryBonusEnabled && <td>{benchmark?.memoryBaselineKb ? `${benchmark.memoryBaselineKb} KB` : "—"}</td>}<td><span className={`season-row-status ${configured ? "enabled" : ""}`}>{configured ? "已设置" : "待设置"}</span></td><td>{canEdit ? <button className="text-action" type="button" onClick={() => onEdit({ problem, language })}>编辑</button> : "—"}</td></tr>; })}</tbody></table></div>; }
function BenchmarkModal({ season, editor, busy, onClose, onSave }: { season: LeaderboardSeason; editor: NonNullable<BenchmarkEditor>; busy: boolean; onClose: () => void; onSave: (runtime: number | null, memory: number | null) => void }) { const benchmark = editor.problem.benchmarks.find((item) => item.language === editor.language); const [runtime, setRuntime] = useState(benchmark?.runtimeBaselineMs ?? 0); const [memory, setMemory] = useState(benchmark?.memoryBaselineKb ?? 0); const valid = (!season.scoringRules.runtimeBonusEnabled || runtime > 0) && (!season.scoringRules.memoryBonusEnabled || memory > 0); return <div className="season-editor-backdrop" role="presentation"><div className="admin-panel benchmark-editor-modal"><div className="admin-panel-header"><div><p className="eyebrow">BENCHMARK</p><h2>{editor.problem.problemTitle} · {languageLabel(editor.language)}</h2></div></div>{season.scoringRules.runtimeBonusEnabled && <label>运行基准 ms<input type="number" min="1" value={runtime || ""} onChange={(event) => setRuntime(Number(event.target.value))} /></label>}{season.scoringRules.memoryBonusEnabled && <label>内存基准 KB<input type="number" min="1" value={memory || ""} onChange={(event) => setMemory(Number(event.target.value))} /></label>}<div className="season-modal-actions"><button className="button" type="button" onClick={onClose}>取消</button><button className="button primary" type="button" disabled={busy || !valid} onClick={() => onSave(season.scoringRules.runtimeBonusEnabled ? runtime : null, season.scoringRules.memoryBonusEnabled ? memory : null)}>保存</button></div></div></div>; }

function toggleSection(key: SectionKey, setOpen: Dispatch<SetStateAction<Record<SectionKey, boolean>>>) { setOpen((current) => ({ ...current, [key]: !current[key] })); }
function defaultForm() { const now = new Date(); return { name: "", startAt: localValue(new Date(now.getTime() + 60 * 60_000)), freezeAt: localValue(new Date(now.getTime() + 25 * 60 * 60_000)), publicUntil: localValue(new Date(now.getTime() + 49 * 60 * 60_000)), includeGlobalBoard: true }; }
function toForm(season: LeaderboardSeason) { return { name: season.name, startAt: localValue(new Date(season.startAt)), freezeAt: localValue(new Date(season.freezeAt)), publicUntil: localValue(new Date(season.publicUntil)), includeGlobalBoard: season.boards.some((board) => board.boardType === 1) }; }
function localValue(value: Date) { const shifted = new Date(value.getTime() - value.getTimezoneOffset() * 60_000); return shifted.toISOString().slice(0, 16); }
function formatDate(value: string) { return new Intl.DateTimeFormat("zh-CN", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)); }
function statusLabel(status: number) { return ["", "Scheduled", "Active", "Frozen", "Public", "Archived"][status] ?? "Unknown"; }
function challengeStatus(challenge: ChallengeListItemDto) { const now = Date.now(); return !challenge.isPublished ? "未发布" : now < new Date(challenge.startAt).getTime() ? "未开始" : now <= new Date(challenge.endAt).getTime() ? "进行中" : "已结束"; }
function allowedLanguages(mask: number): LeaderboardJudgeLanguage[] { return ([1, 2, 3] as LeaderboardJudgeLanguage[]).filter((language) => mask === 0 || (mask & (language === 1 ? 1 : language === 2 ? 2 : 4)) !== 0); }
function languageLabel(language: LeaderboardJudgeLanguage) { return language === 1 ? "C++17" : language === 2 ? "C11" : "C#"; }
function languageMaskLabel(mask: number) { return allowedLanguages(mask).map(languageLabel).join(" / "); }
function rewardSummary(season: LeaderboardSeason) { const enabled = [season.scoringRules.firstCompletionBonusEnabled && "抢先", season.scoringRules.runtimeBonusEnabled && "运行", season.scoringRules.memoryBonusEnabled && "内存"].filter(Boolean); return enabled.length > 0 ? `${enabled.join(" / ")} 奖励已启用` : "未启用额外奖励"; }
