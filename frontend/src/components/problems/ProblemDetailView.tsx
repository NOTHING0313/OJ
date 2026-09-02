import { type FormEvent, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { MarkdownRenderer } from "../MarkdownRenderer";

export interface ProblemDetailViewModel {
  id: string;
  title: string;
  description: string;
  inputDescription: string;
  outputDescription: string;
  timeLimitMs: number;
  memoryLimitMb: number;
  totalScore: number;
  judgeMode: 1 | 2;
  languageTags: string[];
  functionSpec: { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }> } | null;
  hasListNode: boolean;
  hasTreeNode: boolean;
  samples: Array<{ id: string; input: string; output: string }>;
}

interface ProblemDetailViewProps {
  problem: ProblemDetailViewModel;
  seasonScore?: { seasonName: string; baseScore: number; timeBonus: number; runtimeBonus: number; memoryBonus: number } | null;
  language: number;
  languages: Array<{ value: number; label: string }>;
  isAuthenticated: boolean;
  canManage: boolean;
  challengeId?: string | null;
  error?: string | null;
  isSubmitting: boolean;
  editor: ReactNode;
  onSubmit: (event: FormEvent) => void;
  onLanguageChange: (language: number) => void;
  onClearSource: () => void;
}

export function ProblemDetailView({ problem, seasonScore, language, languages, isAuthenticated, canManage, challengeId, error, isSubmitting, editor, onSubmit, onLanguageChange, onClearSource }: ProblemDetailViewProps) {
  return (
    <section className="page-section two-column problem-detail-layout ui-v2-page problem-detail-v2-page">
      <article className="problem-content problem-content-v2" data-surface="panel.primary">
        <div className="page-header compact ui-v2-page-header problem-detail-header-v3" data-surface="decoration.pageHeader">
          <div className="problem-title-block-v3"><p className="eyebrow">PROBLEM</p><h1>{problem.title}</h1><div className="problem-meta-row-v3"><span className="problem-meta-summary-v3">{problem.timeLimitMs} ms / {problem.memoryLimitMb} MB / {problem.totalScore} 分</span>{problem.languageTags.map((tag) => <span className="context-chip" key={tag}>{tag}</span>)}</div></div>
          <div className="problem-header-actions-v3">{isAuthenticated && <Link className="button" to={`/submissions/my?problemId=${problem.id}`}>我的提交</Link>}{canManage && <Link className="button" to={`/admin/problems/${problem.id}/test-cases`}>测试用例</Link>}</div>
        </div>

        {seasonScore && <section className="problem-season-score-card"><div><span>赛季计分</span><strong>{seasonScore.seasonName}</strong></div><dl><div><dt>基础分</dt><dd>{seasonScore.baseScore}</dd></div><div><dt>Top10 时间奖励</dt><dd>最高 +{seasonScore.timeBonus}%</dd></div><div><dt>运行奖励</dt><dd>最高 +{seasonScore.runtimeBonus}%</dd></div><div><dt>内存奖励</dt><dd>最高 +{seasonScore.memoryBonus}%</dd></div></dl><Link className="button" to={`/leaderboards/users/problems/${problem.id}`}>查看单题榜</Link></section>}

        <section className="content-block"><h2>描述</h2><MarkdownRenderer value={problem.description} /></section>
        {problem.judgeMode === 1 ? <><section className="content-block"><h2>输入说明</h2><MarkdownRenderer value={problem.inputDescription} /></section><section className="content-block"><h2>输出说明</h2><MarkdownRenderer value={problem.outputDescription} /></section></> : <section className="content-block"><h2>函数说明</h2><p>只需要完成 Solution 类中的函数，不需要编写 Main/main，不需要处理输入输出。函数式题目当前支持 C++17、C# 和 C11。</p>{problem.hasListNode && <p className="quiet-note">链表测试数据使用数组表示，例如 [1,2,3] 表示 1 -&gt; 2 -&gt; 3；[] 表示空链表。C11 暂不支持链表函数式判题。</p>}{problem.hasTreeNode && <p className="quiet-note">二叉树测试数据使用层序数组表示，例如 [1,2,3,null,4]；[] 表示空树；输出比较会忽略尾部多余 null。C11 暂不支持二叉树函数式判题。</p>}{language === 2 && <p className="quiet-note">C 语言返回数组时，请使用 malloc 分配返回数组，并正确设置 *returnSize。</p>}{problem.functionSpec ? <div className="table-wrap"><table className="function-spec-table"><tbody><tr><th>函数名</th><td>{problem.functionSpec.functionName}</td></tr><tr><th>返回类型</th><td>{problem.functionSpec.returnType}</td></tr><tr><th>参数</th><td>{problem.functionSpec.parameters.map((parameter) => `${parameter.type} ${parameter.name}`).join(", ") || "无"}</td></tr></tbody></table></div> : <div className="quiet-note">函数配置暂不可用，请联系出题人检查配置。</div>}</section>}

        <section className="content-block public-samples"><h2>公开样例</h2>{problem.samples.length === 0 ? <div className="empty-state">暂无公开样例</div> : <div className="sample-list">{problem.samples.map((sample, index) => <div className="sample-card" key={sample.id}><h3>样例 {index + 1}</h3>{problem.judgeMode === 1 ? <div className="sample-grid"><div><span>输入</span><pre>{sample.input || "-"}</pre></div><div><span>输出</span><pre>{sample.output || "-"}</pre></div></div> : <div className="function-sample"><div className="function-sample-section"><span className="function-sample-label">输入</span><pre className="function-sample-code">{sample.input}</pre></div><div className="function-sample-section"><span className="function-sample-label">输出</span><pre className="function-sample-code">{sample.output}</pre></div></div>}</div>)}</div>}</section>
      </article>

      <aside className="submit-panel submit-panel-v2" data-surface="panel.primary"><div className="submit-panel-heading-v3"><h2>提交代码</h2>{challengeId && <Link className="button" to={`/challenges/${challengeId}`}>返回挑战棋盘</Link>}</div><form onSubmit={onSubmit} className="form-stack"><label>语言<select value={language} disabled={languages.length === 0} onChange={(event) => onLanguageChange(Number(event.target.value))}>{languages.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label><div className="code-field"><span>代码</span><div className="problem-editor-slot">{editor}</div></div>{error && <div className="alert error">{error}</div>}<div className="button-row"><button className="button" type="button" onClick={onClearSource}>清空本地缓存</button><button className="button primary" type="submit" disabled={isSubmitting || languages.length === 0}>{isSubmitting ? "提交中..." : "提交"}</button></div></form></aside>
    </section>
  );
}
