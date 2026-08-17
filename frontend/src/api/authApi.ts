import { request } from "./httpClient";

export interface AuthUserDto {
  id: string;
  userName: string;
  email: string;
  avatarUrl: string | null;
  role: number;
  isBlacklisted: boolean;
}

export interface LoginResponse {
  accessToken: string;
  user: AuthUserDto;
}

export interface RegisterRequest {
  userName: string;
  email: string;
  password: string;
  emailCode: string;
  avatarUrl?: string;
}

export interface EmailCodeResult {
  message: string;
  debugCode: string | null;
}

export function register(payload: RegisterRequest) {
  return request<AuthUserDto>("/api/auth/register", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function sendRegisterEmailCode(email: string) {
  return request<EmailCodeResult>("/api/auth/register/send-code", {
    method: "POST",
    body: JSON.stringify({ email })
  });
}

export function login(account: string, password: string) {
  return request<LoginResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ account, password })
  });
}

export function me() {
  return request<AuthUserDto>("/api/auth/me");
}
