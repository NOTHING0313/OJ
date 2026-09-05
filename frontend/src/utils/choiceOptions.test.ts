import { describe, expect, it } from "vitest";
import {
  choiceOptionLabel,
  maximumChoiceOptionCount,
  minimumChoiceOptionCount,
  orderChoiceOptions,
  resizeChoiceOptions
} from "./choiceOptions";

describe("choice option helpers", () => {
  it("labels persisted option orders as A through J", () => {
    expect(Array.from({ length: 10 }, (_, order) => choiceOptionLabel(order))).toEqual([
      "A", "B", "C", "D", "E", "F", "G", "H", "I", "J"
    ]);
  });

  it("lets authors resize options while preserving existing values", () => {
    const initial = [
      { id: "a", contentMarkdown: "First", isCorrect: true },
      { id: "b", contentMarkdown: "Second", isCorrect: false }
    ];

    const expanded = resizeChoiceOptions(initial, 4);
    expect(expanded).toHaveLength(4);
    expect(expanded.slice(0, 2)).toEqual(initial);
    expect(expanded.slice(2)).toEqual([
      { contentMarkdown: "", isCorrect: false },
      { contentMarkdown: "", isCorrect: false }
    ]);
    expect(resizeChoiceOptions(expanded, 3)).toEqual(expanded.slice(0, 3));
  });

  it("enforces the same 2 to 10 option boundary as the backend", () => {
    const initial = [
      { contentMarkdown: "A", isCorrect: true },
      { contentMarkdown: "B", isCorrect: false }
    ];

    expect(resizeChoiceOptions(initial, 1)).toHaveLength(minimumChoiceOptionCount);
    expect(resizeChoiceOptions(initial, 20)).toHaveLength(maximumChoiceOptionCount);
  });

  it("renders by persisted order without mutating the response", () => {
    const response = [{ order: 2 }, { order: 0 }, { order: 1 }];
    expect(orderChoiceOptions(response).map((option) => option.order)).toEqual([0, 1, 2]);
    expect(response.map((option) => option.order)).toEqual([2, 0, 1]);
  });
});
