import { request } from "@/api/client";
import type { AvailableModel, ModelEvent, ScoringModel } from "@/api/types";

export const modelsKey = ["models"] as const;

export const listModels = () => request<ScoringModel[]>("/api/models");

export const listAvailableModels = () => request<AvailableModel[]>("/api/models/available");

export const listModelEvents = () => request<ModelEvent[]>("/api/models/events");

export const registerModel = (modelId: string) => request<ScoringModel>("/api/models", { method: "POST", body: { modelId } });

export const changeModelRole = (modelId: string, change: "promote" | "retire", reason: string | null) =>
  request<ScoringModel[]>(`/api/models/${encodeURIComponent(modelId)}/${change}`, { method: "POST", body: { reason } });
