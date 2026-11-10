import { expect, type Page } from "@playwright/test";
import { passwordOf, type DemoUser } from "./env";
import { en } from "./text";

export async function signInThroughForm(page: Page, userName: string, password: string) {
  await page.getByLabel(en.signIn.userName).fill(userName);
  await page.getByLabel(en.signIn.password).fill(password);
  await page.getByRole("button", { name: en.signIn.submit }).click();
}

export async function signedInAs(page: Page, user: DemoUser) {
  const response = await page.request.post("/api/auth/login", { data: { userName: user, password: passwordOf(user) } });
  expect(response.ok(), `sign-in ${user}`).toBeTruthy();
}

export const heading = (page: Page) => page.getByRole("heading", { level: 1 });

export const mainNav = (page: Page, label = en.nav.label) => page.getByRole("navigation", { name: label });
