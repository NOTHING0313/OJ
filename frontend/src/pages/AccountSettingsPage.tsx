import { ChangeEvent, FormEvent, useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  confirmAccountDelete,
  getAccountMe,
  getMyAppearance,
  sendAccountDeleteCode,
  updateAvatar,
  updateLeaderboardAnonymity,
  updateMyAppearance,
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
  const privacySavedTimerRef = useRef<number | null>(null);
  const [account, setAccount] = useState<AccountUserDto | null>(null);
  const [appearance, setAppearance] = useState<UserAppearance>(() => createDefaultUserAppearance());
  const [deletePassword, setDeletePassword] = useState("");
  const [deleteCode, setDeleteCode] = useState("");
  const [deleteDebugCode, setDeleteDebugCode] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [isSavingWallpaper, setIsSavingWallpaper] = useState(false);
  const [isSavingLeaderboardPrivacy, setIsSavingLeaderboardPrivacy] = useState(false);
  const [leaderboardPrivacyError, setLeaderboardPrivacyError] = useState<string | null>(null);
  const [leaderboardPrivacySaved, setLeaderboardPrivacySaved] = useState(false);
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

  useEffect(() => () => {
    if (privacySavedTimerRef.current !== null) {
      window.clearTimeout(privacySavedTimerRef.current);
    }
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

  async function handleLeaderboardAnonymityChange(enabled: boolean) {
    if (isSavingLeaderboardPrivacy) {
      return;
    }

    const previousValue = account?.isLeaderboardAnonymous ?? false;
    setIsSavingLeaderboardPrivacy(true);
    setLeaderboardPrivacyError(null);
    setLeaderboardPrivacySaved(false);
    setAccount((current) => current ? { ...current, isLeaderboardAnonymous: enabled } : current);

    try {
      const updated = await updateLeaderboardAnonymity(enabled);
      setAccount(updated);
      updateCurrentUser(updated);
      setLeaderboardPrivacySaved(true);
      if (privacySavedTimerRef.current !== null) {
        window.clearTimeout(privacySavedTimerRef.current);
      }
      privacySavedTimerRef.current = window.setTimeout(() => setLeaderboardPrivacySaved(false), 1800);
    } catch (err) {
      setAccount((current) => current ? { ...current, isLeaderboardAnonymous: previousValue } : current);
      setLeaderboardPrivacyError(err instanceof Error ? err.message : "排行榜匿名设置保存失败");
    } finally {
      setIsSavingLeaderboardPrivacy(false);
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
      await logout();
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

  const roleLabel = accountRoleLabel(account.role);
  const roleClass = accountRoleClass(account.role);

  return (
    <section className="account-settings-page ui-v2-page account-settings-v2-page account-settings-v3-page">
      <div className="page-header ui-v2-page-header account-settings-header-v3">
        <div>
          <p className="eyebrow">ACCOUNT</p>
          <h1>账号设置</h1>
          <p>管理个人资料、界面偏好与账号安全。</p>
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

      <section className="account-overview-card-v3">
        <div className="account-overview-identity-v3">
          {avatarUrl ? (
            <img className="account-overview-avatar-v3" src={avatarUrl} alt={account.userName} />
          ) : (
            <span className="account-overview-avatar-v3 account-avatar-fallback">{avatarInitial}</span>
          )}
          <div className="account-overview-copy-v3">
            <div className="account-overview-title-row-v3">
              <h2>{account.userName}</h2>
              <div className="account-overview-badges-v3">
                <span className={`admin-user-badge ${roleClass}`}>{roleLabel}</span>
                <span className={`admin-user-badge ${account.isBlacklisted ? "admin-user-status-blacklisted" : "admin-user-status-active"}`}>
                  {account.isBlacklisted ? "已限制" : "账号正常"}
                </span>
              </div>
            </div>
            <p>{account.email}</p>
            <span>头像和账号身份信息会用于个人中心、榜单与管理页面展示。</span>
          </div>
        </div>

        <div className="account-overview-actions-v3">
          <button className="button primary" type="button" disabled={isUploading} onClick={() => fileInputRef.current?.click()}>
            {isUploading ? "上传中..." : "更换头像"}
          </button>
          <input ref={fileInputRef} className="visually-hidden-file" type="file" accept="image/png,image/jpeg,image/webp" onChange={handleAvatarChange} />
        </div>
      </section>

      {account.role === 1 && (
        <section className="leaderboard-privacy-setting" aria-labelledby="leaderboard-privacy-title">
          <div className="leaderboard-privacy-copy">
            <div className="leaderboard-privacy-title-row">
              <h2 id="leaderboard-privacy-title">排行榜匿名</h2>
              <button
                className={`site-settings-switch ${account.isLeaderboardAnonymous ? "active" : ""}`}
                type="button"
                role="switch"
                aria-checked={account.isLeaderboardAnonymous}
                aria-label="排行榜匿名"
                disabled={isSavingLeaderboardPrivacy}
                onClick={() => void handleLeaderboardAnonymityChange(!account.isLeaderboardAnonymous)}
              >
                <span aria-hidden="true" />
              </button>
            </div>
            <p>公开榜单将显示匿名代号，管理账号仍可查看真实身份。</p>
            {leaderboardPrivacySaved && <span className="leaderboard-privacy-saved" role="status">已保存</span>}
            {leaderboardPrivacyError && <span className="leaderboard-privacy-error" role="alert">{leaderboardPrivacyError}</span>}
          </div>
        </section>
      )}

      <section className="account-section-v3 account-personalization-section-v3">
        <div className="account-section-heading-v3">
          <div>
            <p className="eyebrow">PERSONALIZATION</p>
            <h2>外观与个性化</h2>
          </div>
          <p>配置账号专属背景与浏览器界面风格，不影响其他用户。</p>
        </div>

        <div className="account-personalization-grid-v3">
          <section className="admin-panel account-card account-wallpaper-card account-wallpaper-card-v3">
            <BackgroundAppearanceEditor
              value={wallpaperEditorValue}
              onChange={updateWallpaperFromEditor}
              onSave={handleSaveWallpaper}
              onClear={handleClearWallpaper}
              isSaving={isSavingWallpaper}
              title="个人壁纸"
              description="仅在 Root 配置风格下生效。启用后覆盖平台背景，清除后自动回退到 Root 背景。"
              previewTitle={appearance.backgroundImageUrl ? "个人壁纸预览" : "尚未设置个人壁纸"}
              previewDescription="调整位置、缩放和遮罩后可实时查看效果。"
              saveLabel="保存个人壁纸"
              uploadLabel="上传壁纸"
              clearLabel="清除壁纸"
              onNotice={setNotice}
              onError={setError}
            />
          </section>

          <section className="admin-panel account-card account-theme-card-v3">
            <div className="admin-panel-header">
              <div>
                <p className="eyebrow">THEME</p>
                <h2>界面风格</h2>
              </div>
            </div>
            <p className="account-card-description-v3">选择当前浏览器使用的界面方案。</p>
            <div className="theme-choice-group">
              <label className={`theme-choice-card ${currentTheme === "default" ? "active" : ""}`}>
                <input
                  type="radio"
                  name="theme"
                  checked={currentTheme === "default"}
                  onChange={() => setTheme("default")}
                />
                <span>
                  <strong>默认暗色</strong>
                  <small>使用稳定的深色界面，不展示站点背景图。</small>
                </span>
                <span className="theme-choice-state-v3">{currentTheme === "default" ? "当前" : "选择"}</span>
              </label>
              <label className={`theme-choice-card ${currentTheme === "mystic-background" ? "active" : ""}`}>
                <input
                  type="radio"
                  name="theme"
                  checked={currentTheme === "mystic-background"}
                  onChange={() => setTheme("mystic-background")}
                />
                <span>
                  <strong>沉浸背景</strong>
                  <small>使用 Root 页面背景与半透明面板，并叠加你的个人壁纸配置。</small>
                </span>
                <span className="theme-choice-state-v3">{currentTheme === "mystic-background" ? "当前" : "选择"}</span>
              </label>
            </div>
          </section>
        </div>
      </section>

      <section className="account-section-v3">
        <div className="account-section-heading-v3">
          <div>
            <p className="eyebrow">SECURITY</p>
            <h2>登录与安全</h2>
          </div>
          <p>管理账号安全，并在需要时注销账号。</p>
        </div>

        <div className="account-security-grid-v3">
          <section className="admin-panel account-card account-danger-zone account-danger-zone-v3">
            <div className="admin-panel-header account-card-title-v3">
              <div>
                <span className="account-card-icon-v3 danger">!</span>
                <div>
                  <h2>注销账号</h2>
                  <p>永久操作，执行前需要密码与邮箱验证码。</p>
                </div>
              </div>
              <span className="account-danger-label-v3">危险操作</span>
            </div>

            <div className="account-danger-note-v3">
              注销后账号资料会匿名化，历史提交与挑战记录仍会保留用于平台统计。此操作不可恢复。
            </div>

            <form className="form-stack account-security-form account-security-form-v3" onSubmit={handleDeleteAccount}>
              <label>
                当前密码
                <input type="password" value={deletePassword} onChange={(event) => setDeletePassword(event.target.value)} autoComplete="current-password" placeholder="输入当前账号密码" />
              </label>

              <div className="inline-action-row">
                <label>
                  邮箱验证码
                  <input value={deleteCode} onChange={(event) => setDeleteCode(event.target.value)} placeholder="6 位验证码" inputMode="numeric" maxLength={6} />
                </label>
                <button className="button danger-button" type="button" disabled={isSendingDeleteCode} onClick={handleSendDeleteCode}>
                  {isSendingDeleteCode ? "发送中..." : "发送验证码"}
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
    </section>
  );
}

function accountRoleLabel(role: number) {
  if (role === 3) {
    return "Root";
  }

  if (role === 2) {
    return "出题人";
  }

  return "答题人";
}

function accountRoleClass(role: number) {
  if (role === 3) {
    return "admin-user-role-root";
  }

  if (role === 2) {
    return "admin-user-role-problem-setter";
  }

  return "admin-user-role-answerer";
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
