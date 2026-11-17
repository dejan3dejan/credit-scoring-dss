import { expect, test } from "@playwright/test";
import { apiAs, expectProblem, signInAs } from "../support/api";
import { runId } from "../support/env";

test("a user with a temporary password can use only the sign-in endpoints until it is changed", async () => {
  const userName = `e2e.pwd.${runId}`;
  const temporaryPassword = `tmp-${runId}-${Date.now()}`;
  const admin = await apiAs("admin");
  await admin.json("POST", "/api/users", {
    userName,
    displayName: `Probna Lozinka ${runId}`,
    email: null,
    role: "Risk",
    preferredLanguage: "en",
    temporaryPassword,
    branchId: null,
  });
  await admin.dispose();

  const user = await signInAs(userName, temporaryPassword);
  await expectProblem(await user.get("/api/applications"), 403, "password_change_required");
  expect((await user.get("/api/auth/me")).status()).toBe(200);

  const changed = await user.post("/api/auth/password", { currentPassword: temporaryPassword, newPassword: `e2e-${runId}-${Date.now()}` });
  expect(changed.status()).toBe(204);
  expect((await user.get("/api/applications")).status()).toBe(200);
  await user.dispose();
});
