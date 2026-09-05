import { act, StrictMode } from "react";
import { createRoot, type Root } from "react-dom/client";
import { Link, MemoryRouter, Route, Routes } from "react-router-dom";
import { AuthProvider } from "../src/auth/AuthContext";
import { ChallengeDetailPage } from "../src/pages/ChallengeDetailPage";
import { SubmissionDetailPage } from "../src/pages/SubmissionDetailPage";
import { problemDraftKey } from "../src/utils/problemDrafts";
import type { ChallengeDetailDto } from "../src/api/challengesApi";
import type { SubmissionDto } from "../src/api/submissionsApi";
import "../src/styles.css";

Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
const fixture = document.getElementById("fixture")!;
const values = new Map<string, string>();
const storage = Object.getOwnPropertyDescriptor(window, "localStorage")!;
Object.defineProperty(window, "localStorage", { configurable: true, value: {
  getItem: (key: string) => values.get(key) ?? null,
  setItem: (key: string, value: string) => values.set(key, value),
  removeItem: (key: string) => values.delete(key)
} });
const originalFetch = window.fetch;
let calls = 0, offline = false;
let completed = false;
let fileTask = false;
let latestStatus: 1 | 2 | 3 | 4 | 9 | null = null;
let userId = "flow-user";
let pending: (() => Promise<Response>) | undefined;
const json = (value: unknown) => new Response(JSON.stringify(value), { headers: { "Content-Type": "application/json" } });
const detail = (): ChallengeDetailDto => ({
  id: "c", title: "测试挑战", description: "", startAt: "2026-01-01", endAt: "2027-01-01",
  createdByUserId: "admin", isPublished: true, participationMode: 1, participationModeLocked: false,
  peerReviewEnabled: false, peerReviewEndAt: null, peerReviewConfigurationLocked: false,
  createdAt: "2026-01-01", updatedAt: "2026-01-01", totalTaskCount: 2,
  completedTaskCount: completed ? 1 : 0, canManage: false, teamParticipation: null,
  tasks: ["first", "second"].map((id, index) => ({
    id, challengeId: "c", title: id, description: "", taskType: fileTask && index === 1 ? 2 : 1, difficulty: 1,
    boardX: index, boardY: 0, algorithmProblemId: "p", score: 10, isPublished: true,
    createdAt: "2026-01-01", updatedAt: "2026-01-01", isCompleted: completed && index === 1,
    completedAt: null, completedScore: completed && index === 1 ? 10 : null,
    earnedScore: completed && index === 1 ? 10 : 0
    ,myLatestSubmissionStatus: index === 1 ? latestStatus : null,
    algorithmProblemDifficulty: index === 1 ? 3 : 1
  }))
});
const submission = {
  id: "s", problemId: "p", problemTitle: "题目", userId: "flow-user", userName: "Test",
  challengeTaskId: "second", submissionKind: 1, language: 1, status: 4,
  createdAt: "2026-01-01", finishedAt: "2026-01-01", caseResults: [], choiceQuestionResults: [], evaluation: { maxTimeUsedMs: null, averageCaseTimeUsedMs: null, maxMemoryUsedKb: null, averageCaseMemoryUsedKb: null }
} as SubmissionDto;
window.fetch = async input => {
  const url = String(input);
  if (url === "/api/auth/me") return json({ id: userId, role: 1, userName: "Test" });
  if (url.endsWith("/join")) return new Response(null, { status: 204 });
  if (url === "/api/submissions/s") return json(submission);
  if (url === "/api/challenges/c") {
    calls++;
    if (offline) throw new TypeError("offline");
    if (pending) return pending();
    return json(detail());
  }
  throw new Error(`Unexpected request ${url}`);
};
let root: Root | undefined;
const assert = (value: unknown, message: string) => { if (!value) throw new Error(message); };
async function flush(ms = 30) { await act(async () => { await new Promise(resolve => setTimeout(resolve, ms)); }); }
async function unmount() { if (root) { const old = root; root = undefined; await act(async () => old.unmount()); } }
async function mount(path = "/challenges/c", state: unknown = null) {
  await unmount();
  await act(async () => {
    root = createRoot(fixture);
    root.render(<StrictMode><MemoryRouter initialEntries={[{ pathname: path.split("?")[0], search: path.includes("?") ? `?${path.split("?")[1]}` : "", state }]}><AuthProvider><Routes>
      <Route path="/challenges/:id" element={<ChallengeDetailPage />} />
      <Route path="/submissions/:id" element={<SubmissionDetailPage />} />
      <Route path="/problems/:id" element={<Link to="/challenges/c">返回棋盘</Link>} />
    </Routes></AuthProvider></MemoryRouter></StrictMode>);
  });
  await flush();
  for (let attempt = 0; attempt < 30 && !fixture.querySelector(path.startsWith("/submissions") ? ".detail-grid" : ".challenge-board"); attempt++) await flush();
}
async function click(element: HTMLElement | undefined | null) { assert(element, "控件不存在"); await act(async () => element!.click()); await flush(); }
const selected = () => fixture.querySelector(".selected-task h2")?.textContent;
async function event(name: string) { await act(async () => { window.dispatchEvent(new Event(name)); }); await flush(); }
const cases: Array<[string, () => Promise<void>]> = [
  ["阶段随时间更新，未开始/结束禁用作答，起止边界允许作答", async () => {
    const originalNow = Date.now;
    let clock = Date.parse("2025-12-31T00:00:00Z");
    Date.now = () => clock;
    try {
      await mount();
      assert(fixture.querySelector(".challenge-phase")?.textContent === "未开始", "未开始阶段错误");
      await click(fixture.querySelectorAll<HTMLButtonElement>(".board-cell:not(:disabled)")[1]);
      assert(fixture.querySelector(".challenge-board"), "未开始仍直接跳转");
      assert(fixture.querySelector<HTMLButtonElement>(".selected-task button")?.disabled, "未开始可作答");
      clock = Date.parse("2026-01-01T00:00:00Z"); await flush(1100);
      assert(fixture.querySelector(".challenge-phase")?.textContent === "进行中", "开始时间未自动切换");
      assert(!fixture.querySelector<HTMLButtonElement>(".selected-task button")?.disabled, "开始边界禁用");
      clock = Date.parse("2027-01-01T00:00:00Z"); await event("focus");
      assert(!fixture.querySelector<HTMLButtonElement>(".selected-task button")?.disabled, "截止边界应允许");
      clock++; await event("focus");
      assert(fixture.querySelector(".challenge-phase")?.textContent === "已结束", "结束阶段错误");
      assert(fixture.querySelector<HTMLButtonElement>(".selected-task button")?.disabled, "结束后可作答");
    } finally { Date.now = originalNow; }
  }],
  ["手机点棋子先展示详情，再点开始作答；状态与难度色正确", async () => {
    const originalMatchMedia = window.matchMedia;
    window.matchMedia = (query: string) => query.includes("max-width: 760px") ? { ...originalMatchMedia(query), matches: true } as MediaQueryList : originalMatchMedia(query);
    try {
      latestStatus = 1; await mount();
      const second = fixture.querySelectorAll<HTMLButtonElement>(".board-cell:not(:disabled)")[1];
      assert(second.getAttribute("aria-label")?.includes("排队中"), "无障碍状态缺失");
      await click(second);
      assert(fixture.querySelector(".challenge-board"), "手机点棋子直接跳转了");
      assert(selected() === "second", "手机未选中详情");
      assert(getComputedStyle(fixture.querySelector(".challenge-piece-kind")!).color === "rgb(248, 113, 113)", "难度红色失效");
      latestStatus = 2; await event("focus"); assert(fixture.querySelector(".selected-task-facts")?.textContent?.includes("判题中"), "判题状态未更新");
      latestStatus = 4; await event("focus"); assert(fixture.querySelector(".selected-task-facts")?.textContent?.includes("未通过"), "失败状态未更新");
      completed = true; await event("focus"); assert(fixture.querySelector(".selected-task-facts")?.textContent?.includes("已完成"), "再次失败覆盖历史完成");
      await click([...fixture.querySelectorAll<HTMLButtonElement>("button")].find(button => button.textContent === "开始作答"));
      assert(!fixture.querySelector(".challenge-board") && fixture.querySelector("a")?.textContent === "返回棋盘", "开始作答未进入任务");
    } finally { window.matchMedia = originalMatchMedia; latestStatus = null; completed = false; }
  }],
  ["失败结果返回题目保留挑战和棋子；无挑战 URL 时也保留任务归属", async () => {
    await mount("/submissions/s?challengeId=c");
    let link = [...fixture.querySelectorAll("a")].find(a => a.textContent === "返回题目")!;
    assert(link, `结果未加载：${fixture.textContent}`);
    assert(link.getAttribute("href") === "/problems/p?taskId=second&challengeId=c", "挑战上下文丢失");
    await mount("/submissions/s");
    link = [...fixture.querySelectorAll("a")].find(a => a.textContent === "返回题目")!;
    assert(link.getAttribute("href") === "/problems/p?taskId=second", "提交自身的任务归属丢失");
  }],
  ["普通返回与重新进入恢复第二颗棋子，其他账号不继承", async () => {
    await mount();
    await click(fixture.querySelectorAll<HTMLButtonElement>(".board-cell:not(:disabled)")[1]);
    await click(fixture.querySelector("a"));
    assert(selected() === "second", "返回后未恢复棋子");
    await mount(); assert(selected() === "second", "重新进入未恢复棋子");
    userId = "other-user"; await mount(); assert(selected() === "first", "账号间位置串用"); userId = "flow-user";
  }],
  ["提前返回棋盘后自动更新完成状态和得分", async () => {
    await mount(); completed = true; await flush(5200);
    assert(fixture.querySelector(".selected-task-facts")?.textContent?.includes("10 / 10"), "自动刷新未更新得分");
    assert(fixture.querySelector(".selected-task-facts")?.textContent?.includes("已完成"), "棋子状态未更新");
  }],
  ["断网保留棋盘并提示，联网立即恢复；聚焦也会刷新", async () => {
    offline = true; await event("focus");
    assert(fixture.querySelector(".challenge-board"), "断网清空棋盘");
    assert(fixture.textContent?.includes("进度暂未更新"), "断网缺少提示");
    offline = false; completed = false; await event("online");
    assert(!fixture.textContent?.includes("进度暂未更新"), "联网未清除错误");
    const before = calls; await event("focus"); assert(calls > before, "聚焦未刷新");
  }],
  ["并发刷新不重复请求，离开后迟到响应不更新页面或续轮询", async () => {
    let resolve!: (response: Response) => void;
    pending = () => new Promise<Response>(r => { resolve = r; });
    await event("focus"); const before = calls; await event("online"); assert(calls === before, "重叠刷新请求");
    await unmount(); await act(async () => resolve(json(detail()))); pending = undefined;
    await flush(5200); assert(calls === before, "离开后仍然轮询"); assert(!fixture.textContent, "迟到响应渲染旧页面");
  }],
  ["后台暂停刷新，重新可见时刷新；成功提交仍定位完成棋子", async () => {
    await mount();
    const descriptor = Object.getOwnPropertyDescriptor(document, "visibilityState");
    try {
      Object.defineProperty(document, "visibilityState", { configurable: true, value: "hidden" });
      const before = calls; await flush(5200); assert(calls === before, "后台仍在轮询");
      Object.defineProperty(document, "visibilityState", { configurable: true, value: "visible" });
      await act(async () => document.dispatchEvent(new Event("visibilitychange"))); await flush();
      assert(calls > before, "重新可见未刷新");
    } finally {
      if (descriptor) Object.defineProperty(document, "visibilityState", descriptor);
      else Reflect.deleteProperty(document, "visibilityState");
    }
    completed = true; submission.status = 3;
    await mount("/submissions/s?challengeId=c"); await flush(1100);
    assert(selected() === "second", "成功提交未返回目标棋子");
    assert(fixture.querySelector(".selected-task-facts")?.textContent?.includes("已完成"), "成功完成状态错误");
    assert(document.querySelector(".challenge-completion-notice")?.textContent?.includes("当前得分 10/10"), "完成反馈未显示确认得分");
    assert(document.querySelector(".challenge-completion-notice")?.textContent?.includes("挑战进度 1/2"), "完成反馈未显示进度");
    await flush(6100); assert(!document.querySelector(".challenge-completion-notice"), "提示未自动消失或轮询重复弹出");
  }],
  ["文件完成提示不宣称已得满分，可手动关闭", async () => {
    fileTask = true; completed = true;
    try {
      await mount("/challenges/c", { completedTaskId: "second", playBreakAnimation: true, animationNonce: "file-test" });
      const notice = document.querySelector(".challenge-completion-notice");
      assert(notice?.textContent?.includes("文件已提交，等待评分"), "文件题未提示待评分");
      assert(!notice?.textContent?.includes("当前得分"), "文件上传被当作获得分数");
      await click(notice?.querySelector("button")); assert(!document.querySelector(".challenge-completion-notice"), "提示无法关闭");
    } finally { fileTask = false; }
  }]
];
let passed = 0;
if (new URLSearchParams(location.search).has("preview")) {
  latestStatus = 2;
  completed = new URLSearchParams(location.search).has("complete");
  await mount("/challenges/c", completed ? { completedTaskId: "second", playBreakAnimation: true, animationNonce: "preview" } : null);
  document.getElementById("status")!.textContent = "视觉预览（模拟数据）";
} else {
const actualMatchMedia = window.matchMedia;
// Desktop baseline is explicit; the mobile case above independently overrides it.
window.matchMedia = query => query.includes("max-width: 760px") ? { ...actualMatchMedia(query), matches: false } as MediaQueryList : actualMatchMedia(query);
try {
  for (const [name, run] of cases) {
    const row = document.createElement("li"); document.getElementById("results")!.append(row);
    try { await run(); passed++; row.textContent = `PASS ${name}`; }
    catch (error) { row.textContent = `FAIL ${name}: ${String(error)}`; break; }
  }
} finally {
  await unmount(); window.matchMedia = actualMatchMedia; window.fetch = originalFetch; Object.defineProperty(window, "localStorage", storage);
  document.getElementById("status")!.textContent = `${passed}/${cases.length} PASS`;
  // Assert the test cache used exactly the production account-scoped key.
  if (!values.has(problemDraftKey("flow-user", "challenge-position", "c"))) document.getElementById("status")!.textContent += "（位置缓存未验证）";
}
}
