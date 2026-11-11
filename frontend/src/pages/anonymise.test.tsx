import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { problem, signedInAs, stubApi } from "@/test/api";
import { clientResponse, paged } from "@/test/fixtures";

const item = (overrides: Record<string, unknown> = {}) => ({
  id: "c1",
  clientNumber: "C-000007",
  fullName: "Milica Testić",
  branchId: "b1",
  isActive: true,
  anonymisedAt: null,
  createdAt: "2026-10-01T08:00:00Z",
  ...overrides,
});

const branches = {
  "GET /api/branches": {
    status: 200,
    body: [{ id: "b1", code: "NS", name: "Novi Sad", city: "Novi Sad", isActive: true, createdAt: "2026-01-01T00:00:00Z", version: 1 }],
  },
};

describe("anonymise clients", () => {
  it("lists clients for Admin with number, name, branch and status", async () => {
    stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "GET /api/clients/lookup": {
        status: 200,
        body: paged([item(), item({ id: "c2", clientNumber: "C-000008", fullName: "Anonymised", isActive: false, anonymisedAt: "2026-11-02T10:00:00Z" })]),
      },
    });
    renderApp("/anonymise");
    const table = await screen.findByRole("table", { name: "Clients" });
    const first = within(table).getByText("C-000007").closest("tr")!;
    expect(within(first).getByText("Milica Testić")).toBeInTheDocument();
    expect(within(first).getByText("NS")).toBeInTheDocument();
    expect(within(first).getByRole("button", { name: "Anonymise Milica Testić" })).toBeInTheDocument();
    const done = within(table).getByText("C-000008").closest("tr")!;
    expect(within(done).getByText(/Anonymised on 2 November 2026/)).toBeInTheDocument();
    expect(within(done).queryByRole("button")).toBeNull();
  });

  it("searches by name or client number", async () => {
    const calls = stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "GET /api/clients/lookup": { status: 200, body: paged([item()]) },
      "GET /api/clients/lookup?search=C-000007": { status: 200, body: paged([item()]) },
    });
    renderApp("/anonymise");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Name or client number"), "C-000007");
    await user.click(screen.getByRole("button", { name: "Search" }));
    await screen.findByText("C-000007");
    expect(calls.map((c) => c.path)).toContain("/api/clients/lookup?search=C-000007");
  });

  it("anonymises only after the client number is typed to confirm", async () => {
    const calls = stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "GET /api/clients/lookup": { status: 200, body: paged([item()]) },
      "POST /api/clients/c1/anonymise": {
        status: 200,
        body: clientResponse({ fullName: "Anonymised", isActive: false, anonymisedAt: "2026-11-11T09:00:00Z", dateOfBirth: null }),
      },
    });
    renderApp("/anonymise");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Anonymise Milica Testić" }));
    const confirm = screen.getByRole("group", { name: "Anonymise Milica Testić" });
    expect(within(confirm).getByText(/cannot be undone/)).toBeInTheDocument();
    const go = within(confirm).getByRole("button", { name: "Anonymise permanently" });
    expect(go).toBeDisabled();
    await user.type(within(confirm).getByLabelText("Type C-000007 to confirm"), "C-000008");
    expect(go).toBeDisabled();
    await user.clear(within(confirm).getByLabelText("Type C-000007 to confirm"));
    await user.type(within(confirm).getByLabelText("Type C-000007 to confirm"), "c-000007");
    expect(go).toBeEnabled();
    await user.click(go);
    expect(await screen.findByText("Client C-000007 was anonymised.")).toBeInTheDocument();
    const row = screen.getByText("C-000007").closest("tr")!;
    expect(within(row).getByText(/Anonymised on 11 November 2026/)).toBeInTheDocument();
    expect(within(row).queryByRole("button")).toBeNull();
    expect(calls.filter((c) => c.method === "POST").map((c) => c.path)).toEqual(["/api/clients/c1/anonymise"]);
  });

  it("cancels without changing anything", async () => {
    const calls = stubApi({ ...signedInAs("Admin"), ...branches, "GET /api/clients/lookup": { status: 200, body: paged([item()]) } });
    renderApp("/anonymise");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Anonymise Milica Testić" }));
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(screen.queryByRole("group", { name: "Anonymise Milica Testić" })).toBeNull();
    expect(calls.some((c) => c.method === "POST")).toBe(false);
  });

  it("explains when the client was anonymised in the meantime", async () => {
    stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "GET /api/clients/lookup": { status: 200, body: paged([item()]) },
      "POST /api/clients/c1/anonymise": problem(409, "client_anonymised"),
    });
    renderApp("/anonymise");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Anonymise Milica Testić" }));
    await user.type(screen.getByLabelText("Type C-000007 to confirm"), "C-000007");
    await user.click(screen.getByRole("button", { name: "Anonymise permanently" }));
    expect(await screen.findByText("This client was anonymised and cannot be changed.")).toBeInTheDocument();
  });

  it("is not open to other roles", async () => {
    stubApi(signedInAs("Officer"));
    renderApp("/anonymise");
    expect(await screen.findByRole("heading", { name: "No access" })).toBeInTheDocument();
  });
});
