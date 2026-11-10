import type { ApplicationFilters } from "@/api/applications";
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
