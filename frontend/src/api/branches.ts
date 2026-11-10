import { request } from "@/api/client";
import type { BranchRef } from "@/api/types";

export type Branch = BranchRef & { city: string | null; isActive: boolean; createdAt: string; version: number };

export const branchesKey = ["branches"] as const;

export const listBranches = () => request<Branch[]>("/api/branches");

export const createBranch = (branch: { code: string; name: string; city: string | null }) =>
  request<Branch>("/api/branches", { method: "POST", body: branch });

export const updateBranch = (id: string, branch: { name: string; city: string | null }, version: number) =>
  request<Branch>(`/api/branches/${id}`, { method: "PUT", body: { ...branch, version } });

export const deactivateBranch = (id: string) => request<Branch>(`/api/branches/${id}/deactivate`, { method: "POST" });

export const reactivateBranch = (id: string) => request<Branch>(`/api/branches/${id}/reactivate`, { method: "POST" });
