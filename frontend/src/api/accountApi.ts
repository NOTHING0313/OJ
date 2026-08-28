import type { AuthUserDto } from "./authApi";
import { request } from "./httpClient";
import { normalizeUploadedImagePath } from "../utils/uploadedImageUrl";

export interface AccountUserDto extends AuthUserDto {
  phoneNumberMasked: string | null;
  phoneNumberConfirmed: boolean;
}

export interface SmsSendResultDto {
  message: string;
  debugCode: string | null;
}

export interface EmailSendResultDto {
  message: string;
  debugCode: string | null;
}

export type UserAppearance = {
  backgroundImageUrl: string | null;
  backgroundEnabled: boolean;
  positionX: number;
  positionY: number;
  scale: number;
  overlayOpacity: number;
};

export type UpdateUserAppearanceRequest = {
  backgroundImageUrl?: string | null;
  backgroundEnabled: boolean;
  positionX: number;
  positionY: number;
  scale: number;
  overlayOpacity: number;
};

export function getAccountMe() {
  return request<AccountUserDto>("/api/account/me");
}

export function updateAvatar(avatarUrl: string) {
  return request<AccountUserDto>("/api/account/avatar", {
    method: "PUT",
    body: JSON.stringify({ avatarUrl })
  });
}

export function updateLeaderboardAnonymity(isLeaderboardAnonymous: boolean) {
  return request<AccountUserDto>("/api/account/leaderboard-anonymity", {
    method: "PUT",
    body: JSON.stringify({ isLeaderboardAnonymous })
  });
}

export function getMyAppearance() {
  return request<UserAppearance>("/api/account/appearance").then(normalizeUserAppearance);
}

export function updateMyAppearance(payload: UpdateUserAppearanceRequest) {
  return request<UserAppearance>("/api/account/appearance", {
    method: "PUT",
    body: JSON.stringify({
      ...payload,
      backgroundImageUrl: normalizeUploadedImagePath(payload.backgroundImageUrl)
    })
  }).then(normalizeUserAppearance);
}

export function sendPhoneCode(phoneNumber: string) {
  return request<SmsSendResultDto>("/api/account/phone/send-code", {
    method: "POST",
    body: JSON.stringify({ phoneNumber })
  });
}

export function verifyPhone(phoneNumber: string, code: string) {
  return request<AccountUserDto>("/api/account/phone/verify", {
    method: "POST",
    body: JSON.stringify({ phoneNumber, code })
  });
}

export function sendPasswordResetCode(phoneNumber: string) {
  return request<SmsSendResultDto>("/api/auth/password-reset/send-code", {
    method: "POST",
    body: JSON.stringify({ phoneNumber })
  });
}

export function confirmPasswordReset(phoneNumber: string, code: string, newPassword: string) {
  return request<void>("/api/auth/password-reset/confirm", {
    method: "POST",
    body: JSON.stringify({ phoneNumber, code, newPassword })
  });
}

export function sendEmailPasswordResetCode(email: string) {
  return request<EmailSendResultDto>("/api/auth/email-password-reset/send-code", {
    method: "POST",
    body: JSON.stringify({ email })
  });
}

export function confirmEmailPasswordReset(email: string, code: string, newPassword: string) {
  return request<void>("/api/auth/email-password-reset/confirm", {
    method: "POST",
    body: JSON.stringify({ email, code, newPassword })
  });
}

export function sendAccountDeleteCode() {
  return request<EmailSendResultDto>("/api/account/delete/send-code", {
    method: "POST"
  });
}

export function confirmAccountDelete(code: string, password: string) {
  return request<void>("/api/account/delete/confirm", {
    method: "POST",
    body: JSON.stringify({ code, password })
  });
}

function normalizeUserAppearance(value: UserAppearance | any): UserAppearance {
  return {
    backgroundImageUrl: normalizeUploadedImagePath(value?.backgroundImageUrl),
    backgroundEnabled: Boolean(value?.backgroundEnabled),
    positionX: readNumber(value?.positionX, 50),
    positionY: readNumber(value?.positionY, 50),
    scale: readNumber(value?.scale, 1),
    overlayOpacity: readNumber(value?.overlayOpacity, 0.65)
  };
}

function readNumber(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}
