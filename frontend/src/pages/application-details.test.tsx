import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { problem, signedInAs, stubApi } from "@/test/api";
import { application, unscored } from "@/test/fixtures";
import { i18n } from "@/i18n";

const get = (body: unknown) => ({ "GET /api/applications/a1": { status: 200, body } });

describe("application details", () => {
  it("shows the probability, the recommendation in words and both edges of the scale", async () => {
    stubApi({ ...signedInAs("Officer"), ...get(application()) });
    renderApp("/applications/a1");
    expect(await screen.findByRole("heading", { name: "Milica Testić" })).toBeInTheDocument();
    expect(screen.getAllByText("23.7%").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Review closely").length).toBeGreaterThan(0);
    expect(
      screen.getByRole("img", { name: "Probability of default 23.7%; review closely from 20.0%, reject from 32.9%" }),
    ).toBeInTheDocument();
  });

  it("lists what moved the score in words", async () => {
    stubApi({ ...signedInAs("Officer"), ...get(application()) });
    renderApp("/applications/a1");
    await screen.findByRole("heading", { name: "What moved the score" });
    expect(screen.getAllByText("Raises risk")).toHaveLength(3);
    expect(screen.getAllByText("Lowers risk")).toHaveLength(1);
    expect(screen.getAllByText("Small business").length).toBeGreaterThan(0);
  });

  it("asks for a closer look at the answers that raised a review case", async () => {
    stubApi({ ...signedInAs("Officer"), ...get(application()) });
    renderApp("/applications/a1");
    expect(await screen.findByText("Before you decide")).toBeInTheDocument();
    expect(screen.getByText(/Ask what the money is for in detail/)).toBeInTheDocument();
  });

  it("records the assigned officer's decision with the reason", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...get(application()),
      "POST /api/applications/a1/decision": {
        status: 201,
        body: application({
          stage: "Decided",
          officerDecision: {
            outcome: "Approve",
            reason: "Stable income, documented purpose.",
            decidedBy: "Mila Testić",
            decidedAt: "2026-11-02T10:00:00Z",
            isOverride: false,
            signedOffBy: null,
            signedOffAt: null,
          },
        }),
      },
    });
    renderApp("/applications/a1");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("radio", { name: "Approve" }));
    await user.type(screen.getByLabelText("Reason"), "Stable income, documented purpose.");
    await user.click(screen.getByRole("button", { name: "Record decision" }));
    expect(await screen.findByText("Approved")).toBeInTheDocument();
    expect(calls.find((c) => c.method === "POST")?.body).toEqual({
      outcome: "Approve",
      reason: "Stable income, documented purpose.",
    });
    expect(screen.queryByRole("button", { name: "Record decision" })).toBeNull();
  });

  it("shows the API's reason requirement next to the reason field", async () => {
    stubApi({
      ...signedInAs("Officer"),
      ...get(application()),
      "POST /api/applications/a1/decision": problem(400, "validation_failed", {
        fieldErrors: { reason: [{ code: "reason_required_review" }] },
      }),
    });
    renderApp("/applications/a1");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("radio", { name: "Reject" }));
    await user.click(screen.getByRole("button", { name: "Record decision" }));
    expect(await screen.findByText("Give a reason: the model asked for a closer review.")).toBeInTheDocument();
  });

  it("tells the officer when an approval goes to a manager for sign-off", async () => {
    const pending = application({
      stage: "AwaitingSignOff",
      signOffs: [
        {
          id: "s1",
          outcome: "Approve",
          reason: "Collateral offered.",
          why: "OverridesReject",
          requestedBy: "Mila Testić",
          requestedAt: "2026-11-02T10:00:00Z",
          status: "Pending",
          reviewedBy: null,
          reviewedAt: null,
          note: null,
        },
      ],
    });
    stubApi({
      ...signedInAs("Officer"),
      ...get(application()),
      "POST /api/applications/a1/decision": { status: 201, body: pending },
    });
    renderApp("/applications/a1");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("radio", { name: "Approve" }));
    await user.type(screen.getByLabelText("Reason"), "Collateral offered.");
    await user.click(screen.getByRole("button", { name: "Record decision" }));
    expect(await screen.findByRole("heading", { name: "Waiting for a manager's sign-off" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Approve the decision" })).toBeNull();
  });

  it("does not offer the decision to an officer the application is not assigned to", async () => {
    stubApi({
      ...signedInAs("Officer"),
      ...get(application({ assignedTo: { id: "someone-else", displayName: "Nikola Drugi" } })),
    });
    renderApp("/applications/a1");
    expect(await screen.findByText(/assigned to Nikola Drugi/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Record decision" })).toBeNull();
  });

  it("shows risk staff the other models' scores but no decision form", async () => {
    stubApi({
      ...signedInAs("Risk"),
      ...get(application()),
      "GET /api/applications/a1/shadow-scores": {
        status: 200,
        body: [
          {
            modelId: "credit_lr_isotonic_v1",
            status: "Scored",
            pBad: 0.25,
            thresholdUsed: 0.3,
            reviewFromUsed: 0.2,
            recommendation: "Review",
            errorCode: null,
            scoredAt: "2026-11-02T09:30:03Z",
          },
        ],
      },
    });
    renderApp("/applications/a1");
    expect(await screen.findByText("credit_lr_isotonic_v1")).toBeInTheDocument();
    expect(screen.getByText("Agrees")).toBeInTheDocument();
    expect(screen.getByText("Your role can view applications but not decide them.")).toBeInTheDocument();
  });

  it("explains a missing score and scores again on request", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...get(unscored()),
      "POST /api/applications/a1/rescore": { status: 200, body: application() },
    });
    renderApp("/applications/a1");
    expect(await screen.findByText("Scoring unavailable")).toBeInTheDocument();
    expect(screen.getByText(/The scoring service could not be reached\./)).toBeInTheDocument();
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Score again" }));
    expect((await screen.findAllByText("23.7%")).length).toBeGreaterThan(0);
    expect(calls.some((c) => c.method === "POST" && c.path === "/api/applications/a1/rescore")).toBe(true);
  });

  it("lets a manager approve a pending sign-off", async () => {
    const pending = application({
      stage: "AwaitingSignOff",
      loanAmount: 30000,
      signOffs: [
        {
          id: "s1",
          outcome: "Approve",
          reason: null,
          why: "LargeAmount",
          requestedBy: "Mila Testić",
          requestedAt: "2026-11-02T10:00:00Z",
          status: "Pending",
          reviewedBy: null,
          reviewedAt: null,
          note: null,
        },
      ],
    });
    const calls = stubApi({
      ...signedInAs("Manager"),
      ...get(pending),
      "GET /api/applications/a1/shadow-scores": { status: 200, body: [] },
      "POST /api/applications/a1/sign-off": {
        status: 200,
        body: application({
          stage: "Decided",
          officerDecision: {
            outcome: "Approve",
            reason: null,
            decidedBy: "Mila Testić",
            decidedAt: "2026-11-02T10:00:00Z",
            isOverride: false,
            signedOffBy: "Jovan Testić",
            signedOffAt: "2026-11-02T11:00:00Z",
          },
        }),
      },
    });
    renderApp("/applications/a1");
    expect(await screen.findByText(/amount above 25,000 USD/)).toBeInTheDocument();
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Approve the decision" }));
    expect(await screen.findByText(/Signed off by Jovan Testić/)).toBeInTheDocument();
    expect(calls.find((c) => c.path === "/api/applications/a1/sign-off")?.body).toEqual({ approve: true, note: null });
  });

  it("shows a recorded decision without a form", async () => {
    stubApi({
      ...signedInAs("Officer"),
      ...get(
        application({
          stage: "Decided",
          officerDecision: {
            outcome: "Reject",
            reason: "Income could not be verified.",
            decidedBy: "Mila Testić",
            decidedAt: "2026-11-02T10:00:00Z",
            isOverride: false,
            signedOffBy: null,
            signedOffAt: null,
          },
        }),
      ),
    });
    renderApp("/applications/a1");
    expect(await screen.findByText("Income could not be verified.")).toBeInTheDocument();
    expect(screen.getAllByText("Rejected").length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "Record decision" })).toBeNull();
  });

  it("is shown in Serbian", async () => {
    stubApi({ ...signedInAs("Officer", { preferredLanguage: "sr" }), ...get(application()) });
    renderApp("/applications/a1");
    expect(await screen.findByRole("heading", { name: "Šta je uticalo na ocenu" })).toBeInTheDocument();
    expect(screen.getAllByText("Detaljna provera").length).toBeGreaterThan(0);
    expect(i18n.language).toBe("sr");
  });
});
