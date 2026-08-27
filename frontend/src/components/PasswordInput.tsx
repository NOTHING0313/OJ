import { InputHTMLAttributes, useState } from "react";

type PasswordInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, "type">;

export function PasswordInput(props: PasswordInputProps) {
  const [isVisible, setIsVisible] = useState(false);

  return (
    <div className="password-field">
      <input {...props} type={isVisible ? "text" : "password"} />
      <button
        className="password-visibility-button"
        type="button"
        aria-label={isVisible ? "隐藏密码" : "显示密码"}
        aria-pressed={isVisible}
        title={isVisible ? "隐藏密码" : "显示密码"}
        onClick={() => setIsVisible((value) => !value)}
      >
        {isVisible ? (
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M3 3l18 18M10.6 10.7a2 2 0 0 0 2.7 2.7M9.9 4.2A10.9 10.9 0 0 1 12 4c5.2 0 9.2 4.1 10 8a11.9 11.9 0 0 1-2.7 4.8M6.2 6.2A11.5 11.5 0 0 0 2 12c.8 3.9 4.8 8 10 8 1.5 0 2.9-.3 4.1-.8" />
          </svg>
        ) : (
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z" />
            <circle cx="12" cy="12" r="3" />
          </svg>
        )}
      </button>
    </div>
  );
}
