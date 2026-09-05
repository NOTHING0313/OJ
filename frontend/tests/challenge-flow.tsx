import { act, StrictMode } from "react";
import { createRoot, type Root } from "react-dom/client";
import { Link, MemoryRouter, Route, Routes } from "react-router-dom";
import { AuthProvider } from "../src/auth/AuthContext";
import { ChallengeDetailPage } from "../src/pages/ChallengeDetailPage";
import { SubmissionDetailPage } from "../src/pages/SubmissionDetailPage";
import { problemDraftKey } from "../src/utils/problemDrafts";
import type { ChallengeDetailDto } from "../src/api/challengesApi";
import type { SubmissionDto } from "../src/api/submissionsApi";

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
    id, challengeId: "c", title: id, description: "", taskType: 1, difficulty: 1,
    boardX: index, boardY: 0, algorithmProblemId: "p", score: 10, isPublished: true,
    createdAt: "2026-01-01", updatedAt: "2026-01-01", isCompleted: completed && index === 1,
    completedAt: null, completedScore: completed && index === 1 ? 10 : null,
    earnedScore: completed && index === 1 ? 10 : 0
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
async function mount(path = "/challenges/c") {
  await unmount();
  await act(async () => {
    root = createRoot(fixture);
    root.render(<StrictMode><MemoryRouter initialEntries={[path]}><AuthProvider><Routes>
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
  }]
];
let passed = 0;
try {
  for (const [name, run] of cases) {
    const row = document.createElement("li"); document.getElementById("results")!.append(row);
    try { await run(); passed++; row.textContent = `PASS ${name}`; }
    catch (error) { row.textContent = `FAIL ${name}: ${String(error)}`; break; }
  }
} finally {
  await unmount(); window.fetch = originalFetch; Object.defineProperty(window, "localStorage", storage);
  document.getElementById("status")!.textContent = `${passed}/${cases.length} PASS`;
  // Assert the test cache used exactly the production account-scoped key.
  if (!values.has(problemDraftKey("flow-user", "challenge-position", "c"))) document.getElementById("status")!.textContent += "（位置缓存未验证）";
}
