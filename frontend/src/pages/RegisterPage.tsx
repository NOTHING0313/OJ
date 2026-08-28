import { FormEvent, useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { register, sendRegisterEmailCode } from "../api/authApi";
import { AuthStudioLayout } from "../components/auth/AuthStudioLayout";
import { PasswordInput } from "../components/PasswordInput";

export function RegisterPage() {
  const navigate = useNavigate();
  const [userName, setUserName] = useState("");
  const [email, setEmail] = useState("");
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
        emailCode: emailCode.trim()
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
    <AuthStudioLayout title="创建系统账号">
      <form className="auth-studio-form" onSubmit={handleSubmit} aria-busy={isSubmitting || isSendingCode}>
          <label htmlFor="register-username">用户名</label>
          <input id="register-username" value={userName} onChange={(event) => setUserName(event.target.value)} autoComplete="username" required />
          <label htmlFor="register-email">邮箱</label>
          <input id="register-email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="email" required />
          <div className="inline-action-row">
            <div className="auth-studio-field">
              <label htmlFor="register-email-code">邮箱验证码</label>
              <input id="register-email-code" value={emailCode} onChange={(event) => setEmailCode(event.target.value)} inputMode="numeric" maxLength={6} placeholder="6 位验证码" autoComplete="one-time-code" required />
            </div>
            <button className="button" type="button" disabled={isSendingCode || cooldown > 0} onClick={handleSendCode}>
              {isSendingCode ? "发送中..." : cooldown > 0 ? `${cooldown}s` : "发送验证码"}
            </button>
          </div>
          {debugCode && <div className="quiet-note">开发环境验证码：{debugCode}</div>}
          <label htmlFor="register-password">密码</label>
          <PasswordInput id="register-password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="new-password" required />
          <label htmlFor="register-confirm-password">确认密码</label>
          <PasswordInput id="register-confirm-password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} autoComplete="new-password" required />
          {notice && <div className="quiet-note success">{notice}</div>}
          {error && <div className="alert error" role="alert">{error}</div>}
          <button className="button primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? "正在创建…" : "创建系统账号"}
          </button>
      </form>

        <div className="auth-studio-links">
          <Link to="/login">返回登录</Link>
        </div>
    </AuthStudioLayout>
  );
}
