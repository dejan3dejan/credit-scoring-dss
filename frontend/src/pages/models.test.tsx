import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { problem, signedInAs, stubApi } from "@/test/api";
import type { ScoringModel } from "@/api/types";

const active: ScoringModel = {
  modelId: "credit_lc_xgb_isotonic_v1",
  displayName: "XGBoost",
  dataset: "lending_club_2012_2014",
  threshold: 0.3286,
  reviewFrom: 0.2,
  role: "Active",
  registeredAt: "2026-10-01T08:00:00Z",
};
const shadow: ScoringModel = { ...active, modelId: "credit_lc_lr_isotonic_v1", displayName: "LogisticRegression", role: "Shadow" };

describe("model page", () => {
  it("shows every role what the active model is and when it rejects", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/models": { status: 200, body: [active] } });
    renderApp("/models");
    expect(await screen.findByText("credit_lc_xgb_isotonic_v1")).toBeInTheDocument();
    expect(screen.getByText(/P\(bad\) ≥ 32\.9%/)).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Why the threshold is 32.9%" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Registry" })).toBeNull();
  });

  it("warns when no model is active", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/models": { status: 200, body: [shadow] } });
    renderApp("/models");
    expect(await screen.findByText("No model is set as active, so new applications are recorded without a score.")).toBeInTheDocument();
  });

  it("lets risk staff make a shadow model active with a reason", async () => {
    const calls = stubApi({
      ...signedInAs("Risk"),
      "GET /api/models": { status: 200, body: [active, shadow] },
      "GET /api/models/available": { status: 200, body: [] },
      "GET /api/models/events": { status: 200, body: [] },
      "POST /api/models/credit_lc_lr_isotonic_v1/promote": {
        status: 200,
        body: [{ ...active, role: "Shadow" }, { ...shadow, role: "Active" }],
      },
    });
    renderApp("/models");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Reason for changing credit_lc_lr_isotonic_v1"), "Better calibrated on 2015.");
    const row = screen.getByLabelText("Reason for changing credit_lc_lr_isotonic_v1").closest("li")!;
    await user.click(within(row).getByRole("button", { name: "Make active" }));
    expect(await screen.findByText("The registry was updated.")).toBeInTheDocument();
    expect(calls.find((c) => c.method === "POST")?.body).toEqual({ reason: "Better calibrated on 2015." });
  });

  it("registers a served model as a shadow", async () => {
    const calls = stubApi({
      ...signedInAs("Risk"),
      "GET /api/models": { status: 200, body: [active] },
      "GET /api/models/available": {
        status: 200,
        body: [
          {
            modelId: "credit_lc_lr_isotonic_v1",
            modelName: "LogisticRegression",
            dataset: "lending_club_2012_2014",
            recommendedThreshold: 0.3286,
            reviewFrom: 0.2,
            createdAt: "2026-10-20T08:00:00Z",
          },
        ],
      },
      "GET /api/models/events": { status: 200, body: [] },
      "POST /api/models": { status: 201, body: shadow },
    });
    renderApp("/models");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Register as shadow" }));
    expect(await screen.findByText("credit_lc_lr_isotonic_v1 was registered as a shadow.")).toBeInTheDocument();
    expect(calls.find((c) => c.method === "POST")?.body).toEqual({ modelId: "credit_lc_lr_isotonic_v1" });
  });

  it("explains when the inference service cannot list its models", async () => {
    stubApi({
      ...signedInAs("Risk"),
      "GET /api/models": { status: 200, body: [active] },
      "GET /api/models/available": problem(503, "inference_unavailable"),
      "GET /api/models/events": { status: 200, body: [] },
    });
    renderApp("/models");
    expect(await screen.findByText("The inference service could not list its models")).toBeInTheDocument();
  });
});
