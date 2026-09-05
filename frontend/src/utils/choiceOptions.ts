import type { ChoiceQuestionWriteRequest } from "../api/problemsApi";

type ChoiceOptionWriteRequest = ChoiceQuestionWriteRequest["options"][number];

export const minimumChoiceOptionCount = 2;
export const maximumChoiceOptionCount = 10;

export function choiceOptionLabel(order: number) {
  return Number.isInteger(order) && order >= 0 && order < 26
    ? String.fromCharCode(65 + order)
    : String(order + 1);
}

export function resizeChoiceOptions(options: readonly ChoiceOptionWriteRequest[], requestedCount: number) {
  const normalizedCount = Number.isFinite(requestedCount) ? Math.trunc(requestedCount) : options.length;
  const count = Math.min(maximumChoiceOptionCount, Math.max(minimumChoiceOptionCount, normalizedCount));
  if (count <= options.length) {
    return options.slice(0, count);
  }

  return [
    ...options,
    ...Array.from({ length: count - options.length }, () => ({ contentMarkdown: "", isCorrect: false }))
  ];
}

export function orderChoiceOptions<T extends { order: number }>(options: readonly T[]) {
  return [...options].sort((left, right) => left.order - right.order);
}
