import { request } from "@/api/client";
import type { Language, MeResponse } from "@/api/types";

export const getMe = () => request<MeResponse>("/api/auth/me");

export const login = (userName: string, password: string) =>
  request<MeResponse>("/api/auth/login", { method: "POST", body: { userName, password } });

export const logout = () => request<void>("/api/auth/logout", { method: "POST" });

export const setLanguage = (language: Language) =>
  request<MeResponse>("/api/auth/me/language", { method: "PUT", body: { language } });

export const changePassword = (currentPassword: string, newPassword: string) =>
  request<void>("/api/auth/password", { method: "POST", body: { currentPassword, newPassword } });
