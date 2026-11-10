import { expect, test } from "@playwright/test";
import { apiAs, applicantName, createClient, expectProblem, submitApplication } from "../support/api";
import { en } from "../support/text";
import { signedInAs } from "../support/ui";

let clientId: string;
let applicationId: string;
let name: string;

test.beforeAll(async () => {
  const officer = await apiAs("officer");
  name = applicantName("Filijala");
  clientId = (await createClient(officer, name)).id;
  applicationId = (await submitApplication(officer, clientId, "approve")).id;
  await officer.dispose();
});

test("an officer of another branch gets 404 for the record, like an unknown id", async () => {
  const other = await apiAs("officer.bg");
  await expectProblem(await other.get(`/api/clients/${clientId}`), 404, "not_found");
  await expectProblem(await other.get(`/api/applications/${applicationId}`), 404, "not_found");
  await expectProblem(await other.get("/api/applications/00000000-0000-0000-0000-000000000001"), 404, "not_found");
  await other.dispose();
});

test("the record's own branch and Risk can read it", async () => {
  for (const user of ["officer", "manager", "risk"] as const) {
    const api = await apiAs(user);
    expect((await api.get(`/api/applications/${applicationId}`)).status(), user).toBe(200);
    expect((await api.get(`/api/clients/${clientId}`)).status(), user).toBe(200);
    await api.dispose();
  }
});

test("lists and search of another branch do not show the record", async () => {
  const other = await apiAs("officer.bg");
  const list = await other.json("GET", `/api/applications?applicantName=${encodeURIComponent(name)}`);
  expect(list.items).toEqual([]);
  const clients = await other.json("GET", `/api/clients?search=${encodeURIComponent(name)}`);
  expect(clients.items).toEqual([]);
  const search = await other.json("GET", `/api/search?q=${encodeURIComponent(name)}`);
  expect(search.clients).toEqual([]);
  expect(search.applications).toEqual([]);
  await other.dispose();

  const own = await apiAs("officer");
  const ownList = await own.json("GET", `/api/applications?applicantName=${encodeURIComponent(name)}`);
  expect(ownList.items.map((a: { id: string }) => a.id)).toEqual([applicationId]);
  await own.dispose();
});

test("direct URLs to another branch's records show not found, not the record", async ({ page }) => {
  await signedInAs(page, "officer.bg");
  for (const path of [`/app/applications/${applicationId}`, `/app/clients/${clientId}`]) {
    await page.goto(path);
    await expect(page.getByRole("alert"), path).toContainText(en.notFound);
    await expect(page.getByText(name), path).toHaveCount(0);
  }
});
