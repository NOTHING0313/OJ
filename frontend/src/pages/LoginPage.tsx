import { FormEvent, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { AuthStudioLayout } from "../components/auth/AuthStudioLayout";
import { PasswordInput } from "../components/PasswordInput";

export function LoginPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { login } = useAuth();
  const [account, setAccount] = useState(() => localStorage.getItem("rememberedAccount") ?? "");
  const [password, setPassword] = useState("");
  const [rememberAccount, setRememberAccount] = useState(() => Boolean(localStorage.getItem("rememberedAccount")));
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    if (!account.trim() || !password) {
      setError("请输入账号和密码");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      await login(account, password);
      if (rememberAccount) {
        localStorage.setItem("rememberedAccount", account);
      } else {
        localStorage.removeItem("rememberedAccount");
      }
      navigate(searchParams.get("returnTo") || "/challenges", { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : "登录失败");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <AuthStudioLayout title="欢迎回来">
      <form className="auth-studio-form" onSubmit={handleSubmit} aria-busy={isSubmitting}>
          <label htmlFor="login-account">账号 / 邮箱</label>
          <input
            id="login-account"
            value={account}
            onChange={(event) => setAccount(event.target.value)}
            autoComplete="username"
            required
          />
          <label htmlFor="login-password">密码</label>
          <div id="login-password-field">
            <PasswordInput
              id="login-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
              required
            />
          </div>
          <label className="checkbox-line">
            <input type="checkbox" checked={rememberAccount} onChange={(event) => setRememberAccount(event.target.checked)} />
            记住账号
          </label>
          {error && <div className="alert error" role="alert">{error}</div>}
          <button className="button primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? "正在进入…" : "进入工作室"}
          </button>
      </form>

        <div className="auth-studio-links">
          <Link to="/register">注册</Link>
          <Link to="/forgot-password">找回密码</Link>
        </div>
    </AuthStudioLayout>
  );
}
