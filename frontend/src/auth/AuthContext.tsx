import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { login as loginRequest, logout as logoutRequest, me, type AuthUserDto } from "../api/authApi";
import { ApiError, resetAuthenticationErrorGuard, setAuthenticationErrorHandler } from "../api/httpClient";

interface AuthContextValue {
  currentUser: AuthUserDto | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (account: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  updateCurrentUser: (user: AuthUserDto) => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const [currentUser, setCurrentUser] = useState<AuthUserDto | null>(() => readStoredUser());
  const [isLoading, setIsLoading] = useState(Boolean(localStorage.getItem("accessToken")));
  const redirectGuard = useRef(false);

  const handleAuthenticationError = useCallback((error: ApiError) => {
    if (redirectGuard.current) return;

    redirectGuard.current = true;
    clearAuthStorage();
    setCurrentUser(null);
    const reason = error.errorCode === "AUTH_SESSION_REPLACED"
      ? "session-replaced"
      : error.errorCode === "AUTH_TOKEN_EXPIRED"
        ? "expired"
        : "session-invalid";
    navigate(`/login?reason=${reason}`, { replace: true });
  }, [navigate]);

  useEffect(() => {
    setAuthenticationErrorHandler(handleAuthenticationError);
    return () => setAuthenticationErrorHandler(null);
  }, [handleAuthenticationError]);

  useEffect(() => {
    function handleStorage(event: StorageEvent) {
      if (event.key === "accessToken" && event.newValue === null && !redirectGuard.current) {
        handleAuthenticationError(new ApiError("登录状态已失效，请重新登录。", 401, "AUTH_SESSION_INVALID"));
      }
    }

    window.addEventListener("storage", handleStorage);
    return () => window.removeEventListener("storage", handleStorage);
  }, [handleAuthenticationError]);

  useEffect(() => {
    let ignore = false;

    async function loadCurrentUser() {
      if (!localStorage.getItem("accessToken")) {
        clearAuthStorage();
        setCurrentUser(null);
        setIsLoading(false);
        return;
      }

      try {
        const user = await me();
        if (!ignore) {
          setCurrentUser(user);
          localStorage.setItem("currentUser", JSON.stringify(user));
        }
      } catch {
        if (!ignore) {
          clearAuthStorage();
          setCurrentUser(null);
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadCurrentUser();

    return () => {
      ignore = true;
    };
  }, []);

  async function login(account: string, password: string) {
    const response = await loginRequest(account, password);
    redirectGuard.current = false;
    resetAuthenticationErrorGuard();
    localStorage.setItem("accessToken", response.accessToken);
    localStorage.setItem("currentUser", JSON.stringify(response.user));
    setCurrentUser(response.user);
  }

  async function logout() {
    try {
      if (localStorage.getItem("accessToken")) {
        await logoutRequest();
      }
    } catch {
      // Local logout must still complete when the server is unavailable or the token is already invalid.
    } finally {
      clearAuthStorage();
      setCurrentUser(null);
    }
  }

  function updateCurrentUser(user: AuthUserDto) {
    localStorage.setItem("currentUser", JSON.stringify(user));
    setCurrentUser(user);
  }

  const value = useMemo<AuthContextValue>(() => ({
    currentUser,
    isAuthenticated: Boolean(currentUser && localStorage.getItem("accessToken")),
    isLoading,
    login,
    logout,
    updateCurrentUser
  }), [currentUser, isLoading]);

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

function readStoredUser(): AuthUserDto | null {
  const raw = localStorage.getItem("currentUser");
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as AuthUserDto;
  } catch {
    return null;
  }
}

function clearAuthStorage() {
  localStorage.removeItem("accessToken");
  localStorage.removeItem("currentUser");
}
