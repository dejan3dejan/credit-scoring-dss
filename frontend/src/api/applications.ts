import { request } from "@/api/client";
import type {
  ApplicationAnswers,
  ApplicationResponse,
  ApplicationStage,
  ApplicationSummary,
  FinalDecision,
  LoanOutcomeFilter,
  LoanOutcomeKind,
  Note,
  Paged,
  Recommendation,
  ReviewFilter,
  ShadowScore,
} from "@/api/types";

export type ApplicationFilters = {
  applicantName?: string;
  review?: ReviewFilter;
  recommendation?: Recommendation;
  stage?: ApplicationStage;
  loanOutcome?: LoanOutcomeFilter;
  from?: string;
  to?: string;
  page?: number;
};

function startOfDay(date: string): string {
  const [year, month, day] = date.split("-").map(Number);
  return new Date(year, month - 1, day).toISOString();
}

function startOfNextDay(date: string): string {
  const [year, month, day] = date.split("-").map(Number);
  return new Date(year, month - 1, day + 1).toISOString();
}

export function applicationsQuery(filters: ApplicationFilters): string {
  const params = new URLSearchParams();
  if (filters.applicantName) params.set("applicantName", filters.applicantName);
  if (filters.review) params.set("review", filters.review);
  if (filters.recommendation) params.set("recommendation", filters.recommendation);
  if (filters.stage) params.set("stage", filters.stage);
  if (filters.loanOutcome) params.set("loanOutcome", filters.loanOutcome);
  if (filters.from) params.set("from", startOfDay(filters.from));
  if (filters.to) params.set("to", startOfNextDay(filters.to));
  if (filters.page && filters.page > 1) params.set("page", String(filters.page));
  const query = params.toString();
  return query ? `?${query}` : "";
}

export const listApplications = (filters: ApplicationFilters) =>
  request<Paged<ApplicationSummary>>(`/api/applications${applicationsQuery(filters)}`);

export const applicationKey = (id: string) => ["application", id] as const;

export const getApplication = (id: string) => request<ApplicationResponse>(`/api/applications/${id}`);

export const getShadowScores = (id: string) => request<ShadowScore[]>(`/api/applications/${id}/shadow-scores`);

export const rescore = (id: string) => request<ApplicationResponse>(`/api/applications/${id}/rescore`, { method: "POST" });

export const recordDecision = (id: string, outcome: FinalDecision | null, reason: string | null) =>
  request<ApplicationResponse>(`/api/applications/${id}/decision`, { method: "POST", body: { outcome, reason } });

export type NewApplication = { clientId: string } & { [K in keyof ApplicationAnswers]: ApplicationAnswers[K] | null };

export const createApplication = (application: NewApplication & { draftId?: string }) =>
  request<ApplicationResponse>("/api/applications", { method: "POST", body: application });

export const notesKey = (id: string) => ["notes", id] as const;

export const listNotes = (id: string) => request<Note[]>(`/api/applications/${id}/notes`);

export const addNote = (id: string, text: string) => request<Note>(`/api/applications/${id}/notes`, { method: "POST", body: { text } });

export const withdraw = (id: string, reason: string) =>
  request<ApplicationResponse>(`/api/applications/${id}/withdraw`, { method: "POST", body: { reason } });

export const assign = (id: string, userId: string) =>
  request<ApplicationResponse>(`/api/applications/${id}/assign`, { method: "POST", body: { userId } });

export const recordOutcome = (id: string, outcome: LoanOutcomeKind | null, note: string | null) =>
  request<ApplicationResponse>(`/api/applications/${id}/outcome`, { method: "POST", body: { outcome, note } });

export const signOff = (id: string, approve: boolean, note: string | null) =>
  request<ApplicationResponse>(`/api/applications/${id}/sign-off`, { method: "POST", body: { approve, note } });
