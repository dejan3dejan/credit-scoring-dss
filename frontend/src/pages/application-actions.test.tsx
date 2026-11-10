import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { signedInAs, stubApi } from "@/test/api";
import { application, paged, summary } from "@/test/fixtures";

const get = (body: unknown) => ({ "GET /api/applications/a1": { status: 200, body } });
const noNotes = { "GET /api/applications/a1/notes": { status: 200, body: [] } };

const decidedApproved = application({
  stage: "Decided",
  officerDecision: {
    outcome: "Approve",
    reason: null,
    decidedBy: "Mila Testić",
    decidedAt: "2026-11-02T10:00:00Z",
    isOverride: false,
    signedOffBy: null,
    signedOffAt: null,
  },
});

describe("notes", () => {
  it("lists the notes and adds one", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...get(application()),
      "GET /api/applications/a1/notes": {
        status: 200,
        body: [{ id: "n1", text: "Called the employer.", author: "Mila Testić", createdAt: "2026-11-02T09:45:00Z" }],
      },
      "POST /api/applications/a1/notes": {
        status: 201,
        body: { id: "n2", text: "Pay slips received.", author: "Mila Testić", createdAt: "2026-11-02T10:15:00Z" },
      },
    });
    renderApp("/applications/a1");
    expect(await screen.findByText("Called the employer.")).toBeInTheDocument();
    const user = userEvent.setup();
    await user.type(screen.getByLabelText("Add a note"), "Pay slips received.");
    await user.click(screen.getByRole("button", { name: "Add note" }));
    expect(await screen.findByText("Pay slips received.")).toBeInTheDocument();
    expect(calls.find((c) => c.path === "/api/applications/a1/notes" && c.method === "POST")?.body).toEqual({ text: "Pay slips received." });
  });

  it("does not offer a note to roles that cannot write", async () => {
    stubApi({ ...signedInAs("Risk"), ...get(application()), ...noNotes, "GET /api/applications/a1/shadow-scores": { status: 200, body: [] } });
    renderApp("/applications/a1");
    expect(await screen.findByText("No notes yet.")).toBeInTheDocument();
    expect(screen.queryByLabelText("Add a note")).toBeNull();
  });
});

describe("withdrawal", () => {
  it("withdraws with a reason after a confirmation", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...get(application()),
      ...noNotes,
      "POST /api/applications/a1/withdraw": {
        status: 200,
        body: application({ stage: "Withdrawn", withdrawal: { at: "2026-11-02T11:00:00Z", by: "Mila Testić", reason: "Client found another lender." } }),
      },
    });
    renderApp("/applications/a1");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Withdraw application" }));
    await user.type(screen.getByLabelText("Reason for withdrawing"), "Client found another lender.");
    await user.click(screen.getByRole("button", { name: "Withdraw" }));
    expect((await screen.findAllByText(/Withdrawn by Mila Testić/)).length).toBeGreaterThan(0);
    expect(calls.find((c) => c.path === "/api/applications/a1/withdraw")?.body).toEqual({ reason: "Client found another lender." });
    expect(screen.queryByRole("button", { name: "Withdraw application" })).toBeNull();
  });
});

describe("assignment", () => {
  it("lets a manager reassign the application to an officer of the branch", async () => {
    const calls = stubApi({
      ...signedInAs("Manager"),
      ...get(application()),
      ...noNotes,
      "GET /api/applications/a1/shadow-scores": { status: 200, body: [] },
      "GET /api/work/team": {
        status: 200,
        body: {
          counts: { unassigned: 0, awaitingSignOff: 0, scoringUnavailable: 0 },
          unassigned: [],
          awaitingSignOff: [],
          officers: [
            { id: "00000000-0000-0000-0000-000000000001", displayName: "Mila Testić", role: "Officer", open: 1, awaitingSignOff: 0 },
            { id: "u9", displayName: "Nikola Drugi", role: "Officer", open: 0, awaitingSignOff: 0 },
          ],
        },
      },
      "POST /api/applications/a1/assign": {
        status: 200,
        body: application({ assignedTo: { id: "u9", displayName: "Nikola Drugi" } }),
      },
    });
    renderApp("/applications/a1");
    const user = userEvent.setup();
    await user.selectOptions(await screen.findByLabelText("Assign to"), "Nikola Drugi");
    await user.click(screen.getByRole("button", { name: "Assign" }));
    expect(await screen.findByText("The application was assigned to Nikola Drugi.")).toBeInTheDocument();
    expect(calls.find((c) => c.path === "/api/applications/a1/assign")?.body).toEqual({ userId: "u9" });
  });

  it("is not offered to officers", async () => {
    stubApi({ ...signedInAs("Officer"), ...get(application()), ...noNotes });
    renderApp("/applications/a1");
    await screen.findByRole("heading", { name: "Notes" });
    expect(screen.queryByLabelText("Assign to")).toBeNull();
  });
});

describe("loan outcome", () => {
  it("lets risk staff record what happened to an approved loan", async () => {
    const calls = stubApi({
      ...signedInAs("Risk"),
      ...get(decidedApproved),
      ...noNotes,
      "GET /api/applications/a1/shadow-scores": { status: 200, body: [] },
      "POST /api/applications/a1/outcome": {
        status: 200,
        body: { ...decidedApproved, loanOutcome: { outcome: "Defaulted", note: "Stopped paying in month 9.", recordedBy: "Vera Testić", recordedAt: "2027-08-01T09:00:00Z" } },
      },
    });
    renderApp("/applications/a1");
    const user = userEvent.setup();
    const card = (await screen.findByRole("heading", { name: "Loan outcome" })).closest("section")!;
    await user.click(within(card).getByRole("radio", { name: "Defaulted" }));
    await user.type(within(card).getByLabelText("Note"), "Stopped paying in month 9.");
    await user.click(within(card).getByRole("button", { name: "Record outcome" }));
    expect(await screen.findByText(/Defaulted, recorded by Vera Testić/)).toBeInTheDocument();
    expect(calls.find((c) => c.path === "/api/applications/a1/outcome")?.body).toEqual({
      outcome: "Defaulted",
      note: "Stopped paying in month 9.",
    });
  });

  it("is not asked for a rejected application, where no loan was issued", async () => {
    stubApi({
      ...signedInAs("Risk"),
      ...get({ ...decidedApproved, officerDecision: { ...decidedApproved.officerDecision!, outcome: "Reject" } }),
      ...noNotes,
      "GET /api/applications/a1/shadow-scores": { status: 200, body: [] },
    });
    renderApp("/applications/a1");
    await screen.findByRole("heading", { name: "Notes" });
    expect(screen.queryByRole("heading", { name: "Loan outcome" })).toBeNull();
  });

  it("filters the list to approved loans still waiting for an outcome", async () => {
    const calls = stubApi({
      ...signedInAs("Risk"),
      "GET /api/applications": { status: 200, body: paged([summary()]) },
      "GET /api/applications?loanOutcome=Pending": { status: 200, body: paged([]) },
    });
    renderApp("/applications");
    const user = userEvent.setup();
    await user.selectOptions(await screen.findByLabelText("Loan outcome"), "Outcome not recorded");
    await screen.findByText("No results");
    expect(calls.map((c) => c.path)).toContain("/api/applications?loanOutcome=Pending");
  });
});
