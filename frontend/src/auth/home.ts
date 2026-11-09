import type { Role } from "@/api/types";

export const homeTitleKey: Record<Role, string> = {
  Officer: "nav.myWork",
  Manager: "nav.myWork",
  Risk: "nav.overview",
  Admin: "nav.administration",
};
