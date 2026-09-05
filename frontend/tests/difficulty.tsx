import { useState } from "react";
import { createRoot } from "react-dom/client";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../src/auth/AuthContext";
import { ProblemListPage } from "../src/pages/ProblemListPage";
import "../src/styles.css";

// Isolated preview: no request reaches the real API, and no business data is edited.
const problems = ["未分级示例题", "简单示例题", "中等示例题", "困难示例题"].map((title, difficulty) => ({
  id: `preview-${difficulty}`, title, difficulty, problemKind: 1, isPublished: true,
  timeLimitMs: 1000, memoryLimitMb: 128, judgeMode: 1, allowedLanguagesMask: 0, totalScore: 100, createdAt: "2026-09-05T00:00:00Z"
}));
window.fetch = async input => {
  const url = String(input);
  if (url === "/api/auth/me") return Response.json({ id: "preview-only", userName: "Preview", role: 3 });
  if (url.startsWith("/api/problems/query?")) return Response.json({ items: problems, page: 1, pageSize: 20, totalCount: 4 });
  throw new Error("Unexpected preview request");
};
// eslint-disable-next-line react-refresh/only-export-components -- Standalone visual test entry.
function Preview() {
  const [themed, setThemed] = useState(false);
  return <div className={themed ? "theme-mystic-background" : ""}>
    <div className="site-theme-content" style={{ padding: "24px" }}>
      <button className="button" onClick={() => setThemed(value => !value)}>切换主题样式</button>
      <p role="status">{themed ? "主题风格" : "默认风格"} · 隔离测试数据</p>
      <MemoryRouter><AuthProvider><ProblemListPage /></AuthProvider></MemoryRouter>
    </div>
  </div>;
}
createRoot(document.getElementById("root")!).render(<Preview />);
