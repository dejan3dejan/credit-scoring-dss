import type { ApplicationResponse, ApplicationSummary, ClientDetail, ClientResponse, ClientSummary, Paged } from "@/api/types";

export function summary(overrides: Partial<ApplicationSummary> = {}): ApplicationSummary {
  return {
    id: "a1",
    reference: "A-000001",
    clientId: "c1",
    branchId: "b1",
    applicantName: "Milica Testić",
    submittedBy: "Mila Testić",
    submittedAt: "2026-11-02T09:30:00Z",
    loanAmount: 20000,
    purpose: "SmallBusiness",
    status: "Scored",
    recommendation: "Review",
    pBad: 0.237,
    finalDecision: null,
    isOverride: false,
    stage: "Submitted",
    assignedTo: { id: "00000000-0000-0000-0000-000000000001", displayName: "Mila Testić" },
    loanOutcome: null,
    ...overrides,
  };
}

export function paged<T>(items: T[], totalCount = items.length, page = 1, pageSize = 20): Paged<T> {
  return { items, page, pageSize, totalCount };
}

export function application(overrides: Partial<ApplicationResponse> = {}): ApplicationResponse {
  return {
    id: "a1",
    reference: "A-000001",
    stage: "Submitted",
    assignedTo: { id: "00000000-0000-0000-0000-000000000001", displayName: "Mila Testić" },
    withdrawal: null,
    signOffs: [],
    loanOutcome: null,
    clientId: "c1",
    branchId: "b1",
    clientNumber: "C-000007",
    applicantName: "Milica Testić",
    submittedBy: "Mila Testić",
    submittedAt: "2026-11-02T09:30:00Z",
    termMonths: 36,
    loanAmount: 20000,
    purpose: "SmallBusiness",
    employmentLength: "ThreeYears",
    homeOwnership: "Rent",
    annualIncome: 54000,
    incomeVerification: "SourceVerified",
    debtToIncomePercent: 18.4,
    creditScore: 667,
    earliestCreditLine: "2012-05-01",
    creditHistoryYears: 14.5,
    inquiriesLast6Months: 3,
    delinquenciesLast2Years: 1,
    publicRecords: 0,
    bankruptcies: 0,
    revolvingBalance: 8200,
    revolvingUtilizationPercent: 61.5,
    openAccounts: 9,
    totalAccounts: 21,
    mortgageAccounts: 0,
    decision: {
      status: "Scored",
      evaluatedAt: "2026-11-02T09:30:02Z",
      pBad: 0.237,
      modelId: "credit_lc_xgb_isotonic_v1",
      thresholdUsed: 0.3286,
      reviewFromUsed: 0.2,
      recommendation: "Review",
      errorCode: null,
      factors: [
        { input: "Purpose", contribution: 0.31 },
        { input: "CreditScore", contribution: 0.22 },
        { input: "InquiriesLast6Months", contribution: 0.12 },
        { input: "DelinquenciesLast2Years", contribution: -0.05 },
      ],
    },
    officerDecision: null,
    scoringFailures: [],
    ...overrides,
  };
}

export function unscored(overrides: Partial<ApplicationResponse> = {}): ApplicationResponse {
  return application({
    decision: {
      status: "ScoringUnavailable",
      evaluatedAt: "2026-11-02T09:30:05Z",
      pBad: null,
      modelId: null,
      thresholdUsed: null,
      reviewFromUsed: null,
      recommendation: null,
      errorCode: "unreachable",
      factors: [],
    },
    ...overrides,
  });
}

export function clientSummary(overrides: Partial<ClientSummary> = {}): ClientSummary {
  return {
    id: "c1",
    clientNumber: "C-000007",
    fullName: "Milica Testić",
    dateOfBirth: "1988-04-12",
    applicationCount: 1,
    lastApplicationAt: "2026-11-02T09:30:00Z",
    branchId: "b1",
    isActive: true,
    anonymisedAt: null,
    ...overrides,
  };
}

export function clientResponse(overrides: Partial<ClientResponse> = {}): ClientResponse {
  return {
    id: "c1",
    clientNumber: "C-000007",
    fullName: "Milica Testić",
    dateOfBirth: "1988-04-12",
    address: "Bulevar oslobođenja 1, Novi Sad",
    phone: "+381 21 000 000",
    email: null,
    employer: "Test d.o.o.",
    branchId: "b1",
    isActive: true,
    anonymisedAt: null,
    createdAt: "2026-10-01T08:00:00Z",
    ...overrides,
  };
}

export function clientDetail(overrides: Partial<ClientDetail> = {}): ClientDetail {
  return {
    id: "c1",
    clientNumber: "C-000007",
    fullName: "Milica Testić",
    dateOfBirth: "1988-04-12",
    address: "Bulevar oslobođenja 1, Novi Sad",
    phone: "+381 21 000 000",
    email: null,
    employer: "Test d.o.o.",
    branchId: "b1",
    isActive: true,
    anonymisedAt: null,
    createdAt: "2026-10-01T08:00:00Z",
    applications: [summary()],
    ...overrides,
  };
}
