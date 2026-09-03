import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { createSession, logout as logoutRequest, me, type AuthUserDto } from "../api/authApi";
import { ApiError, resetAuthenticationErrorGuard, setAuthenticationErrorHandler } from "../api/httpClient";

interface AuthContextValue {
  currentUser: AuthUserDto | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (account: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  updateCurrentUser: (user: AuthUserDto) => void;
}

type SessionMessage = { type: "session-changed" } | { type: "session-ended" };

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const [currentUser, setCurrentUser] = useState<AuthUserDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const redirectGuard = useRef(false);
  const sessionChannel = useRef<BroadcastChannel | null>(null);

  const handleAuthenticationError = useCallback((error: ApiError) => {
    if (redirectGuard.current) return;

    redirectGuard.current = true;
    setCurrentUser(null);
    const reason = error.errorCode === "AUTH_SESSION_REPLACED"
      ? "session-replaced"
      : error.errorCode === "AUTH_TOKEN_EXPIRED"
        ? "expired"
        : "session-invalid";
    navigate(`/login?reason=${reason}`, { replace: true });
  }, [navigate]);

  const refreshCurrentUser = useCallback(async () => {
    try {
      const user = await me();
      redirectGuard.current = false;
      resetAuthenticationErrorGuard();
      setCurrentUser(user);
    } catch {
      setCurrentUser(null);
    }
  }, []);

  useEffect(() => {
    setAuthenticationErrorHandler(handleAuthenticationError);
    return () => setAuthenticationErrorHandler(null);
  }, [handleAuthenticationError]);

  useEffect(() => {
    if (typeof BroadcastChannel === "undefined") return;

    const channel = new BroadcastChannel("onlinejudge-session");
    sessionChannel.current = channel;
    channel.onmessage = (event: MessageEvent<SessionMessage>) => {
      if (event.data?.type === "session-ended") {
        setCurrentUser(null);
        if (!redirectGuard.current) {
          redirectGuard.current = true;
          navigate("/login?reason=session-invalid", { replace: true });
        }
      } else if (event.data?.type === "session-changed") {
        void refreshCurrentUser();
      }
    };

    return () => {
      sessionChannel.current = null;
      channel.close();
    };
  }, [navigate, refreshCurrentUser]);

  useEffect(() => {
    let ignore = false;

    async function loadCurrentUser() {
      try {
        const user = await me();
        if (!ignore) setCurrentUser(user);
      } catch {
        if (!ignore) setCurrentUser(null);
      } finally {
        if (!ignore) setIsLoading(false);
      }
    }

    void loadCurrentUser();
    return () => {
      ignore = true;
    };
  }, []);

  const login = useCallback(async (account: string, password: string) => {
    await createSession(account, password);
    const user = await me();
    redirectGuard.current = false;
    resetAuthenticationErrorGuard();
    setCurrentUser(user);
    sessionChannel.current?.postMessage({ type: "session-changed" } satisfies SessionMessage);
  }, []);

  const logout = useCallback(async () => {
    try {
      await logoutRequest();
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401) {
        throw error;
      }
    }

    setCurrentUser(null);
    sessionChannel.current?.postMessage({ type: "session-ended" } satisfies SessionMessage);
  }, []);

  const updateCurrentUser = useCallback((user: AuthUserDto) => {
    setCurrentUser(user);
  }, []);

  const value = useMemo<AuthContextValue>(() => ({
    currentUser,
    isAuthenticated: Boolean(currentUser),
    isLoading,
    login,
    logout,
    updateCurrentUser
  }), [currentUser, isLoading, login, logout, updateCurrentUser]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider.");
  }

  return context;
}

export function canManageContent(role?: number) {
  return role === 2 || role === 3;
}

export function isRoot(role?: number) {
  return role === 3;
}
