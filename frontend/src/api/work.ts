import { request } from "@/api/client";
import type { DraftResponse } from "@/api/drafts";
import type { ApplicationSummary, Role } from "@/api/types";

export type MyWork = {
  counts: {
    drafts: number;
    awaitingDecision: number;
    scoringUnavailable: number;
    awaitingSignOff: number;
    decidedLast7Days: number;
    submittedLast7Days: number;
    approvedLast7Days: number;
    overridesLast7Days: number;
  };
  drafts: DraftResponse[];
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
