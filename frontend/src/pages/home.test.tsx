import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import { renderApp } from "@/test/render";
import { signedInAs, stubApi } from "@/test/api";
import { summary } from "@/test/fixtures";

const mine = (overrides: Record<string, unknown> = {}) => ({
  counts: { drafts: 1, awaitingDecision: 1, scoringUnavailable: 0, awaitingSignOff: 0, decidedLast7Days: 4 },
  drafts: [],
  awaitingDecision: [summary()],
  scoringUnavailable: [],
  awaitingSignOff: [],
  recentlyDecided: [],
  ...overrides,
});

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
});
