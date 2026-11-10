import { expect, test } from "@playwright/test";
import { apiAs, expectProblem } from "../support/api";
import type { DemoUser } from "../support/env";
import { en } from "../support/text";
import { heading, mainNav, signedInAs } from "../support/ui";

type Role = "Officer" | "Manager" | "Risk" | "Admin";

const userOf: Record<Role, DemoUser> = { Officer: "officer", Manager: "manager", Risk: "risk", Admin: "admin" };

const navOf: Record<Role, string[]> = {
  Officer: [en.home.Officer, en.nav.applications, en.nav.clients, en.nav.models],
  Manager: [en.home.Manager, en.nav.applications, en.nav.clients, en.nav.dashboard, en.nav.models],
  Risk: [en.home.Risk, en.nav.applications, en.nav.clients, en.nav.dashboard, en.nav.models, en.nav.audit],
  Admin: [en.home.Admin, en.nav.users, en.nav.branches, en.nav.audit],
};

const refusedPages: Record<Role, string[]> = {
  Officer: ["/dashboard", "/users", "/users/new", "/branches", "/audit"],
  Manager: ["/users", "/users/new", "/branches", "/audit"],
  Risk: ["/users", "/branches", "/applications/new", "/clients/new"],
  Admin: ["/applications", "/applications/new", "/clients", "/clients/new", "/dashboard", "/models"],
};

const allowedPages: Record<Role, [string, string][]> = {
  Officer: [["/applications", en.nav.applications], ["/clients", en.nav.clients], ["/applications/new", "New application"]],
  Manager: [["/dashboard", en.dashboard.title], ["/applications", en.nav.applications]],
  Risk: [["/dashboard", en.dashboard.title], ["/audit", en.nav.audit], ["/applications", en.nav.applications]],
  Admin: [["/users", en.nav.users], ["/branches", en.nav.branches], ["/audit", en.nav.audit]],
};

for (const role of Object.keys(userOf) as Role[]) {
  test.describe(role, () => {
    test("navigation shows exactly what the role may use", async ({ page }) => {
      await signedInAs(page, userOf[role]);
      await page.goto("/app/");
      await expect(mainNav(page).getByRole("link")).toHaveText(navOf[role]);
    });

    test("direct URLs the role may not use show the no-access page", async ({ page }) => {
      await signedInAs(page, userOf[role]);
      for (const path of refusedPages[role]) {
        await page.goto(`/app${path}`);
        await expect(page, path).toHaveURL(/\/app\/no-access$/);
        await expect(heading(page), path).toHaveText(en.noAccess);
      }
    });

    test("pages the role may use open", async ({ page }) => {
      await signedInAs(page, userOf[role]);
      for (const [path, title] of allowedPages[role]) {
        await page.goto(`/app${path}`);
        await expect(heading(page), path).toHaveText(title);
        await expect(page).not.toHaveURL(/no-access/);
      }
    });
  });
}

const unknownId = "00000000-0000-0000-0000-000000000001";

const endpoints: { permission: string; method: "GET" | "POST"; path: string; allowed: Role[] }[] = [
  { permission: "users.manage", method: "GET", path: "/api/users", allowed: ["Admin"] },
  { permission: "branches.manage", method: "POST", path: "/api/branches", allowed: ["Admin"] },
  { permission: "audit.read", method: "GET", path: "/api/audit", allowed: ["Admin", "Risk"] },
  { permission: "clients.read", method: "GET", path: "/api/clients", allowed: ["Officer", "Manager", "Risk"] },
  { permission: "clients.write", method: "POST", path: "/api/clients", allowed: ["Officer", "Manager"] },
  { permission: "clients.anonymise", method: "POST", path: `/api/clients/${unknownId}/anonymise`, allowed: ["Admin"] },
  { permission: "applications.read", method: "GET", path: "/api/applications", allowed: ["Officer", "Manager", "Risk"] },
  { permission: "applications.write", method: "POST", path: "/api/applications", allowed: ["Officer", "Manager"] },
  { permission: "applications.decide", method: "POST", path: `/api/applications/${unknownId}/decision`, allowed: ["Officer", "Manager"] },
  { permission: "applications.assign", method: "GET", path: "/api/work/team", allowed: ["Manager"] },
  { permission: "applications.approve-escalated", method: "POST", path: `/api/applications/${unknownId}/sign-off`, allowed: ["Manager"] },
  { permission: "outcomes.record", method: "POST", path: `/api/applications/${unknownId}/outcome`, allowed: ["Risk"] },
  { permission: "stats.read", method: "GET", path: "/api/stats", allowed: ["Manager", "Risk"] },
  { permission: "models.read", method: "GET", path: "/api/models", allowed: ["Officer", "Manager", "Risk"] },
  { permission: "models.manage", method: "GET", path: "/api/models/events", allowed: ["Risk"] },
];

for (const role of Object.keys(userOf) as Role[]) {
  test(`API permission matrix for ${role}`, async () => {
    const api = await apiAs(userOf[role]);
    for (const endpoint of endpoints) {
      const response = endpoint.method === "GET" ? await api.get(endpoint.path) : await api.post(endpoint.path, {});
      await test.step(`${endpoint.method} ${endpoint.path} (${endpoint.permission})`, async () => {
        if (endpoint.allowed.includes(role)) {
          expect([401, 403], `${role} ${endpoint.method} ${endpoint.path}`).not.toContain(response.status());
        } else {
          await expectProblem(response, 403, "forbidden");
        }
      });
    }
    await api.dispose();
  });
}
