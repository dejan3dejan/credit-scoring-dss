import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { signedInAs, stubApi, type ApiCall } from "@/test/api";
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

function draftCalls(calls: ApiCall[]) {
  return calls.filter((c) => c.path.startsWith("/api/drafts") && c.method !== "GET");
}

function typist() {
  return userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
}

async function wait(ms: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

describe("drafts", () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("saves the form as a draft two seconds after typing stops", async () => {
    const saved = draft({ answers: { ...blank, clientId: "c1", loanAmount: 20000 }, updatedAt: "2026-11-10T10:42:00Z" });
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...client,
      "POST /api/drafts": { status: 201, body: saved },
      "GET /api/drafts/d1": { status: 200, body: saved },
    });
    const view = renderApp("/applications/new?clientId=c1");
    const user = typist();
    await user.type(await screen.findByLabelText("Loan amount"), "20000");
    expect(draftCalls(calls)).toHaveLength(0);
    await wait(2000);
    expect(await screen.findByText(/^Saved/)).toBeInTheDocument();
    expect(draftCalls(calls).map((c) => [c.method, c.path, c.body])).toEqual([
      ["POST", "/api/drafts", { ...blank, clientId: "c1", loanAmount: 20000 }],
    ]);
    expect(view.router.state.location.search).toMatchObject({ clientId: "c1", draftId: "d1" });
    expect(screen.getByLabelText("Loan amount")).toHaveValue("20000");
  });

  it("keeps saving later changes into the same draft", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...client,
      "POST /api/drafts": { status: 201, body: draft() },
      "GET /api/drafts/d1": { status: 200, body: draft() },
      "PUT /api/drafts/d1": { status: 200, body: draft() },
    });
    renderApp("/applications/new?clientId=c1");
    const user = typist();
    await user.type(await screen.findByLabelText("Loan amount"), "20000");
    await wait(2000);
    await screen.findByText(/^Saved/);
    await user.type(screen.getByLabelText("Credit score"), "667");
    await wait(2000);
    await vi.waitFor(() => expect(draftCalls(calls).map((c) => c.method)).toEqual(["POST", "PUT"]));
    expect((draftCalls(calls)[1].body as Record<string, unknown>).creditScore).toBe(667);
  });

  it("does not save a form nobody has typed in", async () => {
    const calls = stubApi({ ...signedInAs("Officer"), ...client });
    renderApp("/applications/new?clientId=c1");
    await screen.findByLabelText("Loan amount");
    await wait(3000);
    expect(draftCalls(calls)).toHaveLength(0);
  });

  it("has no manual save button", async () => {
    stubApi({ ...signedInAs("Officer"), ...client });
    renderApp("/applications/new?clientId=c1");
    await screen.findByLabelText("Loan amount");
    expect(screen.queryByRole("button", { name: "Save draft" })).toBeNull();
  });

  it("says when saving failed and saves again on request", async () => {
    let fail = true;
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...client,
      "POST /api/drafts": () => (fail ? { status: 500 } : { status: 201, body: draft() }),
      "GET /api/drafts/d1": { status: 200, body: draft() },
    });
    renderApp("/applications/new?clientId=c1");
    const user = typist();
    await user.type(await screen.findByLabelText("Loan amount"), "20000");
    await wait(2000);
    expect(await screen.findByText("Not saved")).toBeInTheDocument();
    fail = false;
    await user.click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByText(/^Saved/)).toBeInTheDocument();
    expect(draftCalls(calls)).toHaveLength(2);
  });

  it("saves pending changes when the officer leaves the form", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...client,
      "POST /api/drafts": { status: 201, body: draft() },
    });
    renderApp("/applications/new?clientId=c1");
    const user = typist();
    await user.type(await screen.findByLabelText("Loan amount"), "20000");
    await user.click(screen.getByRole("link", { name: "Change client" }));
    await screen.findByLabelText("Search clients");
    await vi.waitFor(() => expect(draftCalls(calls).map((c) => c.method)).toEqual(["POST"]));
  });

  it("never saves a draft once the application is submitted", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...client,
      "POST /api/applications": { status: 201, body: application({ id: "a9" }) },
      "GET /api/applications/a9": { status: 200, body: application({ id: "a9" }) },
      "GET /api/applications/a9/notes": { status: 200, body: [] },
    });
    renderApp("/applications/new?clientId=c1");
    const user = typist();
    await user.type(await screen.findByLabelText("Loan amount"), "20000");
    await user.click(screen.getByRole("button", { name: "Review" }));
    await user.click(await screen.findByRole("button", { name: "Get recommendation" }));
    await screen.findByRole("heading", { name: "Milica Testić" });
    await wait(3000);
    expect(draftCalls(calls)).toHaveLength(0);
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
    const user = typist();
    await user.click(screen.getByRole("button", { name: "Review" }));
    await user.click(await screen.findByRole("button", { name: "Get recommendation" }));
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
    const user = typist();
    await user.type(await screen.findByLabelText("Credit score"), "667");
    await wait(2000);
    await vi.waitFor(() => expect(draftCalls(calls).map((c) => c.method)).toEqual(["PUT"]));
    expect((draftCalls(calls)[0].body as Record<string, unknown>).creditScore).toBe(667);
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
    const user = typist();
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
