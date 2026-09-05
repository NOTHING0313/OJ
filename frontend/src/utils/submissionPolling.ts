import { ApiError } from "../api/httpClient";
import type { SubmissionDto } from "../api/submissionsApi";

export function submissionRefreshDelay(item: SubmissionDto, now = Date.now()): number | null {
  if (!item.finishedAt) return 2000;
  if (item.submissionKind !== 2 || item.answersRevealed !== false || !item.choiceAnswerRevealAt) return null;
  const revealAt = Date.parse(item.choiceAnswerRevealAt);
  return Number.isFinite(revealAt) ? Math.min(60_000, Math.max(2000, revealAt - now)) : null;
}

// Each page owns and disposes its poller; late responses cannot update a departed page.
export function pollSubmission(load: () => Promise<SubmissionDto>, onResult: (item: SubmissionDto) => boolean | void, onError: (message: string) => void) {
  let stopped = false;
  let failures = 0;
  let timer: ReturnType<typeof setTimeout> | undefined;
  async function refresh() {
    try {
      const item = await load();
      if (stopped) return;
      failures = 0;
      if (onResult(item) === false || stopped) return;
      const delay = submissionRefreshDelay(item);
      if (delay !== null) timer = setTimeout(refresh, delay);
    } catch (error) {
      if (stopped) return;
      failures++;
      const retryable = !(error instanceof ApiError) || error.status === 0 || error.status === 408 || error.status === 429 || error.status >= 500;
      const retry = retryable && failures <= 3;
      onError(`${error instanceof Error ? error.message : "加载提交失败"}。${retry ? "正在自动重试…" : "自动刷新已停止，请手动重试。"}`);
      if (retry) timer = setTimeout(refresh, Math.max(2000 * 2 ** (failures - 1), error instanceof ApiError ? (error.retryAfterSeconds ?? 0) * 1000 : 0));
    }
  }
  void refresh();
  return () => { stopped = true; clearTimeout(timer); };
}
