import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { signedInAs, stubApi } from "@/test/api";
import { clientDetail, clientSummary, paged } from "@/test/fixtures";

describe("clients list", () => {
  it("lists clients with their number, applications and status", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/clients": { status: 200, body: paged([clientSummary()]) } });
    renderApp("/clients");
    const row = (await screen.findByRole("link", { name: "Milica Testić" })).closest("tr")!;
    expect(within(row).getByText("C-000007")).toBeInTheDocument();
    expect(within(row).getByText("Active")).toBeInTheDocument();
  });

  it("shows anonymised clients by label, not by stored name", async () => {
    stubApi({
      ...signedInAs("Risk"),
      "GET /api/clients": {
        status: 200,
        body: paged([clientSummary({ fullName: "Anonymised", isActive: false, anonymisedAt: "2026-11-05T10:00:00Z" })]),
      },
    });
    renderApp("/clients");
    expect(await screen.findByRole("link", { name: "Anonymised client" })).toBeInTheDocument();
    expect(screen.getAllByText("Anonymised").length).toBeGreaterThan(0);
  });

  it("searches by name or number", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      "GET /api/clients": { status: 200, body: paged([clientSummary()]) },
      "GET /api/clients?search=C-000099": { status: 200, body: paged([]) },
    });
    renderApp("/clients");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Search clients"), "C-000099");
    await user.click(screen.getByRole("button", { name: "Search" }));
    expect(await screen.findByText("No results")).toBeInTheDocument();
    expect(calls.map((c) => c.path)).toContain("/api/clients?search=C-000099");
  });

  it("offers a new client only to roles that may create one", async () => {
    stubApi({ ...signedInAs("Risk"), "GET /api/clients": { status: 200, body: paged([clientSummary()]) } });
    renderApp("/clients");
    await screen.findByRole("link", { name: "Milica Testić" });
    expect(screen.queryByRole("link", { name: "New client" })).toBeNull();
  });
});

describe("client profile", () => {
  it("shows the details and the client's applications", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/clients/c1": { status: 200, body: clientDetail() } });
    renderApp("/clients/c1");
    expect(await screen.findByRole("heading", { name: "Milica Testić" })).toBeInTheDocument();
    expect(screen.getByText("Test d.o.o.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "A-000001" })).toBeInTheDocument();
  });

  it("starts a new application from the last one", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/clients/c1": { status: 200, body: clientDetail() } });
    renderApp("/clients/c1");
    expect(await screen.findByRole("link", { name: "Start from last application" })).toHaveAttribute(
      "href",
      "/app/applications/new?clientId=c1&from=a1",
    );
    expect(screen.getByRole("link", { name: "New application" })).toHaveAttribute("href", "/app/applications/new?clientId=c1");
  });
});

describe("new client", () => {
  it("creates the client and continues to the application form", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      "POST /api/clients": { status: 201, body: clientDetail({ id: "c2", clientNumber: "C-000010", applications: [] }) },
      "GET /api/clients/c2": { status: 200, body: clientDetail({ id: "c2", clientNumber: "C-000010", applications: [] }) },
    });
    renderApp("/clients/new");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Full name"), "Petra Nović");
    await user.type(screen.getByLabelText("Date of birth"), "1990-02-03");
    await user.click(screen.getByRole("button", { name: "Create client and continue" }));
    expect(await screen.findByLabelText("Loan amount")).toBeInTheDocument();
    expect(calls.find((c) => c.method === "POST")?.body).toEqual({
      fullName: "Petra Nović",
      dateOfBirth: "1990-02-03",
      address: null,
      phone: null,
      email: null,
      employer: null,
    });
  });
});
