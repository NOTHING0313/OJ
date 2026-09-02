import { type ReactNode, useEffect, useRef } from "react";

const focusableSelector = [
  "button:not([disabled])",
  "[href]",
  "input:not([disabled])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "[tabindex]:not([tabindex='-1'])"
].join(",");

export function ThemeEditorDialog({ titleId, descriptionId, className = "", onCancel, children }: {
  titleId: string;
  descriptionId?: string;
  className?: string;
  onCancel?: () => void;
  children: ReactNode;
}) {
  const dialogRef = useRef<HTMLElement>(null);
  const cancelRef = useRef(onCancel);
  cancelRef.current = onCancel;

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    const activeDialog = dialog;
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const openDialogs = [...document.querySelectorAll<HTMLElement>(".theme-editor-modal")];
    const previousDialog = openDialogs.length > 1 ? openDialogs[openDialogs.length - 2] : null;
    const previousOverflow = document.body.style.overflow;
    if (previousDialog) {
      previousDialog.setAttribute("aria-hidden", "true");
      previousDialog.inert = true;
    }
    document.body.style.overflow = "hidden";

    const focusTarget = activeDialog.querySelector<HTMLElement>("[data-dialog-autofocus]")
      ?? activeDialog.querySelector<HTMLElement>(focusableSelector)
      ?? activeDialog;
    focusTarget.focus();

    function handleKeyDown(event: KeyboardEvent) {
      const dialogs = [...document.querySelectorAll<HTMLElement>(".theme-editor-modal")];
      if (dialogs[dialogs.length - 1] !== activeDialog) return;
      if (event.key === "Escape" && cancelRef.current) {
        event.preventDefault();
        cancelRef.current();
        return;
      }
      if (event.key !== "Tab") return;
      const focusable = [...activeDialog.querySelectorAll<HTMLElement>(focusableSelector)]
        .filter((element) => !element.hidden && element.getAttribute("aria-hidden") !== "true");
      if (focusable.length === 0) {
        event.preventDefault();
        activeDialog.focus();
        return;
      }
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener("keydown", handleKeyDown, true);
    return () => {
      document.removeEventListener("keydown", handleKeyDown, true);
      document.body.style.overflow = previousOverflow;
      if (previousDialog) {
        previousDialog.removeAttribute("aria-hidden");
        previousDialog.inert = false;
      }
      previousFocus?.focus();
    };
  }, []);

  return <div className="theme-editor-modal-backdrop" role="presentation">
    <section
      ref={dialogRef}
      className={`theme-editor-modal ${className}`.trim()}
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
      aria-describedby={descriptionId}
      tabIndex={-1}
    >
      {children}
    </section>
  </div>;
}
