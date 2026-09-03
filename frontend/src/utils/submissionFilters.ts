import type { JudgeLanguage, JudgeStatus } from "../api/submissionsApi";

export function parseLanguage(value: string): JudgeLanguage | "" {
  return value ? (Number(value) as JudgeLanguage) : "";
}

export function parseStatus(value: string): JudgeStatus | "" {
  return value ? (Number(value) as JudgeStatus) : "";
}
