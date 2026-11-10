import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { problem, signedInAs, stubApi } from "@/test/api";
import { clientDetail, clientResponse as response } from "@/test/fixtures";

const clientResponse = response();

describe("client management", () => {
  it("edits the client's details", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      "GET /api/clients/c1": { status: 200, body: clientDetail() },
      "PUT /api/clients/c1": { status: 200, body: { ...clientResponse, phone: "+381 64 111 222" } },
    });
    renderApp("/clients/c1");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Edit details" }));
    const phone = screen.getByLabelText("Phone");
    await user.clear(phone);
    await user.type(phone, "+381 64 111 222");
    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(await screen.findByText("+381 64 111 222")).toBeInTheDocument();
    expect(calls.find((c) => c.method === "PUT")?.body).toEqual({
      fullName: "Milica Testić",
      dateOfBirth: "1988-04-12",
      address: "Bulevar oslobođenja 1, Novi Sad",
      phone: "+381 64 111 222",
      email: null,
      employer: "Test d.o.o.",
    });
  });

  it("shows the API's field errors while editing", async () => {
    stubApi({
      ...signedInAs("Officer"),
      "GET /api/clients/c1": { status: 200, body: clientDetail() },
      "PUT /api/clients/c1": problem(400, "validation_failed", { fieldErrors: { phone: [{ code: "format" }] } }),
    });
    renderApp("/clients/c1");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Edit details" }));
    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(await screen.findByText("Use only the allowed characters.")).toBeInTheDocument();
  });

  it("deactivates a client after a confirmation and stops new applications", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      "GET /api/clients/c1": { status: 200, body: clientDetail() },
      "POST /api/clients/c1/deactivate": { status: 200, body: { ...clientResponse, isActive: false } },
    });
    renderApp("/clients/c1");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Deactivate client" }));
    await user.click(screen.getByRole("button", { name: "Deactivate" }));
    expect(await screen.findByRole("button", { name: "Reactivate client" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "New application" })).toBeNull();
    expect(calls.some((c) => c.path === "/api/clients/c1/deactivate")).toBe(true);
  });

  it("reactivates a deactivated client", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      "GET /api/clients/c1": { status: 200, body: clientDetail({ isActive: false }) },
      "POST /api/clients/c1/reactivate": { status: 200, body: clientResponse },
    });
    renderApp("/clients/c1");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Reactivate client" }));
    expect(await screen.findByRole("link", { name: "New application" })).toBeInTheDocument();
    expect(calls.some((c) => c.path === "/api/clients/c1/reactivate")).toBe(true);
  });

  it("is read-only for risk staff and for anonymised clients", async () => {
    stubApi({ ...signedInAs("Risk"), "GET /api/clients/c1": { status: 200, body: clientDetail() } });
    const risk = renderApp("/clients/c1");
    await screen.findByRole("heading", { name: "Milica Testić" });
    expect(screen.queryByRole("button", { name: "Edit details" })).toBeNull();
    risk.unmount();

    stubApi({
      ...signedInAs("Officer"),
      "GET /api/clients/c1": { status: 200, body: clientDetail({ fullName: "Anonymised", isActive: false, anonymisedAt: "2026-11-05T10:00:00Z" }) },
    });
    renderApp("/clients/c1");
    await screen.findByRole("heading", { name: "Anonymised client" });
    expect(screen.queryByRole("button", { name: "Edit details" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Reactivate client" })).toBeNull();
  });
});
