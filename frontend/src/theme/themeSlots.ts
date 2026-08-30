export const themeIconSlotOptions = [
  { key: "problem", label: "题目" },
  { key: "challenge", label: "挑战" },
  { key: "leaderboard", label: "榜单" },
  { key: "team", label: "战队" },
  { key: "submission", label: "提交" },
  { key: "help", label: "帮助" },
  { key: "profile", label: "个人中心" },
  { key: "chat", label: "聊天" },
  { key: "git", label: "Git" },
  { key: "season", label: "赛季" },
  { key: "reward", label: "奖励" }
] as const;

export const themeDecorationSlotOptions = [
  { key: "pageHeader", label: "Page Header" },
  { key: "cardHeader", label: "Card Header" },
  { key: "panelCorner", label: "Panel Corner" },
  { key: "emptyState", label: "Empty State" }
] as const;

export type ThemeIconSlot = typeof themeIconSlotOptions[number]["key"];
export type ThemeDecorationSlot = typeof themeDecorationSlotOptions[number]["key"];

export function isThemeIconSlot(value: string): value is ThemeIconSlot {
  return themeIconSlotOptions.some((slot) => slot.key === value);
}

export function isThemeDecorationSlot(value: string): value is ThemeDecorationSlot {
  return themeDecorationSlotOptions.some((slot) => slot.key === value);
}
