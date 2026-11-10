import { expect, test } from "@playwright/test";
import { answers, apiAs, applicantName, createClient, expectProblem, submitApplication } from "../support/api";
import { en } from "../support/text";
import { heading, signedInAs } from "../support/ui";

const form = en.newApplication;
const details = en.details;

test("an officer submits an application through the form and decides it", async ({ page }) => {
  const officer = await apiAs("officer");
  const name = applicantName("Zahtev");
  const client = await createClient(officer, name);

  await signedInAs(page, "officer");
  await page.goto(`/app/applications/new?clientId=${client.id}`);
  await expect(heading(page)).toHaveText("New application");
  await expect(page.getByText(name)).toBeVisible();

  const a = answers.approve;
  await page.getByLabel(form.fields.loanAmount).fill(String(a.loanAmount));
  await page.getByLabel(form.fields.purpose).selectOption({ label: form.options.CreditCard });
  await page.getByLabel(form.fields.annualIncome).fill(String(a.annualIncome));
  await page.getByRole("radio", { name: form.options.Verified, exact: true }).check();
  await page.getByLabel(form.fields.debtToIncomePercent).fill(String(a.debtToIncomePercent));
  await page.getByLabel(form.fields.employmentLength).selectOption({ label: form.options.FiveYears });
  await page.getByLabel(form.fields.homeOwnership).selectOption({ label: form.options.Mortgage });
  await page.getByLabel(form.fields.mortgageAccounts).fill(String(a.mortgageAccounts));
  await page.getByLabel(form.fields.creditScore).fill(String(a.creditScore));
  await page.getByLabel(form.fields.earliestCreditLine).fill(a.earliestCreditLine.slice(0, 7));
  await page.getByLabel(form.fields.inquiriesLast6Months).fill(String(a.inquiriesLast6Months));
  await page.getByLabel(form.fields.delinquenciesLast2Years).fill(String(a.delinquenciesLast2Years));
  await page.getByLabel(form.fields.publicRecords).fill(String(a.publicRecords));
  await page.getByLabel(form.fields.bankruptcies).fill(String(a.bankruptcies));
  await page.getByLabel(form.fields.revolvingBalance).fill(String(a.revolvingBalance));
  await page.getByLabel(form.fields.revolvingUtilizationPercent).fill(String(a.revolvingUtilizationPercent));
  await page.getByLabel(form.fields.openAccounts).fill(String(a.openAccounts));
  await page.getByLabel(form.fields.totalAccounts).fill(String(a.totalAccounts));

  await page.getByRole("button", { name: form.review, exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: form.submit }).click();

  await expect(page).toHaveURL(/\/app\/applications\/[0-9a-f-]{36}$/);
  const id = page.url().split("/").pop()!;
  const created = await officer.json("GET", `/api/applications/${id}`);
  expect(created.applicantName).toBe(name);
  expect(created.reference).toMatch(/^A-\d{6}$/);
  expect(created.decision?.recommendation).toBe("Approve");
  await expect(page.getByText(created.reference).first()).toBeVisible();
  await expect(page.getByText(details.recommendation.Approve, { exact: true }).first()).toBeVisible();

  await page.getByRole("group", { name: details.decision }).getByRole("radio", { name: details.approve, exact: true }).check();
  await page.getByRole("button", { name: details.record }).click();
  await expect(page.getByText(details.recorded)).toBeVisible();
  await expect(page.getByText(details.decidedBy)).toBeVisible();
  await expect(page.getByRole("button", { name: details.record })).toHaveCount(0);

  const decided = await officer.json("GET", `/api/applications/${id}`);
  expect(decided.stage).toBe("Decided");
  expect(decided.officerDecision.outcome).toBe("Approve");

  const again = await officer.post(`/api/applications/${id}/decision`, { outcome: "Reject", reason: "Changing my mind." });
  expect(again.status()).toBe(409);
  const after = await officer.json("GET", `/api/applications/${id}`);
  expect(after.officerDecision.outcome).toBe("Approve");
  await officer.dispose();
});

test("a review case is refused without a reason and accepted with one", async ({ page }) => {
  const officer = await apiAs("officer");
  const client = await createClient(officer, applicantName("Provera"));
  const application = await submitApplication(officer, client.id, "review");
  expect(application.decision?.recommendation, "seeded answers must give a Review recommendation").toBe("Review");

  await signedInAs(page, "officer");
  await page.goto(`/app/applications/${application.id}`);
  await expect(page.getByText(details.recommendation.Review, { exact: true }).first()).toBeVisible();

  await page.getByRole("group", { name: details.decision }).getByRole("radio", { name: details.approve, exact: true }).check();
  await page.getByRole("button", { name: details.record }).click();
  await expect(page.getByText(details.reasonRequiredReview)).toBeVisible();
  await expect(page.getByLabel(details.reason)).toHaveAttribute("aria-invalid", "true");
  expect((await officer.json("GET", `/api/applications/${application.id}`)).stage).toBe("Submitted");

  await page.getByLabel(details.reason).fill("Income confirmed with the employer; utilisation explained.");
  await page.getByRole("button", { name: details.record }).click();
  await expect(page.getByText(details.recorded)).toBeVisible();
  const decided = await officer.json("GET", `/api/applications/${application.id}`);
  expect(decided.stage).toBe("Decided");
  expect(decided.officerDecision.reason).toBe("Income confirmed with the employer; utilisation explained.");
  await officer.dispose();
});

test("the API refuses a review decision without a reason", async () => {
  const officer = await apiAs("officer");
  const client = await createClient(officer, applicantName("Razlog"));
  const application = await submitApplication(officer, client.id, "review");
  const response = await officer.post(`/api/applications/${application.id}/decision`, { outcome: "Approve", reason: null });
  await expectProblem(response, 400, "validation_failed");
  const body = await response.json();
  expect(body.fieldErrors.reason).toEqual([{ code: "reason_required_review" }]);
  await officer.dispose();
});
