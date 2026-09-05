import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import type { ProblemDetailDto } from "../../api/problemsApi";
import { createChoiceSubmission, getSubmission, type SubmissionDto } from "../../api/submissionsApi";
import { choiceOptionLabel, orderChoiceOptions } from "../../utils/choiceOptions";
import { useAuth } from "../../auth/AuthContext";
import { problemDraftKey, readChoiceDraft, writeDraft } from "../../utils/problemDrafts";
import { pollSubmission } from "../../utils/submissionPolling";
import { MarkdownRenderer } from "../MarkdownRenderer";

interface Props {
  problem: ProblemDetailDto;
  isAuthenticated: boolean;
  onRequireLogin: () => void;
}

export function ChoiceProblemDetail({ problem, isAuthenticated, onRequireLogin }: Props) {
  const { currentUser } = useAuth();
  const scope = ["choice", problem.id, problem.currentJudgeRevisionId ?? "draft"];
  const draftKey = problemDraftKey(currentUser?.id, ...scope);
  const [answers, setAnswers] = useState<Record<string, string[]>>(() => readChoiceDraft(draftKey));
  const [draftWarning, setDraftWarning] = useState<string | null>(null);
  const [refreshVersion, setRefreshVersion] = useState(0);
  const active = useRef(true);
  useEffect(() => { active.current = true; return () => { active.current = false; }; }, []);
  const [result, setResult] = useState<SubmissionDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const resultId = result?.id;
  const waitingForReveal = result?.answersRevealed === false;
  useEffect(() => {
    if (!resultId || !waitingForReveal) return;
    return pollSubmission(() => getSubmission(resultId), (item) => { setResult(item); setError(null); }, setError);
  }, [resultId, waitingForReveal, refreshVersion]);

  function toggle(questionId: string, optionId: string, single: boolean) {
    const selected = answers[questionId] ?? [];
    const next = { ...answers, [questionId]: single ? [optionId]
      : selected.includes(optionId) ? selected.filter((id) => id !== optionId) : [...selected, optionId] };
    setAnswers(next);
    setDraftWarning(writeDraft(draftKey, JSON.stringify(next)) ? null : "浏览器无法保存草稿，请勿刷新或离开页面。");
  }

  function restart() {
    setResult(null);
    setAnswers({});
    setError(null);
    setDraftWarning(writeDraft(draftKey, null) ? null : "浏览器无法清除旧草稿。");
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!isAuthenticated) {
      onRequireLogin();
      return;
    }
    if (!problem.currentJudgeRevisionId) {
      setError("题目当前没有可用的发布修订。");
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const submitted = await createChoiceSubmission({
        problemId: problem.id,
        problemJudgeRevisionId: problem.currentJudgeRevisionId,
        answers: problem.choiceQuestions.map((question) => ({ questionId: question.id, optionIds: answers[question.id] ?? [] }))
      });
      if (!active.current) return;
      setResult(submitted);
      writeDraft(draftKey, null);
    } catch (err) {
      if (!active.current) return;
      setError(err instanceof Error ? err.message : "提交失败");
    } finally {
      if (active.current) setSubmitting(false);
    }
  }

  const results = new Map(result?.choiceQuestionResults.map((item) => [item.questionId, item]));
  return (
    <section className="page-section choice-problem-detail problem-detail-layout ui-v2-page problem-detail-v2-page">
      <article className="problem-content problem-content-v2" data-surface="panel.primary">
        <div className="page-header compact ui-v2-page-header"><div><p className="eyebrow">CHOICE SET</p><h1>{problem.title}</h1><p>{problem.totalScore} 分 · {problem.choiceQuestions.length} 道小题 · {problem.choiceAnswerRevealPolicy === 1 ? "提交后查看答案" : problem.choiceAnswerRevealAt ? `答案于 ${new Date(problem.choiceAnswerRevealAt).toLocaleString()} 揭示` : "答案策略尚未配置"}</p></div></div>
        <section className="content-block"><h2>描述</h2><MarkdownRenderer value={problem.description} /></section>
        <form className="form-stack" onSubmit={submit}>
          {problem.choiceQuestions.map((question, index) => {
            const questionResult = results.get(question.id);
            const orderedOptions = orderChoiceOptions(question.options);
            return <section className="content-block choice-question" key={question.id}>
              <div className="section-heading-row"><h2>第 {index + 1} 题</h2><span>{question.selectionMode === 1 ? "单选" : "多选"} · {question.score} 分</span></div>
              <MarkdownRenderer value={question.stemMarkdown} />
              <div className="form-stack choice-options">
                {orderedOptions.map((option) => <label className="choice-option" key={option.id}>
                  <input type={question.selectionMode === 1 ? "radio" : "checkbox"} name={`question-${question.id}`} checked={(answers[question.id] ?? []).includes(option.id)} disabled={Boolean(result) || submitting} onChange={() => toggle(question.id, option.id, question.selectionMode === 1)} />
                  <strong className="choice-option-letter">{choiceOptionLabel(option.order)}.</strong>
                  <div className="choice-option-content"><MarkdownRenderer value={option.contentMarkdown} /></div>
                </label>)}
              </div>
              {questionResult && <div className={questionResult.isCorrect ? "quiet-note success" : "alert error"}>{questionResult.isCorrect ? `回答正确，获得 ${questionResult.score} 分` : "回答错误，本题 0 分"}</div>}
              {questionResult?.correctOptionIds && <div className="quiet-note">正确答案：{orderedOptions.filter(option => questionResult.correctOptionIds?.includes(option.id)).map(option => choiceOptionLabel(option.order)).join("、")}</div>}
              {questionResult?.explanationMarkdown && <section className="content-block"><h3>解析</h3><MarkdownRenderer value={questionResult.explanationMarkdown} /></section>}
            </section>;
          })}
          {!result && <p className="quiet-note">还有 {problem.choiceQuestions.filter(question => !(answers[question.id]?.length)).length} 道题未作答</p>}
          {draftWarning && <div className="alert error" role="alert">{draftWarning}</div>}
          {error && <div className="alert error">{error}</div>}
          {result && <div className="quiet-note success">本次得分：{result.choiceScore}/{result.choiceTotalScore}{result.answersRevealed === false && problem.choiceAnswerRevealAt ? `；答案将在 ${new Date(problem.choiceAnswerRevealAt).toLocaleString()} 揭示` : ""} · <Link to={`/submissions/${result.id}`}>查看提交详情</Link></div>}
          {result && <div className="button-row"><button className="button" type="button" onClick={restart}>重新练习</button>{waitingForReveal && <button className="button" type="button" onClick={() => setRefreshVersion(value => value + 1)}>刷新答案</button>}</div>}
          {!result && <button className="button primary" type="submit" disabled={submitting}>{submitting ? "提交中..." : "提交答案"}</button>}
        </form>
      </article>
    </section>
  );
}
