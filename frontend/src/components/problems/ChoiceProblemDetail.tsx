import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import type { ProblemDetailDto } from "../../api/problemsApi";
import { createChoiceSubmission, type SubmissionDto } from "../../api/submissionsApi";
import { choiceOptionLabel, orderChoiceOptions } from "../../utils/choiceOptions";
import { MarkdownRenderer } from "../MarkdownRenderer";

interface Props {
  problem: ProblemDetailDto;
  isAuthenticated: boolean;
  canManage: boolean;
  onRequireLogin: () => void;
}

export function ChoiceProblemDetail({ problem, isAuthenticated, canManage, onRequireLogin }: Props) {
  const [answers, setAnswers] = useState<Record<string, string[]>>({});
  const [result, setResult] = useState<SubmissionDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  function toggle(questionId: string, optionId: string, single: boolean) {
    setAnswers((current) => {
      const selected = current[questionId] ?? [];
      return {
        ...current,
        [questionId]: single
          ? [optionId]
          : selected.includes(optionId) ? selected.filter((id) => id !== optionId) : [...selected, optionId]
      };
    });
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
      setResult(await createChoiceSubmission({
        problemId: problem.id,
        problemJudgeRevisionId: problem.currentJudgeRevisionId,
        answers: problem.choiceQuestions.map((question) => ({ questionId: question.id, optionIds: answers[question.id] ?? [] }))
      }));
    } catch (err) {
      setError(err instanceof Error ? err.message : "提交失败");
    } finally {
      setSubmitting(false);
    }
  }

  const results = new Map(result?.choiceQuestionResults.map((item) => [item.questionId, item]));
  return (
    <section className="page-section two-column problem-detail-layout ui-v2-page problem-detail-v2-page">
      <article className="problem-content problem-content-v2" data-surface="panel.primary">
        <div className="page-header compact ui-v2-page-header"><div><p className="eyebrow">CHOICE SET</p><h1>{problem.title}</h1><p>{problem.totalScore} 分 · {problem.choiceQuestions.length} 道小题 · {problem.choiceAnswerRevealPolicy === 1 ? "提交后查看答案" : problem.choiceAnswerRevealAt ? `答案于 ${new Date(problem.choiceAnswerRevealAt).toLocaleString()} 揭示` : "答案策略尚未配置"}</p></div>{canManage && <Link className="button" to={`/admin/problems/${problem.id}/edit`}>编辑题目</Link>}</div>
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
                  <input type={question.selectionMode === 1 ? "radio" : "checkbox"} name={`question-${question.id}`} checked={(answers[question.id] ?? []).includes(option.id)} disabled={Boolean(result)} onChange={() => toggle(question.id, option.id, question.selectionMode === 1)} />
                  <strong className="choice-option-letter">{choiceOptionLabel(option.order)}.</strong>
                  <span><MarkdownRenderer value={option.contentMarkdown} /></span>
                </label>)}
              </div>
              {questionResult && <div className={questionResult.isCorrect ? "quiet-note success" : "alert error"}>{questionResult.isCorrect ? `回答正确，获得 ${questionResult.score} 分` : "回答错误，本题 0 分"}</div>}
              {questionResult?.correctOptionIds && <div className="quiet-note">正确答案：{orderedOptions.filter(option => questionResult.correctOptionIds?.includes(option.id)).map(option => choiceOptionLabel(option.order)).join("、")}</div>}
              {questionResult?.explanationMarkdown && <section className="content-block"><h3>解析</h3><MarkdownRenderer value={questionResult.explanationMarkdown} /></section>}
            </section>;
          })}
          {error && <div className="alert error">{error}</div>}
          {result && <div className="quiet-note success">本次得分：{result.choiceScore}/{result.choiceTotalScore}{result.answersRevealed === false && problem.choiceAnswerRevealAt ? `；答案将在 ${new Date(problem.choiceAnswerRevealAt).toLocaleString()} 揭示` : ""} · <Link to={`/submissions/${result.id}`}>查看提交详情</Link></div>}
          {!result && <button className="button primary" type="submit" disabled={submitting}>{submitting ? "提交中..." : "提交答案"}</button>}
        </form>
      </article>
    </section>
  );
}
