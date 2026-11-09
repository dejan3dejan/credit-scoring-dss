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
export type DecisionStatus = "Scored" | "ScoringUnavailable";
export type FinalDecision = "Approve" | "Reject";
export type ApplicationStage = "Submitted" | "AwaitingSignOff" | "Decided" | "Withdrawn";
export type LoanOutcomeKind = "Repaid" | "Defaulted";
export type ReviewFilter = "Pending" | "Approved" | "Rejected" | "Override";
export type SignOffReason = "OverridesReject" | "LargeAmount";
export type SignOffStatus = "Pending" | "Approved" | "Returned";
export type ModelRole = "Active" | "Shadow" | "Retired";
export type DecisionPath = "AsRecommended" | "AfterReview" | "Override" | "WithoutScore";

export const loanPurposes = [
  "DebtConsolidation",
  "CreditCard",
  "HomeImprovement",
  "MajorPurchase",
  "Car",
  "Medical",
  "Moving",
  "House",
  "Vacation",
  "Wedding",
  "SmallBusiness",
  "RenewableEnergy",
  "Other",
] as const;
export type LoanPurpose = (typeof loanPurposes)[number];

export const employmentLengths = [
  "NotStated",
  "LessThanOneYear",
  "OneYear",
  "TwoYears",
  "ThreeYears",
  "FourYears",
  "FiveYears",
  "SixYears",
  "SevenYears",
  "EightYears",
  "NineYears",
  "TenOrMoreYears",
] as const;
export type EmploymentLength = (typeof employmentLengths)[number];

export const homeOwnerships = ["Rent", "Own", "Mortgage", "Other"] as const;
export type HomeOwnership = (typeof homeOwnerships)[number];

export const incomeVerifications = ["NotVerified", "SourceVerified", "Verified"] as const;
export type IncomeVerification = (typeof incomeVerifications)[number];

export type UserRef = { id: string; displayName: string };

export type Paged<T> = { items: T[]; page: number; pageSize: number; totalCount: number };

export type ApplicationSummary = {
  id: string;
  reference: string;
  clientId: string;
  branchId: string;
  applicantName: string;
  submittedBy: string;
  submittedAt: string;
  loanAmount: number;
  purpose: LoanPurpose;
  status: DecisionStatus | null;
  recommendation: Recommendation | null;
  pBad: number | null;
  finalDecision: FinalDecision | null;
  isOverride: boolean;
  stage: ApplicationStage;
  assignedTo: UserRef | null;
  loanOutcome: LoanOutcomeKind | null;
};

export type ScoreFactor = { input: string; contribution: number };

export type DecisionResponse = {
  status: DecisionStatus;
  evaluatedAt: string;
  pBad: number | null;
  modelId: string | null;
  thresholdUsed: number | null;
  reviewFromUsed: number | null;
  recommendation: Recommendation | null;
  errorCode: string | null;
  factors: ScoreFactor[];
};

export type OfficerDecisionResponse = {
  outcome: FinalDecision;
  reason: string | null;
  decidedBy: string;
  decidedAt: string;
  isOverride: boolean;
  signedOffBy: string | null;
  signedOffAt: string | null;
};

export type SignOffResponse = {
  id: string;
  outcome: FinalDecision;
  reason: string | null;
  why: SignOffReason;
  requestedBy: string;
  requestedAt: string;
  status: SignOffStatus;
  reviewedBy: string | null;
  reviewedAt: string | null;
  note: string | null;
};

export type ApplicationAnswers = {
  loanAmount: number;
  purpose: LoanPurpose;
  employmentLength: EmploymentLength;
  homeOwnership: HomeOwnership;
  annualIncome: number;
  incomeVerification: IncomeVerification;
  debtToIncomePercent: number;
  creditScore: number;
  earliestCreditLine: string;
  inquiriesLast6Months: number;
  delinquenciesLast2Years: number;
  publicRecords: number;
  bankruptcies: number;
  revolvingBalance: number;
  revolvingUtilizationPercent: number;
  openAccounts: number;
  totalAccounts: number;
  mortgageAccounts: number;
};

export type ApplicationResponse = ApplicationAnswers & {
  id: string;
  reference: string;
  stage: ApplicationStage;
  assignedTo: UserRef | null;
  withdrawal: { at: string; by: string; reason: string } | null;
  signOffs: SignOffResponse[];
  loanOutcome: { outcome: LoanOutcomeKind; note: string | null; recordedBy: string; recordedAt: string } | null;
  clientId: string;
  branchId: string;
  clientNumber: string;
  applicantName: string;
  submittedBy: string;
  submittedAt: string;
  termMonths: number;
  creditHistoryYears: number;
  decision: DecisionResponse | null;
  officerDecision: OfficerDecisionResponse | null;
  scoringFailures: { errorCode: string; attemptedAt: string }[];
};

export type ShadowScore = {
  modelId: string;
  status: DecisionStatus;
  pBad: number | null;
  thresholdUsed: number | null;
  reviewFromUsed: number | null;
  recommendation: Recommendation | null;
  errorCode: string | null;
  scoredAt: string;
};

export type ClientSummary = {
  id: string;
  clientNumber: string;
  fullName: string;
  dateOfBirth: string | null;
  applicationCount: number;
  lastApplicationAt: string | null;
  branchId: string;
  isActive: boolean;
  anonymisedAt: string | null;
};

export type ClientContact = {
  address: string | null;
  phone: string | null;
  email: string | null;
  employer: string | null;
};

export type ClientResponse = ClientContact & {
  id: string;
  clientNumber: string;
  fullName: string;
  dateOfBirth: string | null;
  branchId: string;
  isActive: boolean;
  anonymisedAt: string | null;
  createdAt: string;
};

export type ClientDetail = ClientResponse & { applications: ApplicationSummary[] };

export type ScoringModel = {
  modelId: string;
  displayName: string;
  dataset: string;
  threshold: number;
  reviewFrom: number | null;
  role: ModelRole;
  registeredAt: string;
};

export type AvailableModel = {
  modelId: string;
  modelName: string;
  dataset: string;
  recommendedThreshold: number;
  reviewFrom: number | null;
  createdAt: string;
};

export type ModelEvent = {
  id: string;
  modelId: string;
  fromRole: ModelRole | null;
  toRole: ModelRole;
  reason: string | null;
  changedBy: string;
  changedAt: string;
};

export type FieldError = { code: string } & Record<string, unknown>;

export type Problem = {
  status: number;
  code: string;
  title?: string;
  fieldErrors?: Record<string, FieldError[]>;
  lockedUntil?: string;
};
