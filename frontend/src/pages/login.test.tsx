import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { meFor, problem, stubApi, unauthenticated } from "@/test/api";

async function signIn(userName: string, password: string) {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText("User name"), userName);
  await user.type(screen.getByLabelText("Password"), password);
  await user.click(screen.getByRole("button", { name: "Sign in" }));
}

describe("login page", () => {
  it("tells the user the name or password is wrong without saying which", async () => {
    stubApi({ "GET /api/auth/me": unauthenticated, "POST /api/auth/login": problem(401, "invalid_credentials") });
    renderApp("/login");
    await signIn("officer", "wrong-password");
    expect(await screen.findByRole("alert")).toHaveTextContent("Wrong user name or password.");
  });

  it("tells a locked-out user when to try again", async () => {
    stubApi({
      "GET /api/auth/me": unauthenticated,
      "POST /api/auth/login": problem(401, "locked_out", { lockedUntil: "2026-11-09T21:15:00Z" }),
    });
    renderApp("/login");
    await signIn("officer", "wrong-password");
    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Too many wrong passwords.");
    expect(alert).toHaveTextContent("9 November 2026");
  });

  it("shows translated field errors from the API next to the field", async () => {
    stubApi({
      "GET /api/auth/me": unauthenticated,
      "POST /api/auth/login": problem(400, "validation_failed", {
        fieldErrors: { password: [{ code: "length", minimum: 0, maximum: 256 }] },
      }),
    });
    renderApp("/login");
    await signIn("officer", "x");
    expect(await screen.findByText("Use at most 256 characters.")).toBeInTheDocument();
    expect(screen.getByLabelText("Password")).toHaveAttribute("aria-invalid", "true");
  });

  it("sends the typed name and password and opens the page the user asked for", async () => {
    let signedIn = false;
    const calls = stubApi({
      "GET /api/auth/me": () => (signedIn ? { status: 200, body: meFor("Officer") } : unauthenticated),
      "POST /api/auth/login": () => {
        signedIn = true;
        return { status: 200, body: meFor("Officer") };
      },
    });
    renderApp("/clients");
    await signIn("officer", "the-password");
    expect(calls.find((c) => c.method === "POST")?.body).toEqual({ userName: "officer", password: "the-password" });
    expect(await screen.findByRole("heading", { name: "Clients" })).toBeInTheDocument();
  });

  it("switches the login page to Serbian", async () => {
    stubApi({ "GET /api/auth/me": unauthenticated });
    renderApp("/login");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Language: English" }));
    expect(await screen.findByLabelText("Korisničko ime")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Prijavi se" })).toBeInTheDocument();
  });
});
