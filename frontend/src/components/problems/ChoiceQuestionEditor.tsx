import type { ChoiceQuestionWriteRequest } from "../../api/problemsApi";
import {
  choiceOptionLabel,
  maximumChoiceOptionCount,
  minimumChoiceOptionCount,
  resizeChoiceOptions
} from "../../utils/choiceOptions";
import { MarkdownEditor } from "../MarkdownEditor";

interface Props {
  questions: ChoiceQuestionWriteRequest[];
  onChange: (questions: ChoiceQuestionWriteRequest[]) => void;
}

export function ChoiceQuestionEditor({ questions, onChange }: Props) {
  function updateQuestion(index: number, next: ChoiceQuestionWriteRequest) {
    onChange(questions.map((question, questionIndex) => questionIndex === index ? next : question));
  }

  function move(index: number, delta: number) {
    const target = index + delta;
    if (target < 0 || target >= questions.length) return;
    const next = [...questions];
    [next[index], next[target]] = [next[target], next[index]];
    onChange(next);
  }

  return (
    <section className="content-block choice-authoring">
      <div className="section-heading-row">
        <div><h2>选择题题组</h2><p className="muted-text">题干、选项和解析均支持 Markdown 与围栏代码块。</p></div>
        <button className="button" type="button" onClick={() => onChange([...questions, {
          stemMarkdown: "",
          selectionMode: 1,
          score: 1,
          explanationMarkdown: "",
          options: [
            { contentMarkdown: "", isCorrect: true },
            { contentMarkdown: "", isCorrect: false }
          ]
        }])}>添加小题</button>
      </div>

      {questions.map((question, questionIndex) => (
        <article className="content-block choice-authoring-question" key={question.id ?? `new-${questionIndex}`}>
          <div className="section-heading-row">
            <h3>第 {questionIndex + 1} 题</h3>
            <div className="button-row">
              <button className="button" type="button" disabled={questionIndex === 0} onClick={() => move(questionIndex, -1)}>上移</button>
              <button className="button" type="button" disabled={questionIndex === questions.length - 1} onClick={() => move(questionIndex, 1)}>下移</button>
              <button className="button danger" type="button" onClick={() => onChange(questions.filter((_, index) => index !== questionIndex))}>删除小题</button>
            </div>
          </div>
          <MarkdownEditor label="题干" value={question.stemMarkdown} onChange={(value) => updateQuestion(questionIndex, { ...question, stemMarkdown: value })} />
          <div className="form-row">
            <label>作答方式<select value={question.selectionMode} onChange={(event) => {
              const selectionMode = Number(event.target.value) as 1 | 2;
              const options = selectionMode === 1
                ? question.options.map((option, index) => ({ ...option, isCorrect: index === question.options.findIndex(item => item.isCorrect) }))
                : question.options;
              updateQuestion(questionIndex, { ...question, selectionMode, options });
            }}><option value={1}>单选</option><option value={2}>多选</option></select></label>
            <label>分值<input type="number" min={1} max={1000} value={question.score} onChange={(event) => updateQuestion(questionIndex, { ...question, score: Number(event.target.value) })} /></label>
            <label>选项数量<input type="number" min={minimumChoiceOptionCount} max={maximumChoiceOptionCount} value={question.options.length} onChange={(event) => updateQuestion(questionIndex, {
              ...question,
              options: resizeChoiceOptions(question.options, Number(event.target.value))
            })} /></label>
          </div>
          <p className="quiet-note">可手动设置 2–10 个选项；保存后按 A、B、C、D… 的顺序展示。</p>

          <div className="form-stack">
            {question.options.map((option, optionIndex) => (
              <div className="content-block choice-authoring-option" key={option.id ?? `new-${optionIndex}`}>
                <label className="checkbox-line">
                  <input
                    type={question.selectionMode === 1 ? "radio" : "checkbox"}
                    name={`correct-${question.id ?? questionIndex}`}
                    checked={option.isCorrect}
                    onChange={(event) => updateQuestion(questionIndex, {
                      ...question,
                      options: question.options.map((item, index) => ({
                        ...item,
                        isCorrect: question.selectionMode === 1 ? index === optionIndex : index === optionIndex ? event.target.checked : item.isCorrect
                      }))
                    })}
                  />正确答案
                </label>
                <MarkdownEditor label={`选项 ${choiceOptionLabel(optionIndex)}`} value={option.contentMarkdown} onChange={(value) => updateQuestion(questionIndex, {
                  ...question,
                  options: question.options.map((item, index) => index === optionIndex ? { ...item, contentMarkdown: value } : item)
                })} />
                <button className="button danger" type="button" disabled={question.options.length <= minimumChoiceOptionCount} onClick={() => updateQuestion(questionIndex, {
                  ...question,
                  options: question.options.filter((_, index) => index !== optionIndex)
                })}>删除选项</button>
              </div>
            ))}
          </div>
          <button className="button" type="button" disabled={question.options.length >= maximumChoiceOptionCount} onClick={() => updateQuestion(questionIndex, {
            ...question,
            options: [...question.options, { contentMarkdown: "", isCorrect: false }]
          })}>添加选项</button>
          <MarkdownEditor label="答案解析" value={question.explanationMarkdown} onChange={(value) => updateQuestion(questionIndex, { ...question, explanationMarkdown: value })} />
        </article>
      ))}
      {questions.length === 0 && <div className="empty-state">当前是空草稿；发布前至少添加一道完整小题。</div>}
    </section>
  );
}
