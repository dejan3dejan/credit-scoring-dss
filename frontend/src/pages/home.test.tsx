import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import { renderApp } from "@/test/render";
import { signedInAs, stubApi } from "@/test/api";
import { paged, summary } from "@/test/fixtures";

const mine = (overrides: Record<string, unknown> = {}) => ({
  counts: { drafts: 1, awaitingDecision: 1, scoringUnavailable: 0, awaitingSignOff: 0, decidedLast7Days: 4 },
  drafts: [],
  awaitingDecision: [summary()],
  scoringUnavailable: [],
  awaitingSignOff: [],
  recentlyDecided: [],
  ...overrides,
});

const period = (overrides: Record<string, unknown> = {}) => ({
  from: "2026-10-11T00:00:00Z",
  to: "2026-11-11T00:00:00Z",
  applications: 12,
  decided: 10,
  approvalRate: 0.5,
  overrideRate: 0.1,
  reviewShare: 0.25,
  unavailableRate: 0.05,
  ...overrides,
});

const stats = (overrides: Record<string, unknown> = {}) => ({
  comparison: { current: period(), previous: period({ applications: 8, approvalRate: 0.625, overrideRate: null }) },
  calibration: [
    { from: 0, to: 0.1, loans: 4, meanPredicted: 0.06, observedDefaultRate: 0.25 },
    { from: 0.1, to: 0.2, loans: 0, meanPredicted: null, observedDefaultRate: null },
  ],
  ...overrides,
});

const team = {
  counts: { unassigned: 0, awaitingSignOff: 0, scoringUnavailable: 0 },
  unassigned: [],
  awaitingSignOff: [],
  officers: [],
};

function tile(list: HTMLElement, label: string) {
  return within(list).getByText(label).closest("li")!;
}

describe("home", () => {
  it("shows an officer the counts and the applications waiting for them", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/work/mine": { status: 200, body: mine() } });
    renderApp("/");
    const tiles = await screen.findByRole("list", { name: "My work" });
    expect(within(tiles).getByText("Awaiting my decision").nextSibling).toHaveTextContent("1");
    expect(within(tiles).getByText("Decided in the last 7 days").nextSibling).toHaveTextContent("4");
    expect(screen.getByRole("link", { name: "A-000001" })).toBeInTheDocument();
  });

  it("says so when nothing waits", async () => {
    stubApi({
      ...signedInAs("Officer"),
      "GET /api/work/mine": {
        status: 200,
        body: mine({ awaitingDecision: [], counts: { drafts: 0, awaitingDecision: 0, scoringUnavailable: 0, awaitingSignOff: 0, decidedLast7Days: 0 } }),
      },
    });
    renderApp("/");
    expect(await screen.findByText("Nothing waits for your decision.")).toBeInTheDocument();
  });

  it("shows a manager the team's sign-offs and each officer's open work", async () => {
    stubApi({
      ...signedInAs("Manager"),
      "GET /api/work/mine": { status: 200, body: mine({ awaitingDecision: [] }) },
      "GET /api/work/team": {
        status: 200,
        body: {
          counts: { unassigned: 0, awaitingSignOff: 1, scoringUnavailable: 0 },
          unassigned: [],
          awaitingSignOff: [summary({ id: "a5", reference: "A-000005", stage: "AwaitingSignOff" })],
          officers: [{ id: "u1", displayName: "Mila Testić", role: "Officer", open: 3, awaitingSignOff: 1 }],
        },
      },
    });
    renderApp("/");
    expect(await screen.findByRole("heading", { name: "Team" })).toBeInTheDocument();
    expect(await screen.findByRole("link", { name: "A-000005" })).toBeInTheDocument();
    const officers = screen.getByRole("table", { name: "Officers" });
    expect(within(officers).getByText("Mila Testić")).toBeInTheDocument();
    expect(within(officers).getByText("3")).toBeInTheDocument();
  });

  it("shows a manager the branch's last 30 days against the 30 days before", async () => {
    const calls = stubApi({
      ...signedInAs("Manager"),
      "GET /api/work/mine": { status: 200, body: mine({ awaitingDecision: [] }) },
      "GET /api/work/team": { status: 200, body: team },
      "GET /api/stats*": { status: 200, body: stats() },
    });
    renderApp("/");
    const numbers = await screen.findByRole("list", { name: "Branch, last 30 days" });
    expect(tile(numbers, "Applications")).toHaveTextContent("12");
    expect(tile(numbers, "Applications")).toHaveTextContent("Previous period: 8");
    expect(tile(numbers, "Approval rate")).toHaveTextContent("50.0%");
    expect(tile(numbers, "Override rate")).toHaveTextContent("Previous period: –");
    const call = calls.find((c) => c.path.startsWith("/api/stats"))!;
    expect(call.path).toMatch(/from=.*&to=/);
  });

  it("shows risk the loans waiting for an outcome, the active model and the model's numbers", async () => {
    stubApi({
      ...signedInAs("Risk"),
      "GET /api/applications?loanOutcome=Pending&pageSize=5": {
        status: 200,
        body: paged([summary({ id: "a3", reference: "A-000003" })], 7, 1, 5),
      },
      "GET /api/models": {
        status: 200,
        body: [
          { modelId: "lc-lgbm-v2", displayName: "LightGBM v2", dataset: "Lending Club", threshold: 0.33, reviewFrom: 0.2, role: "Active", registeredAt: "2026-11-01T08:00:00Z" },
          { modelId: "lc-lr-v1", displayName: "Logistic regression v1", dataset: "Lending Club", threshold: 0.3, reviewFrom: null, role: "Shadow", registeredAt: "2026-10-01T08:00:00Z" },
        ],
      },
      "GET /api/stats*": { status: 200, body: stats() },
    });
    renderApp("/");
    const outcomes = await screen.findByRole("region", { name: "Loans waiting for an outcome" });
    expect(await within(outcomes).findByText("7")).toBeInTheDocument();
    expect(within(outcomes).getByRole("link", { name: "A-000003" })).toBeInTheDocument();
    expect(within(outcomes).getByRole("link", { name: "Show all" })).toHaveAttribute("href", "/app/applications?loanOutcome=Pending");
    expect(await screen.findByText("LightGBM v2")).toBeInTheDocument();
    expect(screen.queryByText("Logistic regression v1")).toBeNull();
    const numbers = await screen.findByRole("list", { name: "All branches, last 30 days" });
    expect(tile(numbers, "Review share")).toHaveTextContent("25.0%");
    expect(tile(numbers, "Scoring unavailable")).toHaveTextContent("5.0%");
    const calibration = await screen.findByRole("table", { name: "Predicted and observed default" });
    expect(within(calibration).getAllByRole("row")).toHaveLength(2);
    expect(within(calibration).getByText("25.0%")).toBeInTheDocument();
  });

  it("shows an admin the locked users and the latest changes", async () => {
    stubApi({
      ...signedInAs("Admin"),
      "GET /api/users": {
        status: 200,
        body: [
          { id: "u1", userName: "mila", displayName: "Mila Testić", email: null, role: "Officer", branch: null, isActive: true, preferredLanguage: "en", mustChangePassword: false, createdAt: "2026-10-01T08:00:00Z", lastSignedInAt: null, lockedUntil: null },
          { id: "u2", userName: "vera", displayName: "Vera Testić", email: null, role: "Risk", branch: null, isActive: true, preferredLanguage: "en", mustChangePassword: false, createdAt: "2026-10-01T08:00:00Z", lastSignedInAt: null, lockedUntil: "2026-11-10T12:00:00Z" },
        ],
      },
      "GET /api/branches": { status: 200, body: [] },
      "GET /api/audit?pageSize=50": {
        status: 200,
        body: paged(
          [
            { id: "e0", at: "2026-11-10T09:31:00Z", userId: null, userName: null, action: "Updated", entity: "User", entityId: "u1", traceId: null, changes: {} },
            { id: "e1", at: "2026-11-10T09:30:00Z", userId: "u9", userName: "Petar Admin", action: "Created", entity: "Branch", entityId: "b2", traceId: null, changes: {} },
          ],
          2,
          1,
          50,
        ),
      },
    });
    renderApp("/");
    const locked = await screen.findByRole("table", { name: "Locked now" });
    expect(within(locked).getByRole("link", { name: "Vera Testić" })).toHaveAttribute("href", "/app/users/u2");
    expect(within(locked).queryByText("Mila Testić")).toBeNull();
    const recent = await screen.findByRole("table", { name: "Latest changes" });
    expect(within(recent).getByText("Petar Admin")).toBeInTheDocument();
    expect(within(recent).getByText("Created")).toBeInTheDocument();
    expect(within(recent).queryByText("System")).toBeNull();
    expect(within(recent).getAllByRole("row")).toHaveLength(2);
    expect(screen.getByRole("link", { name: "Open the audit log" })).toHaveAttribute("href", "/app/audit");
  });
});
