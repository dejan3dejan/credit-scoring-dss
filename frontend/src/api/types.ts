export type Role = "Admin" | "Officer" | "Manager" | "Risk";

export type Language = "en" | "sr";

export type Permission =
  | "users.manage"
  | "branches.manage"
  | "audit.read"
  | "clients.read"
  | "clients.write"
  | "clients.anonymise"
  | "applications.read"
  | "applications.write"
  | "applications.decide"
  | "applications.assign"
  | "applications.approve-escalated"
  | "outcomes.record"
  | "stats.read"
  | "models.read"
  | "models.manage";

export type BranchRef = { id: string; code: string; name: string };

export type MeResponse = {
  id: string;
  userName: string;
  displayName: string;
  role: Role;
  branch: BranchRef | null;
  permissions: Permission[];
  preferredLanguage: Language;
  mustChangePassword: boolean;
};

export type Recommendation = "Approve" | "Reject" | "Review";

export type FieldError = { code: string } & Record<string, unknown>;

export type Problem = {
  status: number;
  code: string;
  title?: string;
  fieldErrors?: Record<string, FieldError[]>;
  lockedUntil?: string;
};
