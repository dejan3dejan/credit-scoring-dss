import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { meFor, signedInAs, stubApi, unauthenticated } from "@/test/api";

function navLinks() {
  const nav = screen.getByRole("navigation", { name: "Main" });
  return within(nav)
    .getAllByRole("link")
    .map((link) => link.textContent);
}

describe("signed-out user", () => {
  it("is sent to the login page from any page", async () => {
    stubApi({ "GET /api/auth/me": unauthenticated });
    renderApp("/applications");
    expect(await screen.findByRole("button", { name: "Sign in" })).toBeInTheDocument();
  });
});

describe("navigation by permission", () => {
  it("shows an officer the work pages but not the dashboard or administration", async () => {
    stubApi(signedInAs("Officer"));
    renderApp("/");
    await screen.findByRole("navigation", { name: "Main" });
    expect(navLinks()).toEqual(["My work", "Applications", "Clients", "Models"]);
  });

  it("shows a manager the dashboard as well", async () => {
    stubApi(signedInAs("Manager"));
    renderApp("/");
    await screen.findByRole("navigation", { name: "Main" });
    expect(navLinks()).toEqual(["My work", "Applications", "Clients", "Dashboard", "Models"]);
  });

  it("shows risk the dashboard, models and the audit log", async () => {
    stubApi(signedInAs("Risk"));
    renderApp("/");
    await screen.findByRole("navigation", { name: "Main" });
    expect(navLinks()).toEqual(["Overview", "Applications", "Clients", "Dashboard", "Models", "Audit log"]);
  });

  it("shows an admin only administration pages", async () => {
    stubApi(signedInAs("Admin"));
    renderApp("/");
    await screen.findByRole("navigation", { name: "Main" });
    expect(navLinks()).toEqual(["Administration", "Users", "Branches", "Audit log"]);
  });

  it("offers a new application only to roles that may submit one", async () => {
    stubApi(signedInAs("Officer"));
    const officer = renderApp("/");
    expect(await screen.findByRole("link", { name: "New application" })).toBeInTheDocument();
    officer.unmount();

    stubApi(signedInAs("Risk"));
    renderApp("/");
    await screen.findByRole("navigation", { name: "Main" });
    expect(screen.queryByRole("link", { name: "New application" })).toBeNull();
  });
});

describe("direct links", () => {
  it("show the no-access page when the role lacks the permission", async () => {
    stubApi(signedInAs("Officer"));
    renderApp("/dashboard");
    expect(await screen.findByRole("heading", { name: "No access" })).toBeInTheDocument();
  });

  it("show the not-found page for an unknown address", async () => {
    stubApi(signedInAs("Officer"));
    renderApp("/no-such-page");
    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeInTheDocument();
  });
});

describe("user", () => {
  it("is shown with name, role and branch", async () => {
    stubApi(signedInAs("Officer"));
    renderApp("/");
    expect(await screen.findByText("Mila Testić")).toBeInTheDocument();
    expect(screen.getByText("Officer · Novi Sad")).toBeInTheDocument();
  });

  it("must change the password first when an admin set it", async () => {
    stubApi(signedInAs("Officer", { mustChangePassword: true }));
    renderApp("/applications");
    expect(await screen.findByRole("heading", { name: "Change password" })).toBeInTheDocument();
  });

  it("sees the app in the language saved on the account", async () => {
    stubApi(signedInAs("Officer", { preferredLanguage: "sr" }));
    renderApp("/");
    expect(await screen.findByRole("link", { name: "Klijenti" })).toBeInTheDocument();
  });

  it("keeps the account language for pages shown before the next sign-in check", async () => {
    stubApi(signedInAs("Officer", { preferredLanguage: "sr" }));
    renderApp("/");
    await screen.findByRole("link", { name: "Klijenti" });
    expect(localStorage.getItem("language")).toBe("sr");
  });

  it("switches language and saves it on the account", async () => {
    let language: "en" | "sr" = "en";
    const calls = stubApi({
      "GET /api/auth/me": () => ({ status: 200, body: meFor("Officer", { preferredLanguage: language }) }),
      "PUT /api/auth/me/language": (body) => {
        language = (body as { language: "en" | "sr" }).language;
        return { status: 200, body: meFor("Officer", { preferredLanguage: language }) };
      },
    });
    renderApp("/");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Language: English" }));
    expect(await screen.findByRole("link", { name: "Klijenti" })).toBeInTheDocument();
    expect(calls.find((c) => c.method === "PUT")?.body).toEqual({ language: "sr" });
  });

  it("uses one language button that flips between English and Serbian", async () => {
    let language: "en" | "sr" = "en";
    const calls = stubApi({
      "GET /api/auth/me": () => ({ status: 200, body: meFor("Officer", { preferredLanguage: language }) }),
      "PUT /api/auth/me/language": (body) => {
        language = (body as { language: "en" | "sr" }).language;
        return { status: 200, body: meFor("Officer", { preferredLanguage: language }) };
      },
    });
    renderApp("/");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Language: English" }));
    await user.click(await screen.findByRole("button", { name: "Jezik: srpski" }));
    expect(await screen.findByRole("link", { name: "Clients" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "SR" })).toBeNull();
    expect(calls.filter((c) => c.method === "PUT").map((c) => c.body)).toEqual([{ language: "sr" }, { language: "en" }]);
  });

  it("logs out and returns to the login page", async () => {
    let signedIn = true;
    const calls = stubApi({
      "GET /api/auth/me": () => (signedIn ? { status: 200, body: meFor("Officer") } : unauthenticated),
      "POST /api/auth/logout": () => {
        signedIn = false;
        return { status: 204 };
      },
    });
    renderApp("/");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: /Mila Testić/ }));
    await user.click(await screen.findByRole("menuitem", { name: "Log out" }));
    expect(await screen.findByRole("button", { name: "Sign in" })).toBeInTheDocument();
    expect(calls.some((c) => c.method === "POST" && c.path === "/api/auth/logout")).toBe(true);
  });
});
