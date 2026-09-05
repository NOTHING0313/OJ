import { act, StrictMode } from "react";
import { createRoot, type Root } from "react-dom/client";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { AdminProblemEditorPage } from "../src/pages/AdminProblemEditorPage";
import { MySubmissionsPage } from "../src/pages/MySubmissionsPage";
import { AdminSubmissionsPage } from "../src/pages/AdminSubmissionsPage";
import { AuthProvider } from "../src/auth/AuthContext";
import { problemDraftKey } from "../src/utils/problemDrafts";

// Local test page: all fetches and storage are isolated before mounting production components.
(globalThis as any).IS_REACT_ACT_ENVIRONMENT = true;
const fixture = document.getElementById("fixture")!;
const results = document.getElementById("results")!;
const originalFetch = window.fetch;
const storageDescriptor = Object.getOwnPropertyDescriptor(window, "localStorage")!;
const originalConfirm = window.confirm;
const values = new Map<string, string>();
let failWrite = false, failRemove = false;
Object.defineProperty(window, "localStorage", { configurable: true, value: {
  getItem: (key: string) => values.get(key) ?? null,
  setItem: (key: string, value: string) => { if (failWrite) throw new Error("quota exceeded"); values.set(key, value); },
  removeItem: (key: string) => { if (failRemove) throw new Error("storage disabled"); values.delete(key); }
} });
let handler: (url: string, options: RequestInit) => Promise<Response>;
window.fetch = (input, options = {}) => handler(String(input), options);
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json" } });
const user = { id: "reliability-test-user", userName: "Test", role: 3, email: "test@example.invalid" };
const key = problemDraftKey(user.id, "authoring-v1", "p-test");
let root: Root | undefined;
let server: any;
let saveStatus = 200;
let saveCalls = 0;
function assert(value: unknown, message: string): asserts value { if (!value) throw new Error(message); }
const text = () => fixture.textContent ?? "";
async function flush(ms = 0) { await act(async () => { await new Promise(resolve => setTimeout(resolve, ms)); }); }
async function unmount() { if (root) { const current = root; root = undefined; await act(async () => current.unmount()); } }
// eslint-disable-next-line react-refresh/only-export-components -- Standalone test entry mounts its own root.
function Location() { return <output data-location>{useLocation().pathname}{useLocation().search}</output>; }
async function mount(page: "editor" | "mine" | "admin", path = "/admin/problems/p-test/edit") {
  await unmount();
  await act(async () => {
    root = createRoot(fixture);
    root.render(<StrictMode><MemoryRouter initialEntries={[path]}><Location />{page === "editor" ? <AuthProvider><Routes><Route path="/admin/problems/new" element={<AdminProblemEditorPage />} /><Route path="/admin/problems/:id/edit" element={<AdminProblemEditorPage />} /></Routes></AuthProvider> : page === "mine" ? <MySubmissionsPage /> : <AdminSubmissionsPage />}</MemoryRouter></StrictMode>);
  });
  await flush();
  if (page === "editor") {
    for (let attempt = 0; attempt < 30 && !fixture.querySelector("form"); attempt++) await flush(10);
    assert(fixture.querySelector("form"), `editor did not load: ${text()}`);
  }
}
async function value(element: HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement, next: string) {
  assert(element, "missing form control");
  await act(async () => {
    const proto = element instanceof HTMLSelectElement ? HTMLSelectElement.prototype : element instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(proto, "value")!.set!.call(element, next);
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? "change" : "input", { bubbles: true }));
  });
}
const title = () => fixture.querySelector<HTMLInputElement>('input')!;
function select(label: string) { return [...fixture.querySelectorAll("label")].find(item => item.textContent?.trim().startsWith(label))!.querySelector("select")!; }
function button(label: string) { const found = [...fixture.querySelectorAll("button")].find(item => item.textContent?.trim() === label); assert(found, `missing button: ${label}`); return found; }
async function click(label: string) { await act(async () => button(label).click()); }
async function submit() { await act(async () => { fixture.querySelector("form")!.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })); }); await flush(); }
function setupEditor() {
  server = { id: "p-test", title: "server title", difficulty: 0, description: "body", inputDescription: "", outputDescription: "", problemKind: 2, authoringVersion: 1, isPublished: false, judgeMode: null, allowedLanguagesMask: 0, choiceAnswerRevealPolicy: 1, choiceAnswerRevealAt: null, choiceQuestions: [], testCases: [] };
  saveStatus = 200; saveCalls = 0;
  handler = async (url, options) => {
    if (url === "/api/auth/me") return json(user);
    if (url.endsWith("/judge-assets")) return json([]);
    if (options.method === "PUT" || options.method === "POST") {
      assert(url === "/api/problems" || url === "/api/problems/p-test/authoring", "unexpected write");
      saveCalls++;
      if (saveStatus !== 200) return json("题目版本冲突，请刷新。", saveStatus);
      const payload = JSON.parse(String(options.body));
      if (options.method === "PUT") assert(payload.expectedAuthoringVersion === server.authoringVersion, "wrong concurrency version");
      server = { ...server, ...payload, authoringVersion: server.authoringVersion + 1 };
      return json(server);
    }
    if (url.endsWith("/authoring")) return json(server);
    throw new Error(`Unexpected test request: ${url}`);
  };
}
const cases: Array<[string, () => Promise<void>]> = [
  ["难度：保存、重新读取、清除分级与草稿恢复", async () => {
    setupEditor(); await mount("editor"); await value(select("难度"), "3");
    assert(JSON.parse(values.get(key)!).fields.difficulty === 3, "difficulty absent from draft");
    await unmount(); await mount("editor"); await click("恢复草稿"); assert(select("难度").value === "3", "difficulty restore failed");
    await submit(); assert(server.difficulty === 3, "difficulty absent from save payload");
    await unmount(); await mount("editor"); assert(select("难度").value === "3", "server difficulty not loaded");
    await value(select("难度"), "0"); await submit(); assert(server.difficulty === 0, "cannot clear grade");
  }],
  ["编辑保存成功：清理草稿，解除脏状态，后续编辑仍可暂存", async () => {
    setupEditor(); await mount("editor"); await value(title(), "edited"); assert(values.has(key), "draft not persisted");
    await submit(); assert(saveCalls === 1, "save not called once"); assert(!values.has(key), "saved draft remains"); assert(!text().includes("尚未保存到服务器"), "dirty notice after save");
    await value(title(), "next edit"); assert(values.has(key), "second edit not persisted");
  }],
  ["保存失败：保留字段、草稿与刷新提醒", async () => {
    setupEditor(); saveStatus = 409; await mount("editor"); await value(title(), "unsaved"); await submit();
    assert(title().value === "unsaved" && values.has(key), "failed save discarded edits"); assert(text().includes("题目版本冲突"), "missing failure reason");
    const event = new Event("beforeunload", { cancelable: true }); window.dispatchEvent(event); assert(event.defaultPrevented, "missing unload guard");
    window.confirm = () => false; const link = fixture.querySelector<HTMLAnchorElement>('a')!; await act(async () => link.click()); assert(fixture.querySelector("[data-location]")!.textContent!.includes("p-test/edit"), "cancelled navigation proceeded");
  }],
  ["重新进入：显式恢复完整草稿，丢弃后不再出现", async () => {
    setupEditor(); await mount("editor"); await value(title(), "restore me"); await unmount(); await mount("editor");
    assert(title().matches(":disabled"), "pending draft did not lock form"); await click("恢复草稿"); assert(title().value === "restore me", "restore failed");
    await unmount(); await mount("editor"); await click("丢弃草稿"); assert(!values.has(key), "discard failed"); assert(title().value === "server title", "discard replaced server fields");
  }],
  ["旧服务器版本：阻止直接恢复和提交", async () => {
    setupEditor(); await mount("editor"); await value(title(), "old draft"); await unmount(); server.authoringVersion = 2; await mount("editor");
    assert(button("恢复草稿").disabled, "old version restore allowed"); assert(text().includes("旧版本草稿"), "missing conflict explanation");
    assert(fixture.querySelector("fieldset")!.disabled && saveCalls === 0, "conflict allowed write");
  }],
  ["存储写入失败：明确提示且仍保留未保存提醒", async () => {
    setupEditor(); await mount("editor"); failWrite = true; await value(title(), "quota test");
    assert(text().includes("浏览器无法保存或清理草稿"), "quota failure hidden"); assert(title().value === "quota test", "quota failure lost field");
    const event = new Event("beforeunload", { cancelable: true }); window.dispatchEvent(event); assert(event.defaultPrevented, "quota failure removed unload guard");
  }],
  ["保存成功但缓存清理失败：显示警告，不误报服务器未保存", async () => {
    setupEditor(); await mount("editor"); await value(title(), "remove fail"); failRemove = true; await submit();
    assert(text().includes("本地旧草稿未能清理"), "cleanup failure hidden"); assert(!text().includes("尚未保存到服务器"), "false dirty after saved");
  }],
  ["新建保存：清理 new 草稿并进入编辑页", async () => {
    setupEditor(); await mount("editor", "/admin/problems/new"); await value(title(), "new draft"); await value(fixture.querySelector("textarea")!, "description"); await value(select("题目类型"), "2");
    const newKey = problemDraftKey(user.id, "authoring-v1", "new"); assert(values.has(newKey), "new draft not written"); await submit();
    assert(saveCalls === 1 && !values.has(newKey), "creation did not clear draft"); assert(fixture.querySelector("[data-location]")!.textContent!.endsWith("p-test/edit"), "creation did not navigate");
  }]
];

function deferred() { let resolve!: (response: Response) => void; let reject!: (error: Error) => void; const promise = new Promise<Response>((yes, no) => { resolve = yes; reject = no; }); return { resolve, reject, promise }; }
const pageResult = (name: string) => ({ items: [{ id: name, problemId: "p", problemTitle: name, userId: "u", userName: "Test", submissionKind: 2, language: null, status: 3, evaluation: {}, createdAt: "2026-09-05T00:00:00Z", finishedAt: null, choiceScore: 1, choiceTotalScore: 2 }], totalCount: 1, page: 1, pageSize: 20 });
for (const page of ["mine", "admin"] as const) {
  cases.push([`${page}：新响应先完成，旧响应不能覆盖`, async () => {
    const pending: Array<ReturnType<typeof deferred> & { signal?: AbortSignal | null }> = [];
    handler = (_url, options) => { const d = deferred(); pending.push({ ...d, signal: options.signal }); return d.promise; };
    await mount(page, "/submissions"); await flush(220); await value(select("状态"), "3"); await flush(220);
    assert(pending.length === 2 && pending[0].signal?.aborted, "old request not aborted");
    await act(async () => pending[1].resolve(json(pageResult("new result")))); await flush(10); await act(async () => pending[0].resolve(json(pageResult("old result")))); await flush(10);
    assert(text().includes("new result") && !text().includes("old result"), "stale response overwrote current result");
  }]);
  cases.push([`${page}：旧请求失败不能打断新请求加载`, async () => {
    const pending: Array<ReturnType<typeof deferred>> = []; handler = () => { const d = deferred(); pending.push(d); return d.promise; };
    await mount(page, "/submissions"); await flush(220); await value(select("状态"), "3"); await flush(220);
    await act(async () => pending[0].reject(new TypeError("Failed to fetch")));
    assert(text().includes("正在加载") && !fixture.querySelector(".alert.error"), "stale failure changed loading/error state");
    await act(async () => pending[1].resolve(json(pageResult("current")))); await flush(10); assert(text().includes("current"), "new result missing");
  }]);
  cases.push([`${page}：重置清除 URL 条件，选择题清空语言`, async () => {
    const urls: string[] = []; handler = async url => { urls.push(url); return json(pageResult("choice")); };
    await mount(page, "/submissions?problemId=p"); await flush(220); await click("重置"); await flush(220);
    assert(!fixture.querySelector("[data-location]")!.textContent!.includes("problemId"), "URL filter remained"); assert(!urls.at(-1)!.includes("problemId"), "request filter remained");
    await value(select("语言"), "1"); await value(select("题型"), "2"); await flush(220);
    assert(select("语言").disabled && select("语言").value === "", "contradictory language filter");
    assert(urls.at(-1)!.includes("submissionKind=2") && !urls.at(-1)!.includes("language="), "wrong server filter"); assert(text().includes("结果评估") && text().includes("得分 1/2"), "choice result mismatch");
  }]);
}
let failures = 0;
try {
  for (const [name, run] of cases) {
    await unmount(); values.clear(); failWrite = false; failRemove = false; window.confirm = originalConfirm;
    const item = document.createElement("li"); results.append(item);
    try { await run(); item.textContent = `PASS ${name}`; }
    catch (error) { failures++; item.textContent = `FAIL ${name}: ${error instanceof Error ? error.stack : String(error)}`; }
  }
} finally {
  await unmount(); window.fetch = originalFetch; window.confirm = originalConfirm; Object.defineProperty(window, "localStorage", storageDescriptor);
  document.getElementById("status")!.textContent = `${failures === 0 ? "PASS" : "FAIL"} ${cases.length - failures}/${cases.length}`;
}
