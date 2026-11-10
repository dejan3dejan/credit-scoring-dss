import { request } from "@/api/client";
import type { BranchRef, Language, Role } from "@/api/types";

export type UserResponse = {
  id: string;
  userName: string;
  displayName: string;
  email: string | null;
  role: Role;
  branch: BranchRef | null;
  isActive: boolean;
  preferredLanguage: Language;
  mustChangePassword: boolean;
  createdAt: string;
  lastSignedInAt: string | null;
  lockedUntil: string | null;
  version: number;
};

export type UserFields = {
  displayName: string;
  email: string | null;
  role: Role | null;
  branchId: string | null;
  preferredLanguage: Language;
};

export type NewUser = UserFields & { userName: string; temporaryPassword: string };

export const usersKey = ["users"] as const;
export const userKey = (id: string) => ["users", id] as const;

export const listUsers = () => request<UserResponse[]>("/api/users");

export const getUser = (id: string) => request<UserResponse>(`/api/users/${id}`);

export const createUser = (user: NewUser) => request<UserResponse>("/api/users", { method: "POST", body: user });

export const updateUser = (id: string, user: UserFields, version: number) =>
  request<UserResponse>(`/api/users/${id}`, { method: "PUT", body: { ...user, version } });

export const deactivateUser = (id: string) => request<UserResponse>(`/api/users/${id}/deactivate`, { method: "POST" });

export const reactivateUser = (id: string) => request<UserResponse>(`/api/users/${id}/reactivate`, { method: "POST" });

export const resetPassword = (id: string, temporaryPassword: string) =>
  request<void>(`/api/users/${id}/password`, { method: "POST", body: { temporaryPassword } });
