import type { NewApplication } from "@/api/applications";
import { request } from "@/api/client";
import type { FieldError } from "@/api/types";

export type DraftResponse = {
  id: string;
  clientId: string;
  clientNumber: string;
  clientName: string;
  answers: NewApplication;
  createdAt: string;
  updatedAt: string;
  status: "Open" | "Submitted" | "Discarded";
  applicationId: string | null;
  fieldErrors: Record<string, FieldError[]>;
};

export const draftKey = (id: string) => ["draft", id] as const;

export const getDraft = (id: string) => request<DraftResponse>(`/api/drafts/${id}`);

export const createDraft = (answers: NewApplication) => request<DraftResponse>("/api/drafts", { method: "POST", body: answers });

export const updateDraft = (id: string, answers: NewApplication) =>
  request<DraftResponse>(`/api/drafts/${id}`, { method: "PUT", body: answers });

export const discardDraft = (id: string) => request<DraftResponse>(`/api/drafts/${id}/discard`, { method: "POST" });
