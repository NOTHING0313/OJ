import { ChangeEvent, useRef, useState } from "react";
import { uploadImage } from "../api/uploadsApi";
import { normalizeUploadedImagePath, resolveSiteAssetUrl } from "../utils/uploadedImageUrl";

export interface BackgroundAppearanceValue {
  enabled: boolean;
  imageUrl: string | null;
  positionX: number;
  positionY: number;
  scale: number;
  overlayOpacity: number;
}

interface BackgroundAppearanceEditorProps {
  value: BackgroundAppearanceValue;
  onChange: (value: BackgroundAppearanceValue) => void;
  onSave: () => void | Promise<void>;
  onClear?: () => void | Promise<void>;
  isSaving?: boolean;
  title: string;
  description?: string;
  previewTitle?: string;
  previewDescription?: string;
  saveLabel?: string;
  uploadLabel?: string;
  clearLabel?: string;
  onNotice?: (message: string) => void;
  onError?: (message: string) => void;
}

export function BackgroundAppearanceEditor({
  value,
  onChange,
  onSave,
  onClear,
  isSaving = false,
  title,
  description,
  previewTitle,
  previewDescription,
  saveLabel = "保存背景配置",
  uploadLabel = "上传背景图",
  clearLabel = "清除背景",
  onNotice,
  onError
}: BackgroundAppearanceEditorProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const previewUrl = resolveSiteAssetUrl(value.imageUrl);

  function update(patch: Partial<BackgroundAppearanceValue>) {
    onChange({
      ...value,
      ...patch
    });
  }

  async function handleUpload(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    setIsUploading(true);
    onError?.("");

    try {
      const result = await uploadImage(file);
      const normalizedPath = normalizeUploadedImagePath(result.url);
      update({
        imageUrl: normalizedPath,
        enabled: true
      });
      onNotice?.("背景图已上传，请保存配置。");
    } catch (err) {
      onError?.(err instanceof Error ? err.message : "背景图上传失败。");
    } finally {
      setIsUploading(false);
      event.target.value = "";
    }
  }

  function handleClear() {
    if (onClear) {
      void onClear();
      return;
    }

    update({
      imageUrl: null,
      enabled: false
    });
  }

  return (
    <section className="background-appearance-editor">
      <div className="background-editor-fields">
        <div className="admin-panel-header">
          <h2>{title}</h2>
        </div>
        {description && <p className="muted background-editor-description">{description}</p>}

        <div className="form-stack">
          <label className="checkbox-line">
            <input
              type="checkbox"
              checked={value.enabled}
              onChange={(event) => update({ enabled: event.target.checked })}
            />
            启用背景
          </label>

          <label>
            背景图路径
            <input
              value={value.imageUrl ?? ""}
              onChange={(event) => update({ imageUrl: normalizeUploadedImagePath(event.target.value) })}
              placeholder="/uploads/images/background.png"
            />
          </label>

          <div className="button-row align-left background-editor-actions">
            <button className="button" type="button" disabled={isUploading} onClick={() => fileInputRef.current?.click()}>
              {isUploading ? "上传中..." : uploadLabel}
            </button>
            <button className="button" type="button" disabled={isSaving} onClick={handleClear}>
              {clearLabel}
            </button>
            <input
              ref={fileInputRef}
              className="visually-hidden-file"
              type="file"
              accept="image/png,image/jpeg,image/webp"
              onChange={handleUpload}
            />
          </div>

          <div className="background-editor-slider-grid">
            <label>
              X 位置：{value.positionX.toFixed(0)}%
              <input
                className="range-input"
                type="range"
                min="0"
                max="100"
                step="1"
                value={value.positionX}
                onChange={(event) => update({ positionX: Number(event.target.value) })}
              />
            </label>
            <label>
              Y 位置：{value.positionY.toFixed(0)}%
              <input
                className="range-input"
                type="range"
                min="0"
                max="100"
                step="1"
                value={value.positionY}
                onChange={(event) => update({ positionY: Number(event.target.value) })}
              />
            </label>
            <label>
              缩放：{value.scale.toFixed(2)}
              <input
                className="range-input"
                type="range"
                min="0.5"
                max="2.5"
                step="0.05"
                value={value.scale}
                onChange={(event) => update({ scale: Number(event.target.value) })}
              />
            </label>
            <label>
              遮罩：{value.overlayOpacity.toFixed(2)}
              <input
                className="range-input"
                type="range"
                min="0"
                max="1"
                step="0.05"
                value={value.overlayOpacity}
                onChange={(event) => update({ overlayOpacity: Number(event.target.value) })}
              />
            </label>
          </div>

          <button className="button primary" type="button" disabled={isSaving} onClick={onSave}>
            {isSaving ? "保存中..." : saveLabel}
          </button>
        </div>
      </div>

      <div
        className="background-editor-preview"
        style={previewUrl ? {
          backgroundImage: `url("${previewUrl}")`,
          backgroundPosition: `${value.positionX}% ${value.positionY}%`,
          backgroundSize: `${value.scale * 100}% auto`
        } : undefined}
      >
        <div style={{ background: `rgba(0, 0, 0, ${value.overlayOpacity})` }} />
        <article>
          <strong>{previewUrl ? previewTitle ?? "背景预览" : "暂无可用背景"}</strong>
          <span>{previewDescription ?? "拖动滑杆即可实时预览背景位置、缩放和遮罩效果。"}</span>
        </article>
      </div>
    </section>
  );
}
