import { FormEvent, useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { register, sendRegisterEmailCode } from "../api/authApi";

export function RegisterPage() {
  const navigate = useNavigate();
  const [userName, setUserName] = useState("");
  const [email, setEmail] = useState("");
  const [avatarUrl, setAvatarUrl] = useState("");
  const [emailCode, setEmailCode] = useState("");
  const [debugCode, setDebugCode] = useState<string | null>(null);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [cooldown, setCooldown] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isSendingCode, setIsSendingCode] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (cooldown <= 0) {
      return;
    }

    const timer = window.setTimeout(() => setCooldown((value) => value - 1), 1000);
    return () => window.clearTimeout(timer);
  }, [cooldown]);

  async function handleSendCode() {
    if (!email.trim()) {
      setError("请输入邮箱");
      return;
    }

    setIsSendingCode(true);
    setError(null);
    setNotice(null);
    setDebugCode(null);

    try {
      const result = await sendRegisterEmailCode(email.trim());
      setDebugCode(result.debugCode ?? null);
      setNotice(result.message || "验证码已发送。");
      setCooldown(60);
    } catch (err) {
      setError(err instanceof Error ? err.message : "验证码发送失败");
    } finally {
      setIsSendingCode(false);
    }
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    if (!emailCode.trim()) {
      setError("请输入邮箱验证码");
      return;
    }

    if (password !== confirmPassword) {
      setError("两次输入的密码不一致");
      return;
    }

    if (password.length < 6) {
      setError("密码至少需要 6 个字符");
      return;
    }

    setIsSubmitting(true);
    setError(null);
    setNotice(null);

    try {
      await register({
        userName: userName.trim(),
        email: email.trim(),
        password,
        emailCode: emailCode.trim(),
        avatarUrl: avatarUrl.trim() || undefined
      });
      setNotice("注册成功，请登录。");
      window.setTimeout(() => navigate("/login"), 600);
    } catch (err) {
      setError(err instanceof Error ? err.message : "注册失败");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-layout">
      <section className="auth-card">
        <div className="auth-brand">
          <img src="/brand/unrealstudio-logo.png" alt="UNREALSTUDIO" />
          <span>虚幻工作室网上答题平台</span>
        </div>
        <div className="page-header">
          <div>
            <h1>注册账号</h1>
            <p>注册后默认成为答题人账号。请先完成邮箱验证码校验。</p>
          </div>
        </div>

        <form className="form-stack" onSubmit={handleSubmit}>
          <label>
            用户名
            <input value={userName} onChange={(event) => setUserName(event.target.value)} autoComplete="username" required />
          </label>
          <label>
            邮箱
            <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="email" required />
          </label>
          <div className="inline-action-row">
            <label>
              邮箱验证码
              <input value={emailCode} onChange={(event) => setEmailCode(event.target.value)} inputMode="numeric" maxLength={6} placeholder="6 位验证码" required />
            </label>
            <button className="button" type="button" disabled={isSendingCode || cooldown > 0} onClick={handleSendCode}>
              {isSendingCode ? "发送中..." : cooldown > 0 ? `${cooldown}s` : "发送验证码"}
            </button>
          </div>
          {debugCode && <div className="quiet-note">开发环境验证码：{debugCode}</div>}
          <label>
            头像 URL（可选）
            <input value={avatarUrl} onChange={(event) => setAvatarUrl(event.target.value)} placeholder="/uploads/images/..." />
          </label>
          <label>
            密码
            <input type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="new-password" required />
          </label>
          <label>
            确认密码
            <input type="password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} autoComplete="new-password" required />
          </label>
          {notice && <div className="quiet-note success">{notice}</div>}
          {error && <div className="alert error">{error}</div>}
          <button className="button primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? "注册中..." : "注册"}
          </button>
        </form>

        <p className="muted">
          已有账号？<Link to="/login">去登录</Link>
        </p>
      </section>
    </main>
  );
}
