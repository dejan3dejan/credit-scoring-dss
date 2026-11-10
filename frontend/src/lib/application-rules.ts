import type { ApplicationResponse, MeResponse } from "@/api/types";
import { can } from "@/auth/session";

export function isOpen(application: ApplicationResponse): boolean {
  return application.stage === "Submitted" || application.stage === "AwaitingSignOff";
}

export function canWithdraw(application: ApplicationResponse, me: MeResponse): boolean {
  return (
    can(me, "applications.write") &&
    isOpen(application) &&
    (application.assignedTo?.id === me.id || can(me, "applications.assign"))
  );
}
