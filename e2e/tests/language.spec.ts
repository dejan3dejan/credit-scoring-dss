import { expect, test } from "@playwright/test";
import { apiAs, signInAs } from "../support/api";
import { runId } from "../support/env";
import { en, sr } from "../support/text";
import { heading, mainNav, signInThroughForm } from "../support/ui";

const userName = `e2e.lang.${runId}`;
const password = `e2e-${runId}-${Date.now()}`;

test.beforeAll(async () => {
  const admin = await apiAs("admin");
  const temporaryPassword = `tmp-${runId}-${Date.now()}`;
  await admin.json("POST", "/api/users", {
    userName,
    displayName: `Probni Jezik ${runId}`,
    email: null,
    role: "Risk",
    preferredLanguage: "en",
    temporaryPassword,
    branchId: null,
  });
  await admin.dispose();
  const user = await signInAs(userName, temporaryPassword);
  const changed = await user.post("/api/auth/password", { currentPassword: temporaryPassword, newPassword: password });
  expect(changed.status()).toBe(204);
  await user.dispose();
});

const rawKey = /\b(app|nav|home|login|list|details|dashboard|fields|enums|errors|fieldErrors|clients|models|audit|states|recommendation)\.[a-zA-Z]+\b/;

test("the language switch turns the app Serbian, is kept on the account, and every page has no raw keys", async ({ page, browser }) => {
  await page.goto("/app/login");
  await signInThroughForm(page, userName, password);
  await expect(heading(page)).toHaveText(en.home.Risk);

  await page.getByRole("button", { name: en.languageButton }).click();
  await expect(heading(page)).toHaveText(sr.home.Risk);
  await expect(mainNav(page, sr.nav.label).getByRole("link")).toHaveText([
    sr.home.Risk,
    sr.nav.applications,
    sr.nav.clients,
    sr.nav.dashboard,
    sr.nav.models,
    sr.nav.audit,
  ]);
  await expect(page.locator("html")).toHaveAttribute("lang", "sr");

  await expect.poll(async () => (await page.request.get("/api/auth/me").then((r) => r.json())).preferredLanguage).toBe("sr");

  await page.reload();
  await expect(heading(page)).toHaveText(sr.home.Risk);

  for (const path of ["/", "/applications", "/clients", "/dashboard", "/models", "/audit"]) {
    await page.goto(`/app${path}`);
    await expect(heading(page), path).toBeVisible();
    await page.waitForLoadState("networkidle");
    const text = await page.locator("body").innerText();
    expect(text, `${path} shows a raw translation key`).not.toMatch(rawKey);
    for (const word of [en.nav.applications, en.nav.clients, en.nav.dashboard, en.nav.audit]) {
      await expect(mainNav(page, sr.nav.label).getByRole("link", { name: word, exact: true }), `${path}: ${word}`).toHaveCount(0);
    }
  }

  const fresh = await browser.newContext();
  const second = await fresh.newPage();
  await second.goto("/app/login");
  await signInThroughForm(second, userName, password);
  await expect(heading(second)).toHaveText(sr.home.Risk);
  await fresh.close();

  await page.goto("/app/");
  await page.getByRole("button", { name: sr.languageButton }).click();
  await expect(heading(page)).toHaveText(en.home.Risk);
  await expect.poll(async () => (await page.request.get("/api/auth/me").then((r) => r.json())).preferredLanguage).toBe("en");
});
