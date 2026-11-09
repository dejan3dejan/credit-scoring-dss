import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { meFor, problem, stubApi } from "@/test/api";

describe("change password page", () => {
  it("shows the reason from the API next to each field", async () => {
    stubApi({
      "GET /api/auth/me": { status: 200, body: meFor("Officer") },
      "POST /api/auth/password": problem(400, "validation_failed", {
        fieldErrors: {
          currentPassword: [{ code: "wrong_password" }],
          newPassword: [{ code: "length", minimum: 10, maximum: 256 }],
        },
      }),
    });
    renderApp("/account/password");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Current password"), "old");
    await user.type(screen.getByLabelText("New password"), "short");
    await user.click(screen.getByRole("button", { name: "Change password" }));
    expect(await screen.findByText("The current password is wrong.")).toBeInTheDocument();
    expect(screen.getByText("Use 10 to 256 characters.")).toBeInTheDocument();
  });

  it("takes the user home after a successful change", async () => {
    let mustChange = true;
    stubApi({
      "GET /api/auth/me": () => ({ status: 200, body: meFor("Officer", { mustChangePassword: mustChange }) }),
      "POST /api/auth/password": () => {
        mustChange = false;
        return { status: 204 };
      },
    });
    renderApp("/");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Current password"), "temporary-password");
    await user.type(screen.getByLabelText("New password"), "a-new-long-password");
    await user.click(screen.getByRole("button", { name: "Change password" }));
    expect(await screen.findByRole("heading", { name: "My work" })).toBeInTheDocument();
  });
});
