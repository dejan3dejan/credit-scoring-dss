import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { signedInAs, stubApi } from "@/test/api";
import { paged } from "@/test/fixtures";
import type { AuditEntry } from "@/api/audit";

function entry(overrides: Partial<AuditEntry> = {}): AuditEntry {
  return {
    id: "e1",
    at: "2026-11-09T14:03:12Z",
    userId: "u1",
    userName: "Vesna Testić",
    action: "Updated",
    entity: "User",
    entityId: "u7",
    traceId: "t1",
    changes: { role: { before: "Officer", after: "Manager" } },
    ...overrides,
  };
}

describe("audit log", () => {
  it("shows who changed what, with the values before and after", async () => {
    stubApi({ ...signedInAs("Risk"), "GET /api/audit": { status: 200, body: paged([entry()], 1, 1, 50) } });
    renderApp("/audit");
    const row = (await screen.findByText("Vesna Testić")).closest("tr")!;
    expect(within(row).getByText("Updated")).toBeInTheDocument();
    expect(within(row).getByText("User")).toBeInTheDocument();
    expect(within(row).getByText("role")).toBeInTheDocument();
    expect(within(row).getByText("Officer")).toBeInTheDocument();
    expect(within(row).getByText("Manager")).toBeInTheDocument();
  });

  it("names hidden and personal values instead of showing them", async () => {
    stubApi({
      ...signedInAs("Admin"),
      "GET /api/audit": {
        status: 200,
        body: paged([
          entry({ entity: "Client", changes: { phone: { before: "[personal]", after: "[personal]" }, passwordHash: { before: "[redacted]", after: "[redacted]" } } }),
        ]),
      },
    });
    renderApp("/audit");
    expect((await screen.findAllByText("personal data")).length).toBe(2);
    expect(screen.getAllByText("hidden").length).toBe(2);
  });

  it("marks changes no signed-in user made as the system's", async () => {
    stubApi({ ...signedInAs("Admin"), "GET /api/audit": { status: 200, body: paged([entry({ userId: null, userName: null })]) } });
    renderApp("/audit");
    expect(await screen.findByText("System")).toBeInTheDocument();
  });

  it("filters by record type and change", async () => {
    const calls = stubApi({
      ...signedInAs("Admin"),
      "GET /api/audit": { status: 200, body: paged([entry()]) },
      "GET /api/audit?entity=Client": { status: 200, body: paged([]) },
      "GET /api/audit?entity=Client&action=Deleted": { status: 200, body: paged([]) },
    });
    renderApp("/audit");
    const user = userEvent.setup();
    await user.selectOptions(await screen.findByLabelText("Record type"), "Client");
    await screen.findByText("No changes match these filters.");
    await user.selectOptions(screen.getByLabelText("Change"), "Deleted");
    await screen.findByText("No changes match these filters.");
    expect(calls.map((c) => c.path)).toContain("/api/audit?entity=Client&action=Deleted");
  });

  it("narrows the log to one person's changes", async () => {
    const calls = stubApi({
      ...signedInAs("Admin"),
      "GET /api/audit": { status: 200, body: paged([entry()]) },
      "GET /api/audit?userId=u1": { status: 200, body: paged([entry()]) },
    });
    renderApp("/audit");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("link", { name: "Show only Vesna Testić" }));
    expect(await screen.findByRole("link", { name: "Show every user" })).toBeInTheDocument();
    expect(calls.map((c) => c.path)).toContain("/api/audit?userId=u1");
  });

  it("links an application record to its page for roles that can read it", async () => {
    stubApi({
      ...signedInAs("Risk"),
      "GET /api/audit": { status: 200, body: paged([entry({ entity: "CreditApplication", entityId: "a1", changes: { stage: { before: "Submitted", after: "Decided" } } })]) },
    });
    renderApp("/audit");
    expect(await screen.findByRole("link", { name: "a1" })).toHaveAttribute("href", "/app/applications/a1");
  });
});
