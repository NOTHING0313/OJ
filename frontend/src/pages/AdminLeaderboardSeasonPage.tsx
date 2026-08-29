import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  addLeaderboardSeasonProblems,
  archiveLeaderboardSeason,
  createLeaderboardSeason,
  finalizeLeaderboardSeason,
  freezeLeaderboardSeason,
  getAdminLeaderboardSeasons,
  getCurrentSeasonAuditLeaderboard,
  getLeaderboardSeasonHistory,
  removeLeaderboardSeasonProblems,
  updateLeaderboardSeasonProblem,
  updateLeaderboardSeasonProblemBenchmark,
  updateLeaderboardSeason,
  type LeaderboardJudgeLanguage,
  type LeaderboardSeason,
  type LeaderboardSeasonHistorySummary,
  type LeaderboardSeasonProblem,
  type SeasonLeaderboard
} from "../api/leaderboardsApi";
import { getChallenges, type ChallengeListItemDto } from "../api/challengesApi";
import { getProblems, type ProblemListItemDto } from "../api/problemsApi";
import { useAuth } from "../auth/AuthContext";

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
  const [form, setForm] = useState(() => defaultForm());
  const [showEditor, setShowEditor] = useState(false);
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
      setSeasons(seasonData); setLeaderboard(leaderboardData); setProblems(problemData); setChallenges(challengeData); setHistory(historyData);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "榜单管理数据加载失败");
    }
  }, []);

  useEffect(() => { void reload(); }, [reload]);

  async function run(action: () => Promise<unknown>, message: string) {
    setIsBusy(true); setError(null); setNotice(null);
    try { await action(); await reload(); setSelectedProblems([]); setNotice(message); }
    catch (err) { setError(err instanceof Error ? err.message : "操作失败"); }
    finally { setIsBusy(false); }
  }

  function openEditor() {
    setForm(currentSeason && scheduled ? toForm(currentSeason) : defaultForm());
    setShowEditor(true);
  }

  function submitSeason(event: FormEvent) {
    event.preventDefault();
    const payload = {
      name: form.name.trim(), startAt: new Date(form.startAt).toISOString(), freezeAt: new Date(form.freezeAt).toISOString(),
      publicUntil: new Date(form.publicUntil).toISOString(), includeGlobalBoard: form.includeGlobalBoard,
      challengeIds: form.challengeIds, firstCompletionBonusEnabled: form.firstCompletionBonusEnabled,
      runtimeBonusEnabled: form.runtimeBonusEnabled, memoryBonusEnabled: form.memoryBonusEnabled
    };
    void run(() => currentSeason ? updateLeaderboardSeason(currentSeason.id, payload) : createLeaderboardSeason(payload), currentSeason ? "赛季配置已更新" : "赛季已创建")
      .then(() => setShowEditor(false));
  }

  const availableProblems = problems.filter((problem) => !currentSeason?.problems.some((item) => item.problemId === problem.id));
  const filteredProblems = availableProblems.filter((problem) => problem.title.toLocaleLowerCase().includes(problemSearch.trim().toLocaleLowerCase()));

  return <section className="admin-page leaderboard-season-admin-page leaderboard-management-v2">
    <div className="page-header ui-v2-page-header"><div><p className="eyebrow">LEADERBOARD MANAGEMENT</p><h1>榜单管理</h1><p>管理当前赛季、公开榜单、计分题目与归档审计。</p></div>
      {isRoot && (!currentSeason || scheduled) && <button className="button primary" type="button" onClick={openEditor}>{currentSeason ? "编辑赛季" : "创建赛季"}</button>}
    </div>
    {error && <div className="alert error">{error}</div>}{notice && <div className="quiet-note success">{notice}</div>}

    {showEditor && <div className="season-editor-backdrop" role="presentation"><form className="admin-panel season-editor-modal" onSubmit={submitSeason}>
      <div className="admin-panel-header"><div><h2>{currentSeason ? "编辑赛季" : "创建赛季"}</h2><p>仅 Scheduled 阶段可修改，开始后自动冻结配置。</p></div><button className="button" type="button" onClick={() => setShowEditor(false)}>关闭</button></div>
      <div className="season-editor-fields"><label>名称<input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required /></label><label>开始时间<input type="datetime-local" value={form.startAt} onChange={(e) => setForm({ ...form, startAt: e.target.value })} required /></label><label>冻结时间<input type="datetime-local" value={form.freezeAt} onChange={(e) => setForm({ ...form, freezeAt: e.target.value })} required /></label><label>公示结束<input type="datetime-local" value={form.publicUntil} onChange={(e) => setForm({ ...form, publicUntil: e.target.value })} required /></label></div>
      <fieldset><legend>公开榜单</legend><label className="checkbox-line"><input type="checkbox" checked={form.includeGlobalBoard} onChange={(e) => setForm({ ...form, includeGlobalBoard: e.target.checked })} />全局赛季榜</label>
        <div className="season-board-options">{challenges.filter((challenge) => new Date(challenge.startAt) >= new Date(form.startAt) && new Date(challenge.endAt) <= new Date(form.freezeAt)).map((challenge) => <label className="checkbox-line" key={challenge.id}><input type="checkbox" checked={form.challengeIds.includes(challenge.id)} onChange={(e) => setForm({ ...form, challengeIds: e.target.checked ? [...form.challengeIds, challenge.id] : form.challengeIds.filter((id) => id !== challenge.id) })} />{challenge.title}</label>)}</div>
      </fieldset>
      <fieldset><legend>奖励规则</legend><div className="season-reward-toggles"><label className="checkbox-line"><input type="checkbox" checked={form.firstCompletionBonusEnabled} onChange={(e) => setForm({ ...form, firstCompletionBonusEnabled: e.target.checked })} />首次完成 / 时间奖励</label><label className="checkbox-line"><input type="checkbox" checked={form.runtimeBonusEnabled} onChange={(e) => setForm({ ...form, runtimeBonusEnabled: e.target.checked })} />运行时间奖励</label><label className="checkbox-line"><input type="checkbox" checked={form.memoryBonusEnabled} onChange={(e) => setForm({ ...form, memoryBonusEnabled: e.target.checked })} />内存奖励</label></div></fieldset>
      <button className="button primary" disabled={isBusy}>保存赛季</button>
    </form></div>}

    {currentSeason ? <section className="admin-panel season-summary-card"><div><span className={`season-status status-${currentSeason.effectiveStatus}`}>{statusLabel(currentSeason.effectiveStatus)}</span><h2>{currentSeason.name}</h2><p>{formatDate(currentSeason.startAt)} → {formatDate(currentSeason.freezeAt)} · 公示至 {formatDate(currentSeason.publicUntil)}</p></div><div className="season-summary-actions">{isRoot && scheduled && <Link className="button" to={`/admin/challenges/new?seasonId=${currentSeason.id}`}>创建并关联挑战</Link>}{isRoot && currentSeason.effectiveStatus === 2 && <button className="button" disabled={isBusy} onClick={() => void run(() => freezeLeaderboardSeason(currentSeason.id), "赛季已提前冻结")}>提前冻结</button>}{isRoot && (currentSeason.effectiveStatus === 3 || currentSeason.status === 4) && <button className="button primary" disabled={isBusy} onClick={() => void run(() => finalizeLeaderboardSeason(currentSeason.id), "最终榜快照已生成")}>定榜</button>}{isRoot && currentSeason.status === 4 && <button className="button danger" disabled={isBusy} onClick={() => void run(() => archiveLeaderboardSeason(currentSeason.id), "赛季已归档")}>归档</button>}</div>
      <div className="season-board-chips">{currentSeason.boards.length === 0 ? <span>未启用公开榜单</span> : currentSeason.boards.map((board) => <span key={board.id ?? `${board.boardType}-${board.challengeId}`}>{board.boardType === 1 ? "全局榜" : board.challengeTitle}</span>)}</div>
    </section> : <div className="empty-state">当前没有赛季</div>}

    {isRoot && currentSeason && scheduled && <section className="admin-panel season-problem-manager"><div className="admin-panel-header"><div><h2>赛季题目</h2><p>搜索并批量加入；已加入题目可批量移除或修改基础分。</p></div></div>
      <div className="season-problem-toolbar"><input placeholder="搜索题目标题" value={problemSearch} onChange={(e) => setProblemSearch(e.target.value)} /><button className="button" type="button" onClick={() => setSelectedProblems(filteredProblems.map((problem) => problem.id))}>选择筛选结果</button><button className="button" type="button" onClick={() => setSelectedProblems([])}>清空</button><button className="button primary" disabled={selectedProblems.length === 0 || isBusy} onClick={() => void run(() => addLeaderboardSeasonProblems(currentSeason.id, selectedProblems), "题目已批量加入")}>批量加入</button></div>
      <div className="season-problem-picker">{filteredProblems.map((problem) => <label className="checkbox-line" key={problem.id}><input type="checkbox" checked={selectedProblems.includes(problem.id)} onChange={(e) => setSelectedProblems(e.target.checked ? [...selectedProblems, problem.id] : selectedProblems.filter((id) => id !== problem.id))} />{problem.title}</label>)}</div>
      <div className="leaderboard-season-problem-list">{currentSeason.problems.map((problem) => <SeasonProblemRow key={problem.id} season={currentSeason} problem={problem} busy={isBusy} selected={selectedProblems.includes(problem.problemId)} onSelect={(checked) => setSelectedProblems(checked ? [...selectedProblems, problem.problemId] : selectedProblems.filter((id) => id !== problem.problemId))} run={run} />)}</div>
      <button className="button danger" disabled={!selectedProblems.some((id) => currentSeason.problems.some((problem) => problem.problemId === id)) || isBusy} onClick={() => void run(() => removeLeaderboardSeasonProblems(currentSeason.id, selectedProblems.filter((id) => currentSeason.problems.some((problem) => problem.problemId === id))), "题目已批量移除")}>批量移除已选题目</button>
    </section>}

    <section className="admin-panel"><div className="admin-panel-header"><div><h2>当前赛季审计榜</h2><p>ProblemSetter 与 Root 可查看匿名用户真实身份。</p></div></div>{!leaderboard?.season || leaderboard.entries.length === 0 ? <div className="empty-state">暂无赛季榜数据</div> : <div className="table-wrap"><table className="leaderboard-table"><thead><tr><th>排名</th><th>用户</th><th>Alias</th><th>完成题目</th><th>总分</th></tr></thead><tbody>{leaderboard.entries.map((entry) => <tr key={`${entry.rank}-${entry.alias}`}><td>{entry.rank}</td><td>{entry.userName ?? entry.displayName}</td><td>{entry.alias}</td><td>{entry.solvedCount}</td><td>{entry.totalScore}</td></tr>)}</tbody></table></div>}</section>
    <section className="admin-panel"><div className="admin-panel-header"><div><h2>历史赛季</h2><p>管理端只读审计归档身份快照。</p></div></div>{history.length === 0 ? <div className="empty-state">暂无已归档赛季</div> : <div className="season-history-compact">{history.map((season) => <Link to={`/leaderboards/history/${season.seasonId}`} key={season.seasonId}><strong>{season.name}</strong><span>{season.participantCount} 人 · 冠军 {season.top3[0]?.displayName ?? "—"} · {season.top3[0]?.finalScore ?? 0} 分</span></Link>)}</div>}</section>
  </section>;
}

function SeasonProblemRow({ season, problem, busy, selected, onSelect, run }: { season: LeaderboardSeason; problem: LeaderboardSeasonProblem; busy: boolean; selected: boolean; onSelect: (checked: boolean) => void; run: (action: () => Promise<unknown>, message: string) => Promise<void> }) {
  const [baseScore, setBaseScore] = useState(problem.baseScore);
  const showBenchmarks = season.scoringRules.runtimeBonusEnabled || season.scoringRules.memoryBonusEnabled;
  return <div className="leaderboard-season-problem-card"><div className="leaderboard-season-problem-heading"><input type="checkbox" checked={selected} onChange={(e) => onSelect(e.target.checked)} /><span>{problem.problemTitle}</span><input className="season-base-score" type="number" min="1" value={baseScore} onChange={(e) => setBaseScore(Number(e.target.value))} /><button className="button" disabled={busy || baseScore <= 0 || baseScore === problem.baseScore} onClick={() => void run(() => updateLeaderboardSeasonProblem(season.id, problem.problemId, baseScore), "基础分已更新")}>保存分数</button></div>{showBenchmarks && <div className="leaderboard-benchmark-table">{allowedLanguages(problem.allowedLanguagesMask).map((language) => <BenchmarkRow key={language} season={season} problem={problem} language={language} busy={busy} run={run} />)}</div>}</div>;
}

function BenchmarkRow({ season, problem, language, busy, run }: { season: LeaderboardSeason; problem: LeaderboardSeasonProblem; language: LeaderboardJudgeLanguage; busy: boolean; run: (action: () => Promise<unknown>, message: string) => Promise<void> }) {
  const benchmark = problem.benchmarks.find((item) => item.language === language); const [runtime, setRuntime] = useState(benchmark?.runtimeBaselineMs ?? 0); const [memory, setMemory] = useState(benchmark?.memoryBaselineKb ?? 0);
  const valid = (!season.scoringRules.runtimeBonusEnabled || runtime > 0) && (!season.scoringRules.memoryBonusEnabled || memory > 0);
  return <div><strong>{languageLabel(language)}</strong>{season.scoringRules.runtimeBonusEnabled && <label>运行基准 ms<input type="number" min="1" value={runtime || ""} onChange={(e) => setRuntime(Number(e.target.value))} /></label>}{season.scoringRules.memoryBonusEnabled && <label>内存基准 KB<input type="number" min="1" value={memory || ""} onChange={(e) => setMemory(Number(e.target.value))} /></label>}<button className="button" disabled={busy || !valid} onClick={() => void run(() => updateLeaderboardSeasonProblemBenchmark(season.id, problem.problemId, language, season.scoringRules.runtimeBonusEnabled ? runtime : null, season.scoringRules.memoryBonusEnabled ? memory : null), `${problem.problemTitle} 基准已保存`)}>保存</button></div>;
}

function defaultForm() { const now = new Date(); return { name: "", startAt: localValue(new Date(now.getTime() + 60 * 60_000)), freezeAt: localValue(new Date(now.getTime() + 25 * 60 * 60_000)), publicUntil: localValue(new Date(now.getTime() + 49 * 60 * 60_000)), includeGlobalBoard: true, challengeIds: [] as string[], firstCompletionBonusEnabled: true, runtimeBonusEnabled: true, memoryBonusEnabled: true }; }
function toForm(season: LeaderboardSeason) { return { name: season.name, startAt: localValue(new Date(season.startAt)), freezeAt: localValue(new Date(season.freezeAt)), publicUntil: localValue(new Date(season.publicUntil)), includeGlobalBoard: season.boards.some((board) => board.boardType === 1), challengeIds: season.boards.flatMap((board) => board.challengeId ? [board.challengeId] : []), firstCompletionBonusEnabled: season.scoringRules.firstCompletionBonusEnabled, runtimeBonusEnabled: season.scoringRules.runtimeBonusEnabled, memoryBonusEnabled: season.scoringRules.memoryBonusEnabled }; }
function localValue(value: Date) { const shifted = new Date(value.getTime() - value.getTimezoneOffset() * 60_000); return shifted.toISOString().slice(0, 16); }
function formatDate(value: string) { return new Intl.DateTimeFormat("zh-CN", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)); }
function statusLabel(status: number) { return ["", "Scheduled", "Active", "Frozen", "Public", "Archived"][status] ?? "Unknown"; }
function allowedLanguages(mask: number): LeaderboardJudgeLanguage[] { return ([1, 2, 3] as LeaderboardJudgeLanguage[]).filter((language) => mask === 0 || (mask & (language === 1 ? 1 : language === 2 ? 2 : 4)) !== 0); }
function languageLabel(language: LeaderboardJudgeLanguage) { return language === 1 ? "C++17" : language === 2 ? "C11" : "C#"; }
