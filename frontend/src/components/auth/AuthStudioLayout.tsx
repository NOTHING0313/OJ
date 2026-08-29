import { type ReactNode } from "react";
import { AuthParticleField } from "./AuthParticleField";

interface AuthStudioLayoutProps {
  title: string;
  children: ReactNode;
}

export function AuthStudioLayout({ title, children }: AuthStudioLayoutProps) {
  return (
    <main className="auth-studio-page">
      <div className="auth-studio-glow" aria-hidden="true" />
      <AuthParticleField />
      <div className="auth-studio-shell">
        <div className="auth-studio-brand">
          <img src="/brand/unrealstudio-logo.png" alt="UNREALSTUDIO" />
          <strong>UNREAL STUDIO</strong>
        </div>
        <div className="auth-studio-atmosphere" aria-hidden="true" />

        <section className="auth-studio-auth" aria-labelledby="auth-studio-title">
          <div className="auth-studio-card">
            <header className="auth-studio-card-header">
              <h1 id="auth-studio-title">{title}</h1>
            </header>
            {children}
          </div>
          <footer className="auth-studio-footer">© 2026 UNREAL STUDIO</footer>
        </section>
      </div>
    </main>
  );
}
