import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { apiAs, applicantName, createClient, submitApplication } from "../support/api";
import type { DemoUser } from "../support/env";
import { heading, signedInAs } from "../support/ui";

const wcag = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];

async function expectNoViolations(page: Page) {
  await expect(heading(page)).toBeVisible();
  await page.waitForLoadState("networkidle");
  const { violations } = await new AxeBuilder({ page }).withTags(wcag).analyze();
  const found = violations.map((v) => `${v.id} (${v.impact}): ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`);
  expect(found, "WCAG 2.1 AA violations").toEqual([]);
}

let approveId = "";
let reviewId = "";
let clientId = "";

test.beforeAll(async () => {
  const officer = await apiAs("officer");
  const client = await createClient(officer, applicantName("Pristup"));
  clientId = client.id;
  approveId = (await submitApplication(officer, client.id, "approve")).id;
  const other = await createClient(officer, applicantName("Pristup"));
  reviewId = (await submitApplication(officer, other.id, "review")).id;
  await officer.dispose();
});

test("the sign-in page has no WCAG 2.1 AA violations", async ({ page }) => {
  await page.goto("/app/login");
  await expectNoViolations(page);
});

const pages: [DemoUser, string, () => string][] = [
  ["officer", "home", () => "/app/"],
  ["officer", "applications list", () => "/app/applications"],
  ["officer", "approve application", () => `/app/applications/${approveId}`],
  ["officer", "review application", () => `/app/applications/${reviewId}`],
  ["officer", "new application", () => `/app/applications/new?clientId=${clientId}`],
  ["officer", "clients list", () => "/app/clients"],
  ["officer", "client file", () => `/app/clients/${clientId}`],
  ["officer", "new client", () => "/app/clients/new"],
  ["manager", "dashboard", () => "/app/dashboard"],
  ["risk", "models", () => "/app/models"],
  ["admin", "users", () => "/app/users"],
  ["admin", "branches", () => "/app/branches"],
  ["admin", "audit log", () => "/app/audit"],
  ["admin", "anonymise", () => "/app/anonymise"],
];

for (const [user, label, path] of pages) {
  test(`${user}: ${label} has no WCAG 2.1 AA violations`, async ({ page }) => {
    await signedInAs(page, user);
    await page.goto(path());
    await expectNoViolations(page);
  });
}
