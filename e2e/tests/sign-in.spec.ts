import { expect, test } from "@playwright/test";
import { anonymous, expectProblem } from "../support/api";
import { passwordOf, type DemoUser } from "../support/env";
import { en } from "../support/text";
import { heading, mainNav, signInThroughForm, signedInAs } from "../support/ui";

const landing: [DemoUser, keyof typeof en.home][] = [
  ["officer", "Officer"],
  ["officer.bg", "Officer"],
  ["manager", "Manager"],
  ["risk", "Risk"],
  ["admin", "Admin"],
];

for (const [user, role] of landing) {
  test(`${user} signs in and lands on ${en.home[role]}`, async ({ page }) => {
    await page.goto("/app/login");
    await expect(heading(page)).toHaveText(en.signIn.title);
    await signInThroughForm(page, user, passwordOf(user));
    await expect(page).toHaveURL(/\/app\/?$/);
    await expect(heading(page)).toHaveText(en.home[role]);
    await expect(mainNav(page)).toBeVisible();
  });
}

test("a wrong sign-in shows an alert and keeps the user out", async ({ page }) => {
  await page.goto("/app/login");
  await signInThroughForm(page, "e2e.nobody", "not-a-real-password");
  await expect(page.getByRole("alert")).toHaveText(en.signIn.wrong);
  await expect(page).toHaveURL(/\/app\/login/);
  const me = await page.request.get("/api/auth/me");
  expect(me.status()).toBe(401);
});

test("a deep link without a session goes to sign-in and back", async ({ page }) => {
  await page.goto("/app/applications");
  await expect(page).toHaveURL(/\/app\/login\?redirect=/);
  await signInThroughForm(page, "officer", passwordOf("officer"));
  await expect(page).toHaveURL(/\/app\/applications$/);
  await expect(heading(page)).toHaveText(en.nav.applications);
});

test("the API answers 401 problem without a session", async () => {
  const api = await anonymous();
  await expectProblem(await api.get("/api/applications"), 401, "unauthenticated");
  await expectProblem(await api.get("/api/auth/me"), 401, "unauthenticated");
  await api.dispose();
});

test("signing out ends the session", async ({ page }) => {
  await signedInAs(page, "risk");
  await page.goto("/app/");
  await expect(heading(page)).toHaveText(en.home.Risk);
  await page.getByRole("button", { name: /Jelena Petrović/ }).click();
  await page.getByRole("menuitem", { name: "Log out" }).click();
  await expect(page).toHaveURL(/\/app\/login/);
  expect((await page.request.get("/api/auth/me")).status()).toBe(401);
});
