import { request } from "@/api/client";
import type { DecisionPath } from "@/api/types";

type MatrixRow = { approved: number; rejected: number; pending: number };
type Rate = number | null;

export type PeriodStats = {
  from: string;
  to: string;
  applications: number;
  decided: number;
  approvalRate: Rate;
  overrideRate: Rate;
  reviewShare: Rate;
  unavailableRate: Rate;
};

export type DashboardStats = {
  applications: number;
  scored: number;
  unavailable: number;
  decided: number;
  pending: number;
  matrix: { approve: MatrixRow; review: MatrixRow; reject: MatrixRow; unavailable: MatrixRow };
  overrideRate: Rate;
  highSideOverrideRate: Rate;
  lowSideOverrideRate: Rate;
  unavailableRate: Rate;
  pBadHistogram: { from: number; to: number; count: number }[];
  officers: { name: string; decisions: number; overrides: number; overrideRate: Rate }[];
  shadowAgreement: { modelId: string; compared: number; agreed: number; agreementRate: Rate }[];
  weekly: {
    weekStart: string;
    applications: number;
    approve: number;
    review: number;
    reject: number;
    unavailable: number;
    decided: number;
    overrides: number;
    overrideRate: Rate;
    reviewShare: Rate;
  }[];
  byBranch: { branchId: string; code: string; name: string; applications: number; decided: number; overrides: number; overrideRate: Rate; reviewShare: Rate }[];
  comparison: { current: PeriodStats; previous: PeriodStats } | null;
  outcomeByDecision: { path: DecisionPath; approved: number; withOutcome: number; defaulted: number; defaultRate: Rate }[];
  calibration: { from: number; to: number; loans: number; meanPredicted: Rate; observedDefaultRate: Rate }[];
};

export type StatsFilters = { from?: string; to?: string; branchId?: string };

export function statsQuery(filters: StatsFilters): string {
  const params = new URLSearchParams();
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  if (filters.branchId) params.set("branchId", filters.branchId);
  const query = params.toString();
  return query ? `?${query}` : "";
}

export const getStats = (filters: StatsFilters) => request<DashboardStats>(`/api/stats${statsQuery(filters)}`);
