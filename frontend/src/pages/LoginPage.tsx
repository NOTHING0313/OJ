import { FormEvent, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

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
    <main className="auth-layout auth-v2-layout">
      <section className="auth-card auth-v2-card">
        <div className="auth-brand">
          <img src="/brand/unrealstudio-logo.png" alt="UNREALSTUDIO" />
          <span>虚幻工作室网上答题平台</span>
        </div>
        <div className="page-header auth-v2-header">
          <div>
            <p className="eyebrow">WELCOME BACK</p>
            <h1>登录</h1>
            <p>进入虚幻工作室网上答题平台。</p>
          </div>
        </div>

        <form className="form-stack" onSubmit={handleSubmit}>
          <label>
            账号
            <input value={account} onChange={(event) => setAccount(event.target.value)} autoComplete="username" />
          </label>
          <label>
            密码
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
            />
          </label>
          <label className="checkbox-line">
            <input type="checkbox" checked={rememberAccount} onChange={(event) => setRememberAccount(event.target.checked)} />
            记住账号
          </label>
          {error && <div className="alert error">{error}</div>}
          <button className="button primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? "登录中..." : "登录"}
          </button>
        </form>

        <p className="muted">
          没有账号？<Link to="/register">立即注册</Link>
        </p>
        <p className="muted">
          忘记密码？<Link to="/forgot-password">邮箱找回</Link>
        </p>
      </section>
    </main>
  );
}
