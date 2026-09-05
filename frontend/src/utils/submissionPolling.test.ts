import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../api/httpClient";
import type { SubmissionDto } from "../api/submissionsApi";
import { pollSubmission, submissionRefreshDelay } from "./submissionPolling";

const completed = { finishedAt: "2026-09-05", submissionKind: 1 } as SubmissionDto;
afterEach(() => vi.useRealTimers());

describe("submission refresh lifecycle", () => {
  it("recovers from a network failure and stops once judging completes", async () => {
    vi.useFakeTimers();
    const load = vi.fn().mockRejectedValueOnce(new ApiError("网络连接中断", 0, "NETWORK_ERROR")).mockResolvedValueOnce({ finishedAt: null }).mockResolvedValue(completed);
    const result = vi.fn();
    const error = vi.fn();
    const stop = pollSubmission(load, result, error);
    await vi.advanceTimersByTimeAsync(4000);
    expect(load).toHaveBeenCalledTimes(3);
    expect(result).toHaveBeenLastCalledWith(completed);
    expect(error).toHaveBeenCalledTimes(1);
    expect(vi.getTimerCount()).toBe(0);
    stop();
  });

  it("caps consecutive retries and allows a fresh manual attempt", async () => {
    vi.useFakeTimers();
    const load = vi.fn().mockRejectedValue(new Error("offline"));
    const error = vi.fn();
    const stop = pollSubmission(load, vi.fn(), error);
    await vi.runAllTimersAsync();
    expect(load).toHaveBeenCalledTimes(4);
    expect(error).toHaveBeenLastCalledWith(expect.stringContaining("自动刷新已停止"));
    stop();
    load.mockResolvedValue(completed);
    const result = vi.fn();
    pollSubmission(load, result, error);
    await vi.runAllTimersAsync();
    expect(result).toHaveBeenCalledWith(completed);
  });

  it("does not retry forbidden requests and respects rate limit delay", async () => {
    vi.useFakeTimers();
    const forbidden = vi.fn().mockRejectedValue(new ApiError("forbidden", 403));
    pollSubmission(forbidden, vi.fn(), vi.fn());
    await vi.runAllTimersAsync();
    expect(forbidden).toHaveBeenCalledTimes(1);
    const limited = vi.fn().mockRejectedValueOnce(new ApiError("slow down", 429, undefined, 10)).mockResolvedValue(completed);
    pollSubmission(limited, vi.fn(), vi.fn());
    await vi.advanceTimersByTimeAsync(9999);
    expect(limited).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(1);
    expect(limited).toHaveBeenCalledTimes(2);
  });

  it("ignores responses after unmount", async () => {
    vi.useFakeTimers();
    let resolve!: (value: SubmissionDto) => void;
    const result = vi.fn();
    const stop = pollSubmission(() => new Promise(r => { resolve = r; }), result, vi.fn());
    stop();
    resolve(completed);
    await vi.runAllTimersAsync();
    expect(result).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
  });

  it("refreshes completed choice submissions until the server reveals answers", async () => {
    vi.useFakeTimers();
    const hidden = { ...completed, submissionKind: 2, answersRevealed: false, choiceAnswerRevealAt: new Date(Date.now() + 5000).toISOString() } as SubmissionDto;
    expect(submissionRefreshDelay(hidden)).toBe(5000);
    const revealed = { ...hidden, answersRevealed: true };
    const load = vi.fn().mockResolvedValueOnce(hidden).mockResolvedValue(revealed);
    pollSubmission(load, vi.fn(), vi.fn());
    await vi.advanceTimersByTimeAsync(5000);
    expect(load).toHaveBeenCalledTimes(2);
    expect(vi.getTimerCount()).toBe(0);
  });
});
