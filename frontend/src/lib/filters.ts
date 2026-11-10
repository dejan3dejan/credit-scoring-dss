import type { ApplicationFilters } from "@/api/applications";
import type { AuditAction, AuditFilters } from "@/api/audit";
import type { ApplicationStage, LoanOutcomeFilter, Recommendation, ReviewFilter } from "@/api/types";

export const reviewFilters: ReviewFilter[] = ["Pending", "Approved", "Rejected", "Override"];
export const recommendations: Recommendation[] = ["Approve", "Review", "Reject"];
export const stages: ApplicationStage[] = ["Submitted", "AwaitingSignOff", "Decided", "Withdrawn"];
export const loanOutcomes: LoanOutcomeFilter[] = ["Pending", "Repaid", "Defaulted"];

export function oneOf<T extends string>(value: unknown, allowed: readonly T[]): T | undefined {
  return allowed.includes(value as T) ? (value as T) : undefined;
}

export function isoDate(value: unknown): string | undefined {
  return typeof value === "string" && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : undefined;
}

export function validateApplicationFilters(search: Record<string, unknown>): ApplicationFilters {
  const page = Number(search.page);
  return {
    applicantName: typeof search.applicantName === "string" && search.applicantName ? search.applicantName : undefined,
    review: oneOf(search.review, reviewFilters),
    recommendation: oneOf(search.recommendation, recommendations),
    stage: oneOf(search.stage, stages),
    loanOutcome: oneOf(search.loanOutcome, loanOutcomes),
    from: isoDate(search.from),
    to: isoDate(search.to),
    page: Number.isInteger(page) && page > 1 ? page : undefined,
  };
}

export const auditEntities = [
  "User",
  "Branch",
  "Client",
  "CreditApplication",
  "CreditDecision",
  "ScoreFactor",
  "OfficerDecision",
  "SignOff",
  "ApplicationNote",
  "ApplicationDraft",
  "LoanOutcome",
  "ShadowScore",
  "ScoringFailure",
  "ScoringModel",
  "ModelEvent",
] as const;

export const auditActions: AuditAction[] = ["Created", "Updated", "Deleted"];

export function validateAuditFilters(search: Record<string, unknown>): AuditFilters {
  const page = Number(search.page);
  const text = (value: unknown) => (typeof value === "string" && value ? value : undefined);
  return {
    from: isoDate(search.from),
    to: isoDate(search.to),
    userId: text(search.userId),
    entity: oneOf(search.entity, auditEntities),
    action: oneOf(search.action, auditActions),
    entityId: text(search.entityId),
    page: Number.isInteger(page) && page > 1 ? page : undefined,
  };
}
