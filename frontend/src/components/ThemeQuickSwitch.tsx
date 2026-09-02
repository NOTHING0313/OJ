import { useTheme } from "../theme/ThemeContext";

export function ThemeQuickSwitch({ interactive = true }: { interactive?: boolean }) {
  const { currentTheme, toggleTheme } = useTheme();
  const isRootConfigured = currentTheme === "mystic-background";
  const hint = isRootConfigured
    ? "当前：Root 配置风格，点击切换到默认风格"
    : "当前：默认风格，点击切换到 Root 配置风格";

  return (
    <button
      className={`theme-quick-switch ${isRootConfigured ? "active" : ""}`}
      type="button"
      title={hint}
      aria-label={hint}
      aria-pressed={isRootConfigured}
      onClick={interactive ? toggleTheme : undefined}
    >
      <span aria-hidden="true" />
    </button>
  );
}
