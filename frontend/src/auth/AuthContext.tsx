import { createContext, ReactNode, useContext, useEffect, useMemo, useState } from "react";
import { login as loginRequest, me, type AuthUserDto } from "../api/authApi";

interface AuthContextValue {
  currentUser: AuthUserDto | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (account: string, password: string) => Promise<void>;
  logout: () => void;
  updateCurrentUser: (user: AuthUserDto) => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [currentUser, setCurrentUser] = useState<AuthUserDto | null>(() => readStoredUser());
  const [isLoading, setIsLoading] = useState(Boolean(localStorage.getItem("accessToken")));

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
    localStorage.setItem("accessToken", response.accessToken);
    localStorage.setItem("currentUser", JSON.stringify(response.user));
    setCurrentUser(response.user);
  }

  function logout() {
    clearAuthStorage();
    setCurrentUser(null);
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
