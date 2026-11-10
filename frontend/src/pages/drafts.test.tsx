import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { signedInAs, stubApi } from "@/test/api";
import { application, clientDetail } from "@/test/fixtures";
import type { DraftResponse } from "@/api/drafts";

const blank = {
  loanAmount: null,
  purpose: null,
  employmentLength: null,
  homeOwnership: null,
  annualIncome: null,
  incomeVerification: null,
  debtToIncomePercent: null,
  creditScore: null,
  earliestCreditLine: null,
  inquiriesLast6Months: null,
  delinquenciesLast2Years: null,
  publicRecords: null,
  bankruptcies: null,
  revolvingBalance: null,
  revolvingUtilizationPercent: null,
  openAccounts: null,
  totalAccounts: null,
  mortgageAccounts: null,
};

function draft(overrides: Partial<DraftResponse> = {}): DraftResponse {
  return {
    id: "d1",
    clientId: "c1",
    clientNumber: "C-000007",
    clientName: "Milica Testić",
    answers: { ...blank, clientId: "c1", loanAmount: 20000, purpose: "SmallBusiness", earliestCreditLine: "2012-05-01" },
    createdAt: "2026-11-09T09:00:00Z",
    updatedAt: "2026-11-09T09:10:00Z",
    status: "Open",
    applicationId: null,
    ...overrides,
  };
}

const client = { "GET /api/clients/c1": { status: 200, body: clientDetail() } };

describe("drafts", () => {
  it("saves an unfinished form as a draft", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...client,
      "POST /api/drafts": { status: 201, body: draft({ answers: { ...blank, clientId: "c1", loanAmount: 20000 } }) },
      "GET /api/drafts/d1": { status: 200, body: draft({ answers: { ...blank, clientId: "c1", loanAmount: 20000 } }) },
    });
    renderApp("/applications/new?clientId=c1");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Loan amount"), "20000");
    await user.click(screen.getByRole("button", { name: "Save draft" }));
    expect(await screen.findByText("Draft saved.")).toBeInTheDocument();
    expect(calls.find((c) => c.path === "/api/drafts")?.body).toEqual({ ...blank, clientId: "c1", loanAmount: 20000 });
    expect(await screen.findByText(/Draft, last saved/)).toBeInTheDocument();
  });

  it("continues a draft and submits it with its id", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...client,
      "GET /api/drafts/d1": { status: 200, body: draft() },
      "POST /api/applications": { status: 201, body: application({ id: "a9" }) },
      "GET /api/applications/a9": { status: 200, body: application({ id: "a9" }) },
      "GET /api/applications/a9/notes": { status: 200, body: [] },
    });
    renderApp("/applications/new?draftId=d1");
    expect(await screen.findByLabelText("Loan amount")).toHaveValue("20000");
    expect(screen.getByLabelText("Purpose")).toHaveValue("SmallBusiness");
    expect(screen.getByLabelText("First credit line opened")).toHaveValue("2012-05");
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Get recommendation" }));
    await screen.findByRole("heading", { name: "Milica Testić" });
    const body = calls.find((c) => c.path === "/api/applications" && c.method === "POST")?.body as Record<string, unknown>;
    expect(body.draftId).toBe("d1");
    expect(body.clientId).toBe("c1");
  });

  it("saves changes to an existing draft in place", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...client,
      "GET /api/drafts/d1": { status: 200, body: draft() },
      "PUT /api/drafts/d1": { status: 200, body: draft() },
    });
    renderApp("/applications/new?draftId=d1");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Credit score"), "667");
    await user.click(screen.getByRole("button", { name: "Save draft" }));
    await screen.findByText("Draft saved.");
    expect((calls.find((c) => c.method === "PUT")?.body as Record<string, unknown>).creditScore).toBe(667);
  });

  it("discards a draft after a confirmation", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...client,
      "GET /api/drafts/d1": { status: 200, body: draft() },
      "POST /api/drafts/d1/discard": { status: 200, body: draft({ status: "Discarded" }) },
      "GET /api/work/mine": {
        status: 200,
        body: {
          counts: { drafts: 0, awaitingDecision: 0, scoringUnavailable: 0, awaitingSignOff: 0, decidedLast7Days: 0 },
          drafts: [],
          awaitingDecision: [],
          scoringUnavailable: [],
          awaitingSignOff: [],
          recentlyDecided: [],
        },
      },
    });
    renderApp("/applications/new?draftId=d1");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Discard draft" }));
    await user.click(screen.getByRole("button", { name: "Discard" }));
    expect(await screen.findByRole("heading", { name: "My work" })).toBeInTheDocument();
    expect(calls.some((c) => c.path === "/api/drafts/d1/discard")).toBe(true);
  });

  it("says when a draft was already submitted and links to the application", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/drafts/d1": { status: 200, body: draft({ status: "Submitted", applicationId: "a9" }) } });
    renderApp("/applications/new?draftId=d1");
    expect(await screen.findByText("This draft was already submitted.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Open the application" })).toHaveAttribute("href", "/app/applications/a9");
  });

  it("lists open drafts on the home page", async () => {
    stubApi({
      ...signedInAs("Officer"),
      "GET /api/work/mine": {
        status: 200,
        body: {
          counts: { drafts: 1, awaitingDecision: 0, scoringUnavailable: 0, awaitingSignOff: 0, decidedLast7Days: 0 },
          drafts: [draft()],
          awaitingDecision: [],
          scoringUnavailable: [],
          awaitingSignOff: [],
          recentlyDecided: [],
        },
      },
    });
    renderApp("/");
    expect(await screen.findByRole("link", { name: "Continue Milica Testić" })).toHaveAttribute("href", "/app/applications/new?draftId=d1");
  });
});
