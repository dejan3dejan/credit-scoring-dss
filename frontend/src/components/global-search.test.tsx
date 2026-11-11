import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { signedInAs, stubApi } from "@/test/api";
import { application, clientDetail, clientSummary, summary } from "@/test/fixtures";

const found = {
  "GET /api/search?q=Mil": {
    status: 200,
    body: { clients: [clientSummary()], applications: [summary({ id: "a1", reference: "A-000001", applicantName: "Milica Testić" })] },
  },
};

describe("global search", () => {
  it("is in the top bar for roles that may read clients, not for Admin", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/work/mine*": { status: 404 } });
    const officer = renderApp("/clients");
    expect(await screen.findByRole("combobox", { name: "Search clients and applications" })).toBeInTheDocument();
    officer.unmount();

    stubApi(signedInAs("Admin"));
    renderApp("/users");
    await screen.findByRole("navigation", { name: "Main" });
    expect(screen.queryByRole("combobox", { name: "Search clients and applications" })).toBeNull();
  });

  it("finds clients by name and applications by reference", async () => {
    const calls = stubApi({ ...signedInAs("Officer"), ...found });
    renderApp("/clients");
    const user = userEvent.setup();
    await user.type(await screen.findByRole("combobox", { name: "Search clients and applications" }), "Mil");
    const list = await screen.findByRole("listbox", { name: "Search results" });
    expect(within(list).getByRole("option", { name: /Milica Testić.*C-000007/ })).toBeInTheDocument();
    expect(within(list).getByRole("option", { name: /A-000001.*Milica Testić/ })).toBeInTheDocument();
    expect(calls.filter((c) => c.path.startsWith("/api/search")).map((c) => c.path)).toEqual(["/api/search?q=Mil"]);
  });

  it("waits for two characters before asking the server", async () => {
    const calls = stubApi(signedInAs("Officer"));
    renderApp("/clients");
    const user = userEvent.setup();
    await user.type(await screen.findByRole("combobox", { name: "Search clients and applications" }), "M");
    await new Promise((resolve) => setTimeout(resolve, 400));
    expect(calls.some((c) => c.path.startsWith("/api/search"))).toBe(false);
    expect(screen.queryByRole("listbox")).toBeNull();
  });

  it("opens the chosen result with the keyboard", async () => {
    stubApi({ ...signedInAs("Officer"), ...found, "GET /api/clients/c1": { status: 200, body: clientDetail() } });
    renderApp("/applications/new");
    const user = userEvent.setup();
    const box = await screen.findByRole("combobox", { name: "Search clients and applications" });
    await user.type(box, "Mil");
    await screen.findByRole("listbox", { name: "Search results" });
    await user.keyboard("{ArrowDown}");
    expect(box).toHaveAttribute("aria-activedescendant", screen.getByRole("option", { name: /Milica Testić.*C-000007/ }).id);
    await user.keyboard("{Enter}");
    expect(await screen.findByRole("heading", { name: "Milica Testić" })).toBeInTheDocument();
    expect(box).toHaveValue("");
    expect(screen.queryByRole("listbox")).toBeNull();
  });

  it("opens an application with a click", async () => {
    stubApi({
      ...signedInAs("Officer"),
      ...found,
      "GET /api/applications/a1": { status: 200, body: application({ id: "a1" }) },
      "GET /api/applications/a1/notes": { status: 200, body: [] },
    });
    const view = renderApp("/clients");
    const user = userEvent.setup();
    await user.type(await screen.findByRole("combobox", { name: "Search clients and applications" }), "Mil");
    await user.click(await screen.findByRole("option", { name: /A-000001/ }));
    await screen.findByRole("heading", { name: "Milica Testić" });
    expect(view.router.state.location.pathname).toBe("/applications/a1");
  });

  it("says when nothing matches and closes with Escape", async () => {
    stubApi({ ...signedInAs("Officer"), "GET /api/search?q=zz": { status: 200, body: { clients: [], applications: [] } } });
    renderApp("/clients");
    const user = userEvent.setup();
    await user.type(await screen.findByRole("combobox", { name: "Search clients and applications" }), "zz");
    expect(await screen.findByText("No matches")).toBeInTheDocument();
    await user.keyboard("{Escape}");
    expect(screen.queryByText("No matches")).toBeNull();
  });

  it("jumps to the search box with the slash key", async () => {
    stubApi(signedInAs("Officer"));
    renderApp("/clients");
    const box = await screen.findByRole("combobox", { name: "Search clients and applications" });
    const user = userEvent.setup();
    await user.keyboard("/");
    expect(box).toHaveFocus();
    expect(box).toHaveValue("");
  });
});
