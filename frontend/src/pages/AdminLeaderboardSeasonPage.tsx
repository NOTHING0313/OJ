import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import {
  addLeaderboardSeasonProblem,
  archiveLeaderboardSeason,
  createLeaderboardSeason,
  finalizeLeaderboardSeason,
  freezeLeaderboardSeason,
  getAdminLeaderboardSeasons,
  getCurrentSeasonAuditLeaderboard,
  removeLeaderboardSeasonProblem,
  updateLeaderboardSeasonProblemBenchmark,
  updateLeaderboardSeason,
  type LeaderboardJudgeLanguage,
  type LeaderboardSeasonProblem,
  type LeaderboardSeason,
  type SeasonLeaderboard
} from "../api/leaderboardsApi";
import { getProblems, type ProblemListItemDto } from "../api/problemsApi";
import { useAuth } from "../auth/AuthContext";

export function AdminLeaderboardSeasonPage() {
  const { currentUser } = useAuth();
  const isRoot = currentUser?.role === 3;
  const [seasons, setSeasons] = useState<LeaderboardSeason[]>([]);
  const [leaderboard, setLeaderboard] = useState<SeasonLeaderboard | null>(null);
  const [problems, setProblems] = useState<ProblemListItemDto[]>([]);
  const [selectedProblemId, setSelectedProblemId] = useState("");
  const [form, setForm] = useState(() => defaultForm());
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  const currentSeason = useMemo(() => seasons.find((season) => season.isCurrent) ?? null, [seasons]);

  const reload = useCallback(async () => {
    try {
      const [seasonData, leaderboardData, problemData] = await Promise.all([
        getAdminLeaderboardSeasons(),
        getCurrentSeasonAuditLeaderboard(),
        getProblems()
      ]);
      setSeasons(seasonData);
      setLeaderboard(leaderboardData);
      setProblems(problemData);
      const current = seasonData.find((season) => season.isCurrent);
      if (current?.effectiveStatus === 1) setForm(toForm(current));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "赛季管理数据加载失败");
    }
  }, []);

  useEffect(() => { void reload(); }, [reload]);

  async function run(action: () => Promise<unknown>, message: string) {
    setIsBusy(true);
    setError(null);
    setNotice(null);
    try {
      await action();
      await reload();
      setNotice(message);
    } catch (err) {
      setError(err instanceof Error ? err.message : "操作失败");
    } finally {
      setIsBusy(false);
    }
  }

  function submitSeason(event: FormEvent) {
    event.preventDefault();
    const payload = {
      name: form.name.trim(),
      startAt: new Date(form.startAt).toISOString(),
      freezeAt: new Date(form.freezeAt).toISOString(),
      publicUntil: new Date(form.publicUntil).toISOString()
    };
    void run(
      () => currentSeason ? updateLeaderboardSeason(currentSeason.id, payload) : createLeaderboardSeason(payload),
      currentSeason ? "赛季配置已更新" : "赛季已创建"
    );
  }

  return (
    <section className="admin-page leaderboard-season-admin-page">
      <div className="page-header ui-v2-page-header">
        <div><p className="eyebrow">LEADERBOARD ADMIN</p><h1>赛季榜管理</h1><p>配置赛季生命周期、计分题目并审计真实身份榜单。</p></div>
      </div>
      {error && <div className="alert error">{error}</div>}
      {notice && <div className="quiet-note success">{notice}</div>}

      {isRoot && (!currentSeason || currentSeason.effectiveStatus === 1) && (
        <form className="admin-panel leaderboard-season-form" onSubmit={submitSeason}>
          <h2>{currentSeason ? "编辑 Scheduled 赛季" : "创建赛季"}</h2>
          <label>名称<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required /></label>
          <label>开始时间<input type="datetime-local" value={form.startAt} onChange={(event) => setForm({ ...form, startAt: event.target.value })} required /></label>
          <label>冻结时间<input type="datetime-local" value={form.freezeAt} onChange={(event) => setForm({ ...form, freezeAt: event.target.value })} required /></label>
          <label>公示结束<input type="datetime-local" value={form.publicUntil} onChange={(event) => setForm({ ...form, publicUntil: event.target.value })} required /></label>
          <button className="button primary" disabled={isBusy}>{currentSeason ? "保存赛季" : "创建赛季"}</button>
        </form>
      )}

      {currentSeason && (
        <section className="admin-panel leaderboard-season-current-card">
          <div className="admin-panel-header"><div><h2>{currentSeason.name}</h2><p>{statusLabel(currentSeason.effectiveStatus)}</p></div></div>
          <p>{formatDate(currentSeason.startAt)} / {formatDate(currentSeason.freezeAt)} / {formatDate(currentSeason.publicUntil)}</p>
          {isRoot && currentSeason.effectiveStatus === 1 && (
            <div className="leaderboard-season-problem-editor">
              <select value={selectedProblemId} onChange={(event) => setSelectedProblemId(event.target.value)}>
                <option value="">选择题目</option>
                {problems.filter((problem) => !currentSeason.problems.some((item) => item.problemId === problem.id)).map((problem) => (
                  <option key={problem.id} value={problem.id}>{problem.title}</option>
                ))}
              </select>
              <button className="button" disabled={!selectedProblemId || isBusy} onClick={() => void run(
                () => addLeaderboardSeasonProblem(currentSeason.id, selectedProblemId), "题目已加入赛季")}>加入题目</button>
            </div>
          )}
          <div className="leaderboard-season-problem-list">
            {currentSeason.problems.map((problem) => (
              <div className="leaderboard-season-problem-card" key={problem.id}>
                <div className="leaderboard-season-problem-heading"><span>{problem.problemTitle}</span><strong>{problem.baseScore} 分</strong>
                  {isRoot && currentSeason.effectiveStatus === 1 && <button className="button" disabled={isBusy} onClick={() => void run(
                    () => removeLeaderboardSeasonProblem(currentSeason.id, problem.problemId), "题目已移出赛季")}>移除</button>}
                </div>
                <div className="leaderboard-benchmark-grid">
                  {allowedLanguages(problem.allowedLanguagesMask).map((language) => (
                    <BenchmarkEditor
                      key={`${problem.id}-${language}-${problem.benchmarks.find((item) => item.language === language)?.runtimeBaselineMs ?? 0}-${problem.benchmarks.find((item) => item.language === language)?.memoryBaselineKb ?? 0}`}
                      problem={problem}
                      language={language}
                      editable={currentSeason.effectiveStatus === 1}
                      disabled={isBusy}
                      onSave={(runtime, memory) => run(
                        () => updateLeaderboardSeasonProblemBenchmark(currentSeason.id, problem.problemId, language, runtime, memory),
                        `${problem.problemTitle} ${languageLabel(language)} 基准已保存`)}
                    />
                  ))}
                </div>
              </div>
            ))}
          </div>
          {isRoot && (
            <div className="leaderboard-season-actions">
              {currentSeason.effectiveStatus === 3 && currentSeason.status !== 4 && <button className="button" disabled={isBusy} onClick={() => void run(
                () => freezeLeaderboardSeason(currentSeason.id), "赛季已冻结")}>确认冻结</button>}
              {(currentSeason.effectiveStatus === 3 || currentSeason.status === 4) && <button className="button primary" disabled={isBusy} onClick={() => void run(
                () => finalizeLeaderboardSeason(currentSeason.id), "最终榜快照已生成")}>Finalize / Public</button>}
              {currentSeason.status === 4 && <button className="button danger" disabled={isBusy} onClick={() => void run(
                () => archiveLeaderboardSeason(currentSeason.id), "赛季已归档")}>Archive</button>}
            </div>
          )}
        </section>
      )}

      <section className="admin-panel">
        <div className="admin-panel-header"><div><h2>当前赛季审计榜</h2><p>ProblemSetter 与 Root 可查看匿名用户真实身份。</p></div></div>
        {!leaderboard?.season || leaderboard.entries.length === 0 ? <div className="empty-state">暂无赛季榜数据</div> : (
          <div className="table-wrap"><table className="leaderboard-table"><thead><tr><th>排名</th><th>用户</th><th>Alias</th><th>完成题目</th><th>总分</th></tr></thead>
            <tbody>{leaderboard.entries.map((entry) => <tr key={`${entry.rank}-${entry.alias}`}><td>{entry.rank}</td><td>{entry.userName ?? entry.displayName}</td><td>{entry.alias}</td><td>{entry.solvedCount}</td><td>{entry.totalScore}</td></tr>)}</tbody>
          </table></div>
        )}
      </section>
    </section>
  );
}

function defaultForm() {
  const now = new Date();
  return { name: "", startAt: localValue(new Date(now.getTime() + 60 * 60_000)), freezeAt: localValue(new Date(now.getTime() + 25 * 60 * 60_000)), publicUntil: localValue(new Date(now.getTime() + 49 * 60 * 60_000)) };
}

function toForm(season: LeaderboardSeason) {
  return { name: season.name, startAt: localValue(new Date(season.startAt)), freezeAt: localValue(new Date(season.freezeAt)), publicUntil: localValue(new Date(season.publicUntil)) };
}

function localValue(value: Date) {
  const shifted = new Date(value.getTime() - value.getTimezoneOffset() * 60_000);
  return shifted.toISOString().slice(0, 16);
}

function formatDate(value: string) { return new Intl.DateTimeFormat("zh-CN", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)); }
function statusLabel(status: number) { return ["", "Scheduled", "Active", "Frozen", "Public", "Archived"][status] ?? "Unknown"; }

function BenchmarkEditor({ problem, language, editable, disabled, onSave }: {
  problem: LeaderboardSeasonProblem;
  language: LeaderboardJudgeLanguage;
  editable: boolean;
  disabled: boolean;
  onSave: (runtime: number, memory: number) => Promise<void>;
}) {
  const benchmark = problem.benchmarks.find((item) => item.language === language);
  const [runtime, setRuntime] = useState(benchmark?.runtimeBaselineMs ?? 0);
  const [memory, setMemory] = useState(benchmark?.memoryBaselineKb ?? 0);
  return (
    <div className="leaderboard-benchmark-card">
      <strong>{languageLabel(language)}</strong>
      <label>Runtime baseline (ms)<input type="number" min="1" value={runtime || ""} readOnly={!editable} onChange={(event) => setRuntime(Number(event.target.value))} /></label>
      <label>Memory baseline (KB)<input type="number" min="1" value={memory || ""} readOnly={!editable} onChange={(event) => setMemory(Number(event.target.value))} /></label>
      {editable && <button className="button" type="button" disabled={disabled || runtime <= 0 || memory <= 0} onClick={() => void onSave(runtime, memory)}>保存基准</button>}
    </div>
  );
}

function allowedLanguages(mask: number): LeaderboardJudgeLanguage[] {
  return ([1, 2, 3] as LeaderboardJudgeLanguage[]).filter((language) => mask === 0 || (mask & (language === 1 ? 1 : language === 2 ? 2 : 4)) !== 0);
}

function languageLabel(language: LeaderboardJudgeLanguage) {
  return language === 1 ? "C++17" : language === 2 ? "C11" : "C#";
}
