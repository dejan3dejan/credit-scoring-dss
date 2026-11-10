import { vi } from "vitest";
import type { MeResponse, Permission, Role } from "@/api/types";

export type StubReply = { status: number; body?: unknown };
export type StubRoute = StubReply | ((body: unknown) => StubReply);
export type ApiCall = { method: string; path: string; body: unknown };

export function stubApi(routes: Record<string, StubRoute>) {
  const calls: ApiCall[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const method = (init?.method ?? "GET").toUpperCase();
    const path = String(input);
    const body = init?.body ? JSON.parse(String(init.body)) : undefined;
    calls.push({ method, path, body });
    const key = `${method} ${path}`;
    const prefix = Object.keys(routes).find((candidate) => candidate.endsWith("*") && key.startsWith(candidate.slice(0, -1)));
    const route = routes[key] ?? (prefix ? routes[prefix] : undefined);
    const reply = route === undefined ? notFound : typeof route === "function" ? route(body) : route;
    const contentType = reply.status >= 400 ? "application/problem+json" : "application/json";
    return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
      status: reply.status,
      headers: { "Content-Type": contentType },
    });
  });
  vi.stubGlobal("fetch", fetchMock);
  return calls;
}

export function problem(status: number, code: string, extra: Record<string, unknown> = {}): StubReply {
  return { status, body: { status, code, title: `English title for ${code}`, ...extra } };
}

const notFound = problem(404, "not_found");
export const unauthenticated = problem(401, "unauthenticated");

const permissionsByRole: Record<Role, Permission[]> = {
  Admin: ["audit.read", "branches.manage", "clients.anonymise", "users.manage"],
  Officer: ["applications.decide", "applications.read", "applications.write", "clients.read", "clients.write", "models.read"],
  Manager: [
    "applications.approve-escalated",
    "applications.assign",
    "applications.decide",
    "applications.read",
    "applications.write",
    "clients.read",
    "clients.write",
    "models.read",
    "stats.read",
  ],
  Risk: ["applications.read", "audit.read", "clients.read", "models.manage", "models.read", "outcomes.record", "stats.read"],
};

const names: Record<Role, string> = {
  Admin: "Petar Admin",
  Officer: "Mila Testić",
  Manager: "Jovan Testić",
  Risk: "Vera Testić",
};

export function meFor(role: Role, overrides: Partial<MeResponse> = {}): MeResponse {
  return {
    id: "00000000-0000-0000-0000-00000000000" + Object.keys(names).indexOf(role),
    userName: role.toLowerCase(),
    displayName: names[role],
    role,
    branch: role === "Officer" || role === "Manager" ? { id: "b1", code: "NS", name: "Novi Sad" } : null,
    permissions: permissionsByRole[role],
    preferredLanguage: "en",
    mustChangePassword: false,
    ...overrides,
  };
}

export function signedInAs(role: Role, overrides: Partial<MeResponse> = {}): Record<string, StubRoute> {
  return { "GET /api/auth/me": { status: 200, body: meFor(role, overrides) } };
}
