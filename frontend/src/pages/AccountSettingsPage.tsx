import { ChangeEvent, FormEvent, useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  confirmAccountDelete,
  getAccountMe,
  getMyAppearance,
  sendAccountDeleteCode,
  sendPhoneCode,
  updateAvatar,
  updateMyAppearance,
  verifyPhone,
  type AccountUserDto,
  type UserAppearance
} from "../api/accountApi";
import { uploadImage } from "../api/uploadsApi";
import { useAuth } from "../auth/AuthContext";
import { BackgroundAppearanceEditor, type BackgroundAppearanceValue } from "../components/BackgroundAppearanceEditor";
import { useTheme } from "../theme/ThemeContext";

export function AccountSettingsPage() {
  const navigate = useNavigate();
  const { currentUser, logout, updateCurrentUser } = useAuth();
  const { currentTheme, setTheme, reloadUserAppearance, updateUserAppearanceLocal } = useTheme();
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [account, setAccount] = useState<AccountUserDto | null>(null);
  const [appearance, setAppearance] = useState<UserAppearance>(() => createDefaultUserAppearance());
  const [phoneNumber, setPhoneNumber] = useState("");
  const [code, setCode] = useState("");
  const [debugCode, setDebugCode] = useState<string | null>(null);
  const [deletePassword, setDeletePassword] = useState("");
  const [deleteCode, setDeleteCode] = useState("");
  const [deleteDebugCode, setDeleteDebugCode] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [isSavingWallpaper, setIsSavingWallpaper] = useState(false);
  const [isSendingCode, setIsSendingCode] = useState(false);
  const [isVerifying, setIsVerifying] = useState(false);
  const [isSendingDeleteCode, setIsSendingDeleteCode] = useState(false);
  const [isDeletingAccount, setIsDeletingAccount] = useState(false);

  useEffect(() => {
    let ignore = false;

    async function loadAccount() {
      try {
        setIsLoading(true);
        const [data, appearanceData] = await Promise.all([
          getAccountMe(),
          getMyAppearance().catch(() => createDefaultUserAppearance())
        ]);
        if (!ignore) {
          setAccount(data);
          setAppearance(appearanceData);
          setPhoneNumber("");
          setError(null);
        }
      } catch (err) {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "账号资料加载失败");
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    void loadAccount();

    return () => {
      ignore = true;
    };
  }, []);

  async function handleAvatarChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    setIsUploading(true);
    setError(null);
    setNotice(null);

    try {
      const uploadResult = await uploadImage(file);
      const updated = await updateAvatar(uploadResult.url);
      setAccount(updated);
      updateCurrentUser(updated);
      setNotice("头像已更新");
    } catch (err) {
      setError(err instanceof Error ? err.message : "头像上传失败");
    } finally {
      setIsUploading(false);
      event.target.value = "";
    }
  }

  async function handleSaveWallpaper() {
    setIsSavingWallpaper(true);
    setError(null);
    setNotice(null);

    try {
      const saved = await updateMyAppearance({
        ...appearance,
        backgroundImageUrl: appearance.backgroundImageUrl || null
      });
      setAppearance(saved);
      updateUserAppearanceLocal(saved);
      await reloadUserAppearance();
      setNotice("个人壁纸配置已保存");
    } catch (err) {
      setError(err instanceof Error ? err.message : "个人壁纸保存失败");
    } finally {
      setIsSavingWallpaper(false);
    }
  }

  async function handleClearWallpaper() {
    setIsSavingWallpaper(true);
    setError(null);
    setNotice(null);

    try {
      const cleared = await updateMyAppearance({
        ...createDefaultUserAppearance(),
        backgroundImageUrl: null,
        backgroundEnabled: false
      });
      setAppearance(cleared);
      updateUserAppearanceLocal(cleared);
      await reloadUserAppearance();
      setNotice("个人壁纸已清除");
    } catch (err) {
      setError(err instanceof Error ? err.message : "个人壁纸清除失败");
    } finally {
      setIsSavingWallpaper(false);
    }
  }

  function updateWallpaperFromEditor(value: BackgroundAppearanceValue) {
    setAppearance({
      backgroundEnabled: value.enabled,
      backgroundImageUrl: value.imageUrl,
      positionX: value.positionX,
      positionY: value.positionY,
      scale: value.scale,
      overlayOpacity: value.overlayOpacity
    });
  }

  const wallpaperEditorValue: BackgroundAppearanceValue = {
    enabled: appearance.backgroundEnabled,
    imageUrl: appearance.backgroundImageUrl,
    positionX: appearance.positionX,
    positionY: appearance.positionY,
    scale: appearance.scale,
    overlayOpacity: appearance.overlayOpacity
  };

  async function handleSendCode() {
    if (!phoneNumber.trim()) {
      setError("请输入手机号");
      return;
    }

    setIsSendingCode(true);
    setError(null);
    setNotice(null);
    setDebugCode(null);

    try {
      const result = await sendPhoneCode(phoneNumber.trim());
      setDebugCode(result.debugCode ?? null);
      setNotice(result.message || "验证码已发送");
    } catch (err) {
      setError(err instanceof Error ? err.message : "验证码发送失败");
    } finally {
      setIsSendingCode(false);
    }
  }

  async function handleVerifyPhone(event: FormEvent) {
    event.preventDefault();

    if (!phoneNumber.trim() || !code.trim()) {
      setError("请输入手机号和验证码");
      return;
    }

    setIsVerifying(true);
    setError(null);
    setNotice(null);

    try {
      const updated = await verifyPhone(phoneNumber.trim(), code.trim());
      setAccount(updated);
      updateCurrentUser(updated);
      setPhoneNumber("");
      setCode("");
      setDebugCode(null);
      setNotice("手机号已绑定");
    } catch (err) {
      setError(err instanceof Error ? err.message : "手机号绑定失败");
    } finally {
      setIsVerifying(false);
    }
  }

  async function handleSendDeleteCode() {
    setIsSendingDeleteCode(true);
    setError(null);
    setNotice(null);
    setDeleteDebugCode(null);

    try {
      const result = await sendAccountDeleteCode();
      setDeleteDebugCode(result.debugCode ?? null);
      setNotice(result.message || "注销验证码已发送。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "注销验证码发送失败");
    } finally {
      setIsSendingDeleteCode(false);
    }
  }

  async function handleDeleteAccount(event: FormEvent) {
    event.preventDefault();

    if (!deletePassword || !deleteCode.trim()) {
      setError("请输入当前密码和邮箱验证码");
      return;
    }

    const confirmed = window.confirm("账号注销不可恢复，历史提交会保留但账号将匿名化。确认继续吗？");
    if (!confirmed) {
      return;
    }

    setIsDeletingAccount(true);
    setError(null);
    setNotice(null);

    try {
      await confirmAccountDelete(deleteCode.trim(), deletePassword);
      logout();
      navigate("/login", { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : "账号注销失败");
    } finally {
      setIsDeletingAccount(false);
    }
  }

  if (isLoading) {
    return <div className="state-line">正在加载账号设置...</div>;
  }

  if (!account) {
    return <div className="alert error">{error ?? "账号设置不可用"}</div>;
  }

  const avatarUrl = account.avatarUrl || currentUser?.avatarUrl;
  const avatarInitial = (account.userName || currentUser?.userName || "U").slice(0, 1).toUpperCase();

  return (
    <section className="account-settings-page">
      <div className="page-header">
        <div>
          <p className="eyebrow">ACCOUNT</p>
          <h1>账号设置</h1>
          <p>维护头像、手机号和账号安全信息。</p>
        </div>
        <Link className="button" to="/profile/me">
          返回个人中心
        </Link>
      </div>

      {(notice || error) && (
        <div className={error ? "alert error" : "quiet-note success"}>
          {error ?? notice}
        </div>
      )}

      <div className="account-settings-grid">
        <section className="admin-panel account-card">
          <div className="admin-panel-header">
            <h2>基本资料</h2>
          </div>
          <div className="account-avatar-block">
            {avatarUrl ? (
              <img className="account-avatar-preview" src={avatarUrl} alt={account.userName} />
            ) : (
              <span className="account-avatar-fallback">{avatarInitial}</span>
            )}
            <div>
              <strong>{account.userName}</strong>
              <p className="muted">{account.email}</p>
              <button className="button primary" type="button" disabled={isUploading} onClick={() => fileInputRef.current?.click()}>
                {isUploading ? "上传中..." : "上传头像"}
              </button>
              <input ref={fileInputRef} className="visually-hidden-file" type="file" accept="image/png,image/jpeg,image/webp" onChange={handleAvatarChange} />
            </div>
          </div>
        </section>

        <section className="admin-panel account-card account-wallpaper-card">
          <BackgroundAppearanceEditor
            value={wallpaperEditorValue}
            onChange={updateWallpaperFromEditor}
            onSave={handleSaveWallpaper}
            onClear={handleClearWallpaper}
            isSaving={isSavingWallpaper}
            title="个人壁纸"
            description="个人背景仅在 Root 配置风格下生效。启用后覆盖 Root 背景；清除后自动回退 Root 背景。"
            previewTitle={appearance.backgroundImageUrl ? "个人壁纸预览" : "尚未设置个人壁纸"}
            previewDescription="调整位置、缩放和遮罩后可实时查看效果。"
            saveLabel="保存个人壁纸"
            uploadLabel="上传壁纸"
            clearLabel="清除壁纸"
            onNotice={setNotice}
            onError={setError}
          />
        </section>

        <section className="admin-panel account-card">
          <div className="admin-panel-header">
            <h2>界面风格</h2>
          </div>
          <div className="theme-choice-group">
            <label className="theme-choice-card">
              <input
                type="radio"
                name="theme"
                checked={currentTheme === "default"}
                onChange={() => setTheme("default")}
              />
              <span>
                <strong>默认风格</strong>
                <small>保持当前暗色界面，不显示 Root 配置背景。</small>
              </span>
            </label>
            <label className="theme-choice-card">
              <input
                type="radio"
                name="theme"
                checked={currentTheme === "mystic-background"}
                onChange={() => setTheme("mystic-background")}
              />
              <span>
                <strong>Root 配置风格</strong>
                <small>使用 Root 为各页面配置的背景和半透明界面；该选择只保存在当前浏览器。</small>
              </span>
            </label>
          </div>
        </section>

        <section className="admin-panel account-card">
          <div className="admin-panel-header">
            <h2>手机号绑定</h2>
          </div>
          <form className="form-stack account-security-form" onSubmit={handleVerifyPhone}>
            <div className="profile-fact">
              <span>当前手机号</span>
              <strong>{account.phoneNumberConfirmed ? account.phoneNumberMasked ?? "已绑定" : "未绑定"}</strong>
            </div>
            <label>
              新手机号
              <input value={phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} placeholder="请输入 11 位手机号" inputMode="tel" />
            </label>
            <div className="inline-action-row">
              <label>
                验证码
                <input value={code} onChange={(event) => setCode(event.target.value)} placeholder="6 位验证码" inputMode="numeric" maxLength={6} />
              </label>
              <button className="button" type="button" disabled={isSendingCode} onClick={handleSendCode}>
                {isSendingCode ? "发送中..." : "发送验证码"}
              </button>
            </div>
            {debugCode && <div className="quiet-note">开发环境验证码：{debugCode}</div>}
            <button className="button primary" type="submit" disabled={isVerifying}>
              {isVerifying ? "绑定中..." : "绑定 / 修改手机号"}
            </button>
          </form>
        </section>

        <section className="admin-panel account-card account-danger-zone">
          <div className="admin-panel-header">
            <h2>账号安全</h2>
          </div>
          <p className="muted">
            注销会匿名化账号资料，但保留历史提交与挑战记录用于平台统计。
          </p>
          <form className="form-stack account-security-form" onSubmit={handleDeleteAccount}>
            <label>
              当前密码
              <input type="password" value={deletePassword} onChange={(event) => setDeletePassword(event.target.value)} autoComplete="current-password" />
            </label>
            <div className="inline-action-row">
              <label>
                邮箱验证码
                <input value={deleteCode} onChange={(event) => setDeleteCode(event.target.value)} placeholder="6 位验证码" inputMode="numeric" maxLength={6} />
              </label>
              <button className="button danger-button" type="button" disabled={isSendingDeleteCode} onClick={handleSendDeleteCode}>
                {isSendingDeleteCode ? "发送中..." : "发送注销验证码"}
              </button>
            </div>
            {deleteDebugCode && <div className="quiet-note">开发环境注销验证码：{deleteDebugCode}</div>}
            <button className="button danger-button solid" type="submit" disabled={isDeletingAccount}>
              {isDeletingAccount ? "注销中..." : "确认注销账号"}
            </button>
          </form>
        </section>
      </div>
    </section>
  );
}

function createDefaultUserAppearance(): UserAppearance {
  return {
    backgroundImageUrl: null,
    backgroundEnabled: false,
    positionX: 50,
    positionY: 50,
    scale: 1,
    overlayOpacity: 0.65
  };
}
