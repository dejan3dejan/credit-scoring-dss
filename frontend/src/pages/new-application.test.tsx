import { describe, expect, it } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderApp } from "@/test/render";
import { problem, signedInAs, stubApi } from "@/test/api";
import { application, clientDetail, clientSummary, paged } from "@/test/fixtures";

const client = { "GET /api/clients/c1": { status: 200, body: clientDetail() } };

async function fillForm() {
  const user = userEvent.setup({ delay: null });
  const type = async (label: string, value: string) => user.type(screen.getByLabelText(label), value);
  const choose = async (label: string, value: string) => user.selectOptions(screen.getByLabelText(label), value);
  await type("Loan amount", "20000");
  await choose("Purpose", "Small business");
  await type("Annual income", "54000");
  await user.click(screen.getByRole("radio", { name: "Source verified" }));
  await type("Debt-to-income ratio", "18.4");
  await choose("Years in current job", "3 years");
  await choose("Housing", "Rents");
  await type("Mortgage accounts", "0");
  await type("Credit score", "667");
  await type("First credit line opened", "2012-05");
  await type("Credit inquiries, last 6 months", "3");
  await type("Delinquencies, last 2 years", "1");
  await type("Derogatory public records", "0");
  await type("Bankruptcies", "0");
  await type("Revolving balance", "8200");
  await type("Revolving utilisation", "61.5");
  await type("Open credit lines", "9");
  await type("Credit lines ever opened", "21");
  return user;
}

describe("new application", () => {
  it("starts by finding the client", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...client,
      "GET /api/clients?search=Mil": { status: 200, body: paged([clientSummary()]) },
    });
    renderApp("/applications/new");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Search clients"), "Mil");
    await user.click(screen.getByRole("button", { name: "Search" }));
    await user.click(await screen.findByRole("link", { name: "Choose Milica Testić" }));
    expect(await screen.findByLabelText("Loan amount")).toBeInTheDocument();
    expect(screen.getByText("C-000007")).toBeInTheDocument();
    expect(calls.map((c) => c.path)).toContain("/api/clients?search=Mil");
  });

  it("submits the answers as numbers and opens the scored application", async () => {
    const calls = stubApi({
      ...signedInAs("Officer"),
      ...client,
      "POST /api/applications": { status: 201, body: application({ id: "a9" }) },
      "GET /api/applications/a9": { status: 200, body: application({ id: "a9" }) },
    });
    renderApp("/applications/new?clientId=c1");
    await screen.findByLabelText("Loan amount");
    const user = await fillForm();
    await user.click(screen.getByRole("button", { name: "Review" }));
    await user.click(await screen.findByRole("button", { name: "Get recommendation" }));
    expect(await screen.findByRole("heading", { name: "What moved the score" })).toBeInTheDocument();
    expect(calls.find((c) => c.method === "POST")?.body).toEqual({
      clientId: "c1",
      loanAmount: 20000,
      purpose: "SmallBusiness",
      employmentLength: "ThreeYears",
      homeOwnership: "Rent",
      annualIncome: 54000,
      incomeVerification: "SourceVerified",
      debtToIncomePercent: 18.4,
      creditScore: 667,
      earliestCreditLine: "2012-05-01",
      inquiriesLast6Months: 3,
      delinquenciesLast2Years: 1,
      publicRecords: 0,
      bankruptcies: 0,
      revolvingBalance: 8200,
      revolvingUtilizationPercent: 61.5,
      openAccounts: 9,
      totalAccounts: 21,
      mortgageAccounts: 0,
    });
  });

  it("shows the API's field errors next to the fields", async () => {
    stubApi({
      ...signedInAs("Officer"),
      ...client,
      "POST /api/applications": problem(400, "validation_failed", {
        fieldErrors: {
          loanAmount: [{ code: "range", minimum: 1000, maximum: 35000 }],
          openAccounts: [{ code: "not_greater_than", other: "totalAccounts" }],
        },
      }),
    });
    renderApp("/applications/new?clientId=c1");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Review" }));
    await user.click(await screen.findByRole("button", { name: "Get recommendation" }));
    expect(await screen.findByText("Enter a value from 1,000 to 35,000.")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(screen.getByLabelText("Loan amount")).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByText("Must not be larger than Credit lines ever opened.")).toBeInTheDocument();
  });

  it("shows every answer for a last check before submitting", async () => {
    const calls = stubApi({ ...signedInAs("Officer"), ...client });
    renderApp("/applications/new?clientId=c1");
    await screen.findByLabelText("Loan amount");
    const user = await fillForm();
    await user.click(screen.getByRole("button", { name: "Review" }));
    const dialog = await screen.findByRole("dialog", { name: "Check the application" });
    expect(within(dialog).getByText("Milica Testić")).toBeInTheDocument();
    expect(within(dialog).getByText("Small business")).toBeInTheDocument();
    expect(within(dialog).getByText("Source verified")).toBeInTheDocument();
    expect(within(dialog).getByText(/20,000/)).toBeInTheDocument();
    expect(within(dialog).getByText("18.4%")).toBeInTheDocument();
    expect(within(dialog).getByText("May 2012")).toBeInTheDocument();
    expect(within(dialog).queryByText("Not entered")).toBeNull();
    expect(calls.some((c) => c.path === "/api/applications")).toBe(false);
  });

  it("marks missing answers in the check", async () => {
    stubApi({ ...signedInAs("Officer"), ...client });
    renderApp("/applications/new?clientId=c1");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Loan amount"), "20000");
    await user.click(screen.getByRole("button", { name: "Review" }));
    const dialog = await screen.findByRole("dialog", { name: "Check the application" });
    expect(within(dialog).getByText("Missing answers: 17")).toBeInTheDocument();
    expect(within(dialog).getAllByText("Not entered")).toHaveLength(17);
  });

  it("goes back to the form from the check without submitting", async () => {
    const calls = stubApi({ ...signedInAs("Officer"), ...client });
    renderApp("/applications/new?clientId=c1");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText("Loan amount"), "20000");
    await user.click(screen.getByRole("button", { name: "Review" }));
    await user.click(await screen.findByRole("button", { name: "Back to the form" }));
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(screen.getByLabelText("Loan amount")).toHaveValue("20000");
    expect(calls.some((c) => c.path === "/api/applications")).toBe(false);
  });

  it("shows the allowed range under each number field, from the server", async () => {
    stubApi({ ...signedInAs("Officer"), ...client });
    renderApp("/applications/new?clientId=c1");
    expect(await screen.findByLabelText("Credit score")).toHaveAccessibleDescription("Allowed: 300 to 850. FICO score.");
    expect(screen.getByLabelText("Loan amount")).toHaveAccessibleDescription("Allowed: 1,000 to 35,000. The model was trained on 36-month loans only.");
    expect(screen.getByLabelText("Bankruptcies")).toHaveAccessibleDescription("Allowed: 0 to 50.");
    expect(screen.getByLabelText("First credit line opened")).toHaveAccessibleDescription(
      "Month and year, from age 18 and at most 80 years ago; the model uses the length of the credit history.",
    );
  });

  it("says when the client already has an open application on submit", async () => {
    stubApi({
      ...signedInAs("Officer"),
      ...client,
      "POST /api/applications": problem(409, "client_has_open_application", { applicationId: "a12", reference: "A-000012" }),
    });
    renderApp("/applications/new?clientId=c1");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Review" }));
    await user.click(await screen.findByRole("button", { name: "Get recommendation" }));
    expect(await screen.findByText("This client already has an open application, A-000012.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Open A-000012" })).toHaveAttribute("href", "/app/applications/a12");
  });

  it("fills the form from the client's last application", async () => {
    stubApi({ ...signedInAs("Officer"), ...client, "GET /api/applications/a1": { status: 200, body: application() } });
    renderApp("/applications/new?clientId=c1&from=a1");
    expect(await screen.findByText(/Filled in from application A-000001/)).toBeInTheDocument();
    expect(screen.getByLabelText("Loan amount")).toHaveValue("20000");
    expect(screen.getByLabelText("Purpose")).toHaveValue("SmallBusiness");
    expect(screen.getByLabelText("First credit line opened")).toHaveValue("2012-05");
  });
});
