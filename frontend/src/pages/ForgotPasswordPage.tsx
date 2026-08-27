import { FormEvent, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { confirmEmailPasswordReset, sendEmailPasswordResetCode } from "../api/accountApi";

export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  const [debugCode, setDebugCode] = useState<string | null>(null);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSendingCode, setIsSendingCode] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

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
      const result = await sendEmailPasswordResetCode(email.trim());
      setDebugCode(result.debugCode ?? null);
      setNotice(result.message || "如果该邮箱存在，验证码将会发送。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "验证码发送失败");
    } finally {
      setIsSendingCode(false);
    }
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    if (newPassword !== confirmPassword) {
      setError("两次输入的密码不一致");
      return;
    }

    if (newPassword.length < 6) {
      setError("新密码至少需要 6 个字符");
      return;
    }

    setIsSubmitting(true);
    setError(null);
    setNotice(null);

    try {
      await confirmEmailPasswordReset(email.trim(), code.trim(), newPassword);
      setNotice("密码已重置，请使用新密码登录。");
      window.setTimeout(() => navigate("/login"), 800);
    } catch (err) {
      setError(err instanceof Error ? err.message : "密码重置失败");
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
            <p className="eyebrow">ACCOUNT RECOVERY</p>
            <h1>找回密码</h1>
            <p>通过账号邮箱接收验证码并重置密码。若邮箱存在，验证码将会发送。</p>
          </div>
        </div>

        <form className="form-stack" onSubmit={handleSubmit}>
          <label>
            邮箱
            <input value={email} onChange={(event) => setEmail(event.target.value)} inputMode="email" autoComplete="email" placeholder="请输入注册邮箱" />
          </label>
          <div className="inline-action-row">
            <label>
              验证码
              <input value={code} onChange={(event) => setCode(event.target.value)} inputMode="numeric" maxLength={6} placeholder="6 位验证码" />
            </label>
            <button className="button" type="button" disabled={isSendingCode} onClick={handleSendCode}>
              {isSendingCode ? "发送中..." : "发送验证码"}
            </button>
          </div>
          {debugCode && <div className="quiet-note">开发环境验证码：{debugCode}</div>}
          <label>
            新密码
            <input type="password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} autoComplete="new-password" />
          </label>
          <label>
            确认新密码
            <input type="password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} autoComplete="new-password" />
          </label>
          {notice && <div className="quiet-note success">{notice}</div>}
          {error && <div className="alert error">{error}</div>}
          <button className="button primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? "重置中..." : "重置密码"}
          </button>
        </form>

        <p className="muted">
          想起密码了？<Link to="/login">返回登录</Link>
        </p>
      </section>
    </main>
  );
}
