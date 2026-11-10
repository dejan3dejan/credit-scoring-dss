import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { problem, signedInAs, stubApi } from "@/test/api";
import type { UserResponse } from "@/api/users";
import type { Branch } from "@/api/branches";

const novi: Branch = { id: "b1", code: "NS", name: "Novi Sad", city: "Novi Sad", isActive: true, createdAt: "2026-01-01T00:00:00Z", version: 2 };
const old: Branch = { id: "b2", code: "SU", name: "Subotica", city: null, isActive: false, createdAt: "2026-01-01T00:00:00Z", version: 1 };

function user(overrides: Partial<UserResponse> = {}): UserResponse {
  return {
    id: "u1",
    userName: "mila",
    displayName: "Mila Testić",
    email: null,
    role: "Officer",
    branch: { id: "b1", code: "NS", name: "Novi Sad" },
    isActive: true,
    preferredLanguage: "en",
    mustChangePassword: false,
    createdAt: "2026-10-01T08:00:00Z",
    lastSignedInAt: "2026-11-09T08:00:00Z",
    lockedUntil: null,
    version: 5,
    ...overrides,
  };
}

const branches = { "GET /api/branches": { status: 200, body: [novi, old] } };

describe("users", () => {
  it("lists users with role, branch and whether they are locked", async () => {
    stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "GET /api/users": { status: 200, body: [user(), user({ id: "u2", userName: "vera", displayName: "Vera Testić", role: "Risk", branch: null, lockedUntil: "2026-11-10T12:00:00Z" })] },
    });
    renderApp("/users");
    const row = (await screen.findByRole("link", { name: "Vera Testić" })).closest("tr")!;
    expect(within(row).getByText("Risk")).toBeInTheDocument();
    expect(within(row).getByText(/Locked until/)).toBeInTheDocument();
    expect(within(screen.getByRole("link", { name: "Mila Testić" }).closest("tr")!).getByText("Novi Sad")).toBeInTheDocument();
  });

  it("filters the list by name", async () => {
    stubApi({ ...signedInAs("Admin"), ...branches, "GET /api/users": { status: 200, body: [user(), user({ id: "u2", displayName: "Vera Testić", userName: "vera" })] } });
    renderApp("/users");
    const userEvents = userEvent.setup();
    await userEvents.type(await screen.findByLabelText("Filter users"), "vera");
    expect(screen.queryByRole("link", { name: "Mila Testić" })).toBeNull();
    expect(screen.getByRole("link", { name: "Vera Testić" })).toBeInTheDocument();
  });

  it("creates an officer in a branch with a temporary password", async () => {
    const calls = stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "POST /api/users": { status: 201, body: user({ id: "u3", userName: "petar", displayName: "Petar Novi", mustChangePassword: true }) },
      "GET /api/users/u3": { status: 200, body: user({ id: "u3", userName: "petar", displayName: "Petar Novi", mustChangePassword: true }) },
    });
    renderApp("/users/new");
    const userEvents = userEvent.setup();
    await userEvents.type(await screen.findByLabelText("User name"), "petar");
    await userEvents.type(screen.getByLabelText("Display name"), "Petar Novi");
    await userEvents.selectOptions(screen.getByLabelText("Role"), "Officer");
    await userEvents.selectOptions(screen.getByLabelText("Branch"), "Novi Sad");
    await userEvents.type(screen.getByLabelText("Temporary password"), "temporary-123");
    await userEvents.click(screen.getByRole("button", { name: "Create user" }));
    expect(await screen.findByRole("heading", { name: "Petar Novi" })).toBeInTheDocument();
    expect(calls.find((c) => c.path === "/api/users" && c.method === "POST")?.body).toEqual({
      userName: "petar",
      displayName: "Petar Novi",
      email: null,
      role: "Officer",
      branchId: "b1",
      preferredLanguage: "en",
      temporaryPassword: "temporary-123",
    });
    expect(screen.queryByRole("option", { name: "Subotica" })).toBeNull();
  });

  it("gives Risk and Admin users no branch", async () => {
    stubApi({ ...signedInAs("Admin"), ...branches, "GET /api/users/u1": { status: 200, body: user() } });
    renderApp("/users/u1");
    const userEvents = userEvent.setup();
    await userEvents.selectOptions(await screen.findByLabelText("Role"), "Risk");
    expect(screen.getByLabelText("Branch")).toBeDisabled();
  });

  it("saves an edited user and shows the API's field errors", async () => {
    const calls = stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "GET /api/users/u1": { status: 200, body: user() },
      "PUT /api/users/u1": problem(400, "validation_failed", { fieldErrors: { role: [{ code: "own_role" }] } }),
    });
    renderApp("/users/u1");
    const userEvents = userEvent.setup();
    await userEvents.selectOptions(await screen.findByLabelText("Role"), "Manager");
    await userEvents.click(screen.getByRole("button", { name: "Save changes" }));
    expect(await screen.findByText("You cannot change your own role.")).toBeInTheDocument();
    expect(calls.find((c) => c.method === "PUT")?.body).toEqual({
      displayName: "Mila Testić",
      email: null,
      role: "Manager",
      branchId: "b1",
      preferredLanguage: "en",
      version: 5,
    });
  });

  it("reloads a user that someone else changed meanwhile", async () => {
    let version = 5;
    stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "GET /api/users/u1": () => ({ status: 200, body: user({ version, displayName: version === 6 ? "Mila Novaković" : "Mila Testić" }) }),
      "PUT /api/users/u1": () => {
        version = 6;
        return problem(409, "edited_elsewhere");
      },
    });
    renderApp("/users/u1");
    const userEvents = userEvent.setup();
    await userEvents.selectOptions(await screen.findByLabelText("Role"), "Manager");
    await userEvents.click(screen.getByRole("button", { name: "Save changes" }));
    expect(await screen.findByText("Someone else changed this in the meantime. The newer values are shown; make your change again.")).toBeInTheDocument();
    expect(await screen.findByDisplayValue("Mila Novaković")).toBeInTheDocument();
    expect(screen.getByLabelText("Role")).toHaveValue("Officer");
  });

  it("sets a temporary password", async () => {
    const calls = stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "GET /api/users/u1": { status: 200, body: user() },
      "POST /api/users/u1/password": { status: 204 },
    });
    renderApp("/users/u1");
    const userEvents = userEvent.setup();
    const card = (await screen.findByRole("heading", { name: "Password" })).closest("section")!;
    await userEvents.type(within(card).getByLabelText("Temporary password"), "temporary-456");
    await userEvents.click(within(card).getByRole("button", { name: "Set temporary password" }));
    expect(await screen.findByText("Temporary password set.")).toBeInTheDocument();
    expect(calls.find((c) => c.path === "/api/users/u1/password")?.body).toEqual({ temporaryPassword: "temporary-456" });
  });

  it("explains why an admin cannot deactivate themselves", async () => {
    const me = "00000000-0000-0000-0000-000000000000";
    stubApi({
      ...signedInAs("Admin"),
      ...branches,
      [`GET /api/users/${me}`]: { status: 200, body: user({ id: me, role: "Admin", branch: null }) },
      [`POST /api/users/${me}/deactivate`]: problem(409, "own_account"),
    });
    renderApp(`/users/${me}`);
    const userEvents = userEvent.setup();
    await userEvents.click(await screen.findByRole("button", { name: "Deactivate user" }));
    await userEvents.click(screen.getByRole("button", { name: "Deactivate" }));
    expect(await screen.findByText("You cannot deactivate your own account.")).toBeInTheDocument();
  });
});

describe("branches", () => {
  it("edits a branch with the version it was read at", async () => {
    const calls = stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "PUT /api/branches/b1": { status: 200, body: { ...novi, name: "Novi Sad centar", version: 3 } },
    });
    renderApp("/branches");
    const row = (await screen.findByText("NS")).closest("tr")!;
    const userEvents = userEvent.setup();
    await userEvents.click(within(row).getByRole("button", { name: "Edit" }));
    const name = screen.getByLabelText("Name", { selector: "tr input" });
    await userEvents.clear(name);
    await userEvents.type(name, "Novi Sad centar");
    await userEvents.click(within(row).getByRole("button", { name: "Save" }));
    expect(await screen.findByText("Novi Sad centar")).toBeInTheDocument();
    expect(calls.find((c) => c.method === "PUT")?.body).toEqual({ name: "Novi Sad centar", city: "Novi Sad", version: 2 });
  });

  it("reloads the branches when someone else changed the branch meanwhile", async () => {
    let changed = false;
    stubApi({
      ...signedInAs("Admin"),
      "GET /api/branches": () => ({ status: 200, body: [changed ? { ...novi, name: "Novi Sad 2", version: 3 } : novi, old] }),
      "PUT /api/branches/b1": () => {
        changed = true;
        return problem(409, "edited_elsewhere");
      },
    });
    renderApp("/branches");
    const row = (await screen.findByText("NS")).closest("tr")!;
    const userEvents = userEvent.setup();
    await userEvents.click(within(row).getByRole("button", { name: "Edit" }));
    await userEvents.click(within(row).getByRole("button", { name: "Save" }));
    expect(await screen.findByText("Someone else changed this in the meantime. The newer values are shown; make your change again.")).toBeInTheDocument();
    expect(await screen.findByText("Novi Sad 2")).toBeInTheDocument();
  });

  it("lists branches and creates one", async () => {
    const calls = stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "POST /api/branches": { status: 201, body: { ...novi, id: "b3", code: "KG", name: "Kragujevac", city: "Kragujevac" } },
    });
    renderApp("/branches");
    expect(await screen.findByText("Subotica")).toBeInTheDocument();
    const userEvents = userEvent.setup();
    await userEvents.type(screen.getByLabelText("Code"), "kg");
    await userEvents.type(screen.getByLabelText("Name"), "Kragujevac");
    await userEvents.type(screen.getByLabelText("City"), "Kragujevac");
    await userEvents.click(screen.getByRole("button", { name: "Create branch" }));
    expect(await screen.findByText("Branch KG was created.")).toBeInTheDocument();
    expect(calls.find((c) => c.method === "POST")?.body).toEqual({ code: "kg", name: "Kragujevac", city: "Kragujevac" });
  });

  it("explains why a branch with active users cannot be deactivated", async () => {
    stubApi({ ...signedInAs("Admin"), ...branches, "POST /api/branches/b1/deactivate": problem(409, "branch_has_active_users") });
    renderApp("/branches");
    const row = (await screen.findByText("NS")).closest("tr")!;
    const userEvents = userEvent.setup();
    await userEvents.click(within(row).getByRole("button", { name: "Deactivate" }));
    expect(await screen.findByText("This branch still has active users. Move or deactivate them first.")).toBeInTheDocument();
  });
});

describe("admin home", () => {
  it("counts users, locked accounts and active branches", async () => {
    stubApi({
      ...signedInAs("Admin"),
      ...branches,
      "GET /api/users": { status: 200, body: [user(), user({ id: "u2", lockedUntil: "2026-11-10T12:00:00Z", mustChangePassword: true })] },
    });
    renderApp("/");
    const tiles = await screen.findByRole("list", { name: "Administration" });
    expect(within(tiles).getByText("Users").nextSibling).toHaveTextContent("2");
    expect(within(tiles).getByText("Locked now").nextSibling).toHaveTextContent("1");
    expect(within(tiles).getByText("Active branches").nextSibling).toHaveTextContent("1");
  });
});
