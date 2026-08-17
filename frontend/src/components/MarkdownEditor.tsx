import { ChangeEvent, useRef, useState } from "react";
import { uploadImage } from "../api/uploadsApi";
import { MarkdownRenderer } from "./MarkdownRenderer";

interface MarkdownEditorProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
}

export function MarkdownEditor({ label, value, onChange, required }: MarkdownEditorProps) {
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);

  async function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";

    if (!file) {
      return;
    }

    const token = localStorage.getItem("accessToken");
    if (!token) {
      setMessage("请先登录后再上传图片。");
      return;
    }

    setIsUploading(true);
    setMessage(null);

    try {
      const result = await uploadImage(file);
      insertText(`\n\n![image](${result.url})\n\n`);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : "图片上传失败。");
    } finally {
      setIsUploading(false);
    }
  }

  function insertText(text: string) {
    const textarea = textareaRef.current;

    if (!textarea) {
      onChange(`${value}\n${text}`);
      return;
    }

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const nextValue = `${value.slice(0, start)}${text}${value.slice(end)}`;
    onChange(nextValue);

    window.requestAnimationFrame(() => {
      textarea.focus();
      const cursor = start + text.length;
      textarea.setSelectionRange(cursor, cursor);
    });
  }

  return (
    <div className="markdown-editor">
      <div className="markdown-editor-header">
        <span>{label}</span>
        <button className="button" type="button" disabled={isUploading} onClick={() => fileInputRef.current?.click()}>
          {isUploading ? "上传中..." : "上传图片"}
        </button>
      </div>
      <textarea ref={textareaRef} value={value} onChange={(event) => onChange(event.target.value)} required={required} />
      <input
        ref={fileInputRef}
        type="file"
        accept=".png,.jpg,.jpeg,.webp,.gif,image/png,image/jpeg,image/webp,image/gif"
        hidden
        onChange={handleFileChange}
      />
      {message && <div className="alert error">{message}</div>}
      <div className="markdown-preview">
        <MarkdownRenderer value={value || "_预览会显示在这里。_"} />
      </div>
    </div>
  );
}
