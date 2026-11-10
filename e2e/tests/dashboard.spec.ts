import { expect, test } from "@playwright/test";
import { apiAs, applicantName, createClient, submitApplication } from "../support/api";
import { en } from "../support/text";
import { heading, signedInAs } from "../support/ui";

test.beforeAll(async () => {
  const officer = await apiAs("officer");
  const client = await createClient(officer, applicantName("Tabla"));
  await submitApplication(officer, client.id, "approve");
  await officer.dispose();
});

for (const user of ["manager", "risk"] as const) {
  test(`${user} opens the dashboard with numbers and charts`, async ({ page }) => {
    await signedInAs(page, user);
    await page.goto("/app/dashboard");
    await expect(heading(page)).toHaveText(en.dashboard.title);
    await expect(page.getByText(en.dashboard.applications, { exact: true }).first()).toBeVisible();
    await expect(page.getByText(en.dashboard.decided, { exact: true }).first()).toBeVisible();
    await expect(page.locator("main svg.recharts-surface").first()).toBeVisible();
    await expect(page.getByRole("alert")).toHaveCount(0);

    const api = await apiAs(user);
    const stats = await api.json("GET", "/api/stats");
    expect(stats.applications).toBeGreaterThan(0);
    await api.dispose();
  });
}
