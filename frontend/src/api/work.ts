import { request } from "@/api/client";
import type { ApplicationSummary, Role } from "@/api/types";

export type MyWork = {
  counts: { drafts: number; awaitingDecision: number; scoringUnavailable: number; awaitingSignOff: number; decidedLast7Days: number };
  awaitingDecision: ApplicationSummary[];
  scoringUnavailable: ApplicationSummary[];
  awaitingSignOff: ApplicationSummary[];
  recentlyDecided: ApplicationSummary[];
};

export type TeamWork = {
  counts: { unassigned: number; awaitingSignOff: number; scoringUnavailable: number };
  unassigned: ApplicationSummary[];
  awaitingSignOff: ApplicationSummary[];
  officers: { id: string; displayName: string; role: Role; open: number; awaitingSignOff: number }[];
};

export const getMyWork = () => request<MyWork>("/api/work/mine");

export const getTeamWork = () => request<TeamWork>("/api/work/team");
