import { expect, request, type APIRequestContext, type APIResponse } from "@playwright/test";
import { passwordOf, runId, type DemoUser } from "./env";

const baseURL = process.env.E2E_BASE ?? "http://localhost:8081";

export type Api = {
  context: APIRequestContext;
  get: (path: string) => Promise<APIResponse>;
  post: (path: string, body?: unknown) => Promise<APIResponse>;
  put: (path: string, body?: unknown) => Promise<APIResponse>;
  json: <T = any>(method: "GET" | "POST" | "PUT", path: string, body?: unknown) => Promise<T>;
  dispose: () => Promise<void>;
};

function wrap(context: APIRequestContext): Api {
  const send = (method: "GET" | "POST" | "PUT", path: string, body?: unknown) =>
    context.fetch(path, {
      method,
      headers: body === undefined ? undefined : { "Content-Type": "application/json" },
      data: body === undefined ? undefined : JSON.stringify(body),
    });
  return {
    context,
    get: (path) => send("GET", path),
    post: (path, body) => send("POST", path, body),
    put: (path, body) => send("PUT", path, body),
    json: async (method, path, body) => {
      const response = await send(method, path, body);
      if (!response.ok()) throw new Error(`${method} ${path}: ${response.status()} ${await response.text()}`);
      const text = await response.text();
      return text ? JSON.parse(text) : undefined;
    },
    dispose: () => context.dispose(),
  };
}

export async function anonymous(): Promise<Api> {
  return wrap(await request.newContext({ baseURL }));
}

export async function signInAs(userName: string, password: string): Promise<Api> {
  const context = await request.newContext({ baseURL });
  const response = await context.post("/api/auth/login", { data: { userName, password } });
  if (!response.ok()) throw new Error(`sign-in ${userName}: ${response.status()}`);
  return wrap(context);
}

export const apiAs = (user: DemoUser) => signInAs(user, passwordOf(user));

export async function expectProblem(response: APIResponse, status: number, code: string) {
  expect(response.status(), `${response.url()}`).toBe(status);
  expect(response.headers()["content-type"]).toContain("application/problem+json");
  const body = await response.json();
  expect(body).toMatchObject({ status, code });
  expect(typeof body.title).toBe("string");
}

export const answers = {
  approve: {
    loanAmount: 8000,
    purpose: "CreditCard",
    employmentLength: "FiveYears",
    homeOwnership: "Mortgage",
    annualIncome: 95000,
    incomeVerification: "Verified",
    debtToIncomePercent: 8,
    creditScore: 785,
    earliestCreditLine: "2008-03-01",
    inquiriesLast6Months: 0,
    delinquenciesLast2Years: 0,
    publicRecords: 0,
    bankruptcies: 0,
    revolvingBalance: 6000,
    revolvingUtilizationPercent: 15,
    openAccounts: 8,
    totalAccounts: 20,
    mortgageAccounts: 1,
  },
  review: {
    loanAmount: 20000,
    purpose: "SmallBusiness",
    employmentLength: "FiveYears",
    homeOwnership: "Rent",
    annualIncome: 54000,
    incomeVerification: "SourceVerified",
    debtToIncomePercent: 18.4,
    creditScore: 667,
    earliestCreditLine: "2008-03-01",
    inquiriesLast6Months: 3,
    delinquenciesLast2Years: 1,
    publicRecords: 0,
    bankruptcies: 0,
    revolvingBalance: 6000,
    revolvingUtilizationPercent: 61.5,
    openAccounts: 8,
    totalAccounts: 20,
    mortgageAccounts: 0,
  },
};

let clientCount = 0;

export function applicantName(label: string) {
  clientCount += 1;
  return `Probni ${label} ${runId}${"abcdefghijklmnopqrstuvwxyz"[clientCount % 26]}`;
}

export async function createClient(api: Api, fullName: string) {
  return api.json<{ id: string; clientNumber: string; fullName: string }>("POST", "/api/clients", {
    fullName,
    dateOfBirth: "1985-06-14",
    employer: "Primer d.o.o.",
  });
}

export async function submitApplication(api: Api, clientId: string, kind: keyof typeof answers) {
  return api.json<{ id: string; reference: string; applicantName: string; decision: { recommendation: string } | null }>(
    "POST",
    "/api/applications",
    { clientId, ...answers[kind] },
  );
}
