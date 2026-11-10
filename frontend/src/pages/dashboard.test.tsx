import { beforeAll, describe, expect, it } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { signedInAs, stubApi } from "@/test/api";
import type { DashboardStats } from "@/api/stats";
import type { ScoringModel } from "@/api/types";

const band = (from: number, count = 0) => ({ from, to: from + 0.1, count });
const empty = (from: number) => ({ from, to: from + 0.1, loans: 0, meanPredicted: null, observedDefaultRate: null });

function stats(overrides: Partial<DashboardStats> = {}): DashboardStats {
  return {
    applications: 3,
    scored: 3,
    unavailable: 0,
    decided: 1,
    pending: 2,
    matrix: {
      approve: { approved: 1, rejected: 0, pending: 0 },
      review: { approved: 0, rejected: 0, pending: 1 },
      reject: { approved: 0, rejected: 0, pending: 1 },
      unavailable: { approved: 0, rejected: 0, pending: 0 },
    },
    overrideRate: 0,
    highSideOverrideRate: 0,
    lowSideOverrideRate: null,
    unavailableRate: 0,
    pBadHistogram: [band(0, 1), band(0.1), band(0.2), band(0.3, 2), band(0.4), band(0.5), band(0.6), band(0.7), band(0.8), band(0.9)],
    officers: [{ name: "Mila Testić", decisions: 1, overrides: 0, overrideRate: 0 }],
    shadowAgreement: [],
    weekly: [
      { weekStart: "2026-11-09", applications: 3, approve: 1, review: 1, reject: 1, unavailable: 0, decided: 1, overrides: 0, overrideRate: 0, reviewShare: 1 / 3 },
    ],
    byBranch: [{ branchId: "b1", code: "NS", name: "Novi Sad", applications: 3, decided: 1, overrides: 0, overrideRate: 0, reviewShare: 1 / 3 }],
    comparison: null,
    outcomeByDecision: [
      { path: "AsRecommended", approved: 1, withOutcome: 0, defaulted: 0, defaultRate: null },
      { path: "AfterReview", approved: 0, withOutcome: 0, defaulted: 0, defaultRate: null },
      { path: "Override", approved: 0, withOutcome: 0, defaulted: 0, defaultRate: null },
      { path: "WithoutScore", approved: 0, withOutcome: 0, defaulted: 0, defaultRate: null },
    ],
    calibration: [0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9].map(empty),
    ...overrides,
  };
}

const active: ScoringModel = {
  modelId: "credit_lc_xgb_isotonic_v1",
  displayName: "XGBoost",
  dataset: "lending_club",
  threshold: 0.3286,
  reviewFrom: 0.2,
  role: "Active",
  registeredAt: "2026-10-01T08:00:00Z",
};

const common = { "GET /api/models": { status: 200, body: [active] } };

function tile(label: string) {
  return within(screen.getByRole("list", { name: "Key numbers" })).getByText(label).parentElement!;
}

describe("dashboard", () => {
  beforeAll(async () => {
    await import("@/pages/dashboard");
  });

  it("shows the key numbers counted for the period", async () => {
    stubApi({ ...signedInAs("Manager"), ...common, "GET /api/stats": { status: 200, body: stats() } });
    renderApp("/dashboard?range=all");
    await screen.findByRole("list", { name: "Key numbers" });
    expect(tile("Applications")).toHaveTextContent("3");
    expect(tile("Decided")).toHaveTextContent("1");
    expect(tile("Override rate")).toHaveTextContent("0.0%");
  });

  it("asks for the last 30 days by default and compares them with the period before", async () => {
    const calls = stubApi({
      ...signedInAs("Manager"),
      ...common,
      "GET /api/stats?from=*": {
        status: 200,
        body: stats({
          comparison: {
            current: { from: "", to: "", applications: 3, decided: 1, approvalRate: 1, overrideRate: 0, reviewShare: 1 / 3, unavailableRate: 0 },
            previous: { from: "", to: "", applications: 5, decided: 4, approvalRate: 0.5, overrideRate: 0.25, reviewShare: 0.2, unavailableRate: 0 },
          },
        }),
      },
    });
    renderApp("/dashboard");
    await screen.findByRole("list", { name: "Key numbers" });
    expect(tile("Applications")).toHaveTextContent("Previous period: 5");
    expect(tile("Approval rate")).toHaveTextContent("100.0%");
    const query = new URLSearchParams(calls.find((c) => c.path.startsWith("/api/stats"))!.path.split("?")[1]);
    const days = (Date.parse(query.get("to")!) - Date.parse(query.get("from")!)) / 86_400_000;
    expect(Math.round(days)).toBe(31);
  });

  it("puts the recommendation next to the officer's decision", async () => {
    stubApi({ ...signedInAs("Manager"), ...common, "GET /api/stats": { status: 200, body: stats() } });
    renderApp("/dashboard?range=all");
    const matrix = await screen.findByRole("table", { name: "Recommendation and decision" });
    const approveRow = within(matrix).getByText("Approve").closest("tr")!;
    expect(within(approveRow).getAllByRole("cell").map((cell) => cell.textContent)).toEqual(["1", "0", "0"]);
  });

  it("lists each officer's decisions and overrides", async () => {
    stubApi({ ...signedInAs("Manager"), ...common, "GET /api/stats": { status: 200, body: stats() } });
    renderApp("/dashboard?range=all");
    const officers = await screen.findByRole("table", { name: "By officer" });
    expect(within(officers).getByText("Mila Testić")).toBeInTheDocument();
  });

  it("shows the outcome of approved loans by how they were approved", async () => {
    stubApi({ ...signedInAs("Risk"), ...common, "GET /api/branches": { status: 200, body: [] }, "GET /api/stats": { status: 200, body: stats() } });
    renderApp("/dashboard?range=all");
    const outcomes = await screen.findByRole("table", { name: "Outcome of approved loans" });
    expect(within(outcomes).getByText("against a Reject recommendation")).toBeInTheDocument();
    expect(screen.getByText("No loan outcomes are recorded yet, so calibration cannot be shown.")).toBeInTheDocument();
  });

  it("lets risk staff narrow the numbers to one branch", async () => {
    const calls = stubApi({
      ...signedInAs("Risk"),
      ...common,
      "GET /api/branches": {
        status: 200,
        body: [{ id: "b1", code: "NS", name: "Novi Sad", city: null, isActive: true, createdAt: "2026-01-01T00:00:00Z" }],
      },
      "GET /api/stats": { status: 200, body: stats() },
      "GET /api/stats?branchId=b1": { status: 200, body: stats({ applications: 2 }) },
    });
    renderApp("/dashboard?range=all");
    const user = userEvent.setup();
    await user.selectOptions(await screen.findByLabelText("Branch"), "Novi Sad");
    await waitFor(() => expect(tile("Applications")).toHaveTextContent(/^Applications2$/));
    expect(calls.map((c) => c.path)).toContain("/api/stats?branchId=b1");
  });

  it("does not offer a branch filter to a manager, whose numbers are already the branch's", async () => {
    stubApi({ ...signedInAs("Manager"), ...common, "GET /api/stats": { status: 200, body: stats() } });
    renderApp("/dashboard?range=all");
    await screen.findByRole("list", { name: "Key numbers" });
    expect(screen.queryByLabelText("Branch")).toBeNull();
  });

  it("says when the period has no applications", async () => {
    stubApi({ ...signedInAs("Manager"), ...common, "GET /api/stats": { status: 200, body: stats({ applications: 0, weekly: [] }) } });
    renderApp("/dashboard?range=all");
    expect(await screen.findByText("No applications in this period")).toBeInTheDocument();
  });

  it("offers each chart as a table", async () => {
    stubApi({ ...signedInAs("Manager"), ...common, "GET /api/stats": { status: 200, body: stats() } });
    renderApp("/dashboard?range=all");
    const weekly = await screen.findByRole("table", { name: "Applications per week by recommendation" });
    expect(within(weekly).getByText("9 Nov 2026")).toBeInTheDocument();
  });
});
