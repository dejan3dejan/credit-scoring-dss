import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { signedInAs, stubApi } from "@/test/api";
import { paged, summary } from "@/test/fixtures";

describe("applications list", () => {
  it("shows each application with its risk, recommendation and stage", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/applications": { status: 200, body: paged([summary()]) } });
    renderApp("/applications");
    const row = (await screen.findByRole("link", { name: "A-000001" })).closest("tr")!;
    expect(within(row).getByText("Milica Testić")).toBeInTheDocument();
    expect(within(row).getByText(/USD\s20,000/)).toBeInTheDocument();
    expect(within(row).getByText("23.7%")).toBeInTheDocument();
    expect(within(row).getByText("Review closely")).toBeInTheDocument();
    expect(within(row).getByText("Awaiting decision")).toBeInTheDocument();
  });

  it("marks a decision against the model as an override", async () => {
    stubApi({
      ...signedInAs("Officer"),
      "GET /api/applications": {
        status: 200,
        body: paged([summary({ stage: "Decided", recommendation: "Approve", finalDecision: "Reject", isOverride: true })]),
      },
    });
    renderApp("/applications");
    const row = (await screen.findByRole("link", { name: "A-000001" })).closest("tr")!;
    expect(within(row).getByText("Rejected")).toBeInTheDocument();
    expect(within(row).getByText("Override")).toBeInTheDocument();
  });

  it("links each reference to the application", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/applications": { status: 200, body: paged([summary()]) } });
    renderApp("/applications");
    expect(await screen.findByRole("link", { name: "A-000001" })).toHaveAttribute("href", "/app/applications/a1");
  });

  it("says when there are no applications at all", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/applications": { status: 200, body: paged([]) } });
    renderApp("/applications");
    expect(await screen.findByText("No applications yet")).toBeInTheDocument();
  });

  it("searches by applicant name", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      "GET /api/applications": { status: 200, body: paged([summary()]) },
      "GET /api/applications?applicantName=Nobody": { status: 200, body: paged([]) },
    });
    renderApp("/applications");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Applicant name"), "Nobody");
    await user.click(screen.getByRole("button", { name: "Search" }));
    expect(await screen.findByText("No results")).toBeInTheDocument();
    expect(calls.map((c) => c.path)).toContain("/api/applications?applicantName=Nobody");
  });

  it("filters to overrides", async () => {
    const calls = stubApi({
      ...signedInAs("Manager"),
      "GET /api/applications": { status: 200, body: paged([summary()]) },
      "GET /api/applications?review=Override": { status: 200, body: paged([]) },
    });
    renderApp("/applications");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("link", { name: "Overrides" }));
    await screen.findByText("No results");
    expect(calls.map((c) => c.path)).toContain("/api/applications?review=Override");
  });

  it("pages through long lists", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      "GET /api/applications": { status: 200, body: paged([summary()], 45) },
      "GET /api/applications?page=2": { status: 200, body: paged([summary({ id: "a2", reference: "A-000021" })], 45, 2) },
    });
    renderApp("/applications");
    expect(await screen.findByText("1–20 of 45")).toBeInTheDocument();
    const user = userEvent.setup();
    await user.click(screen.getByRole("link", { name: "Next" }));
    expect(await screen.findByRole("link", { name: "A-000021" })).toBeInTheDocument();
    expect(screen.getByText("21–40 of 45")).toBeInTheDocument();
    expect(calls.map((c) => c.path)).toContain("/api/applications?page=2");
  });
});
