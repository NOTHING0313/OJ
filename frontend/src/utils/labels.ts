import type { JudgeLanguage, JudgeStatus } from "../api/submissionsApi";
import type { TestCaseVisibility } from "../api/problemsApi";

export function languageLabel(language: JudgeLanguage): string {
  switch (language) {
    case 1:
      return "C++17";
    case 2:
      return "C11";
    case 3:
      return "C#";
  }
}

export function statusLabel(status: JudgeStatus): string {
  switch (status) {
    case 1:
      return "等待中";
    case 2:
      return "判题中";
    case 3:
      return "通过";
    case 4:
      return "答案错误";
    case 5:
      return "超出时间限制";
    case 6:
      return "超出内存限制";
    case 7:
      return "运行错误";
    case 8:
      return "编译错误";
    case 9:
      return "系统错误";
  }
}

export function visibilityLabel(visibility: TestCaseVisibility): string {
  return visibility === 1 ? "示例" : "隐藏";
}

export function formatDate(value: string | null): string {
  if (!value) {
    return "-";
  }

  return new Date(value).toLocaleString("zh-CN");
}
