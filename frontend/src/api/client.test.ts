import { describe, expect, it, vi } from "vitest";
import { ApiError, request } from "@/api/client";
import { problem, stubApi } from "@/test/api";

async function failure(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise;
  } catch (error) {
    if (error instanceof ApiError) return error;
    throw error;
  }
  throw new Error("the request did not fail");
}

describe("request", () => {
  it("returns the parsed body of a successful answer", async () => {
    stubApi({ "GET /api/things": { status: 200, body: { name: "a" } } });
    await expect(request("/api/things")).resolves.toEqual({ name: "a" });
  });

  it("sends JSON with the same-origin cookie", async () => {
    const fetchMock = vi.fn(async () => new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    await request("/api/things", { method: "POST", body: { a: 1 } });
    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(init.method).toBe("POST");
    expect(init.credentials).toBe("same-origin");
    expect(new Headers(init.headers).get("Content-Type")).toBe("application/json");
    expect(init.body).toBe('{"a":1}');
  });

  it("returns undefined for 204", async () => {
    stubApi({ "POST /api/auth/logout": { status: 204 } });
    await expect(request("/api/auth/logout", { method: "POST" })).resolves.toBeUndefined();
  });

  it("throws an ApiError carrying the problem's code, title and field errors", async () => {
    stubApi({
      "POST /api/auth/login": problem(400, "validation_failed", {
        fieldErrors: { password: [{ code: "required" }], userName: [{ code: "length", minimum: 0, maximum: 64 }] },
      }),
    });
    const error = await failure(request("/api/auth/login", { method: "POST", body: {} }));
    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(400);
    expect(error.code).toBe("validation_failed");
    expect(error.title).toBe("English title for validation_failed");
    expect(error.fieldErrors.userName).toEqual([{ code: "length", minimum: 0, maximum: 64 }]);
  });

  it("keeps lockedUntil from a locked_out problem", async () => {
    stubApi({ "POST /api/auth/login": problem(401, "locked_out", { lockedUntil: "2026-11-09T21:15:00Z" }) });
    const error = await failure(request("/api/auth/login", { method: "POST", body: {} }));
    expect(error.code).toBe("locked_out");
    expect(error.lockedUntil).toBe("2026-11-09T21:15:00Z");
  });

  it("throws an ApiError with code unexpected when the error body is not a problem", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("<html>Bad gateway</html>", { status: 502 })));
    const error = await failure(request("/api/things"));
    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(502);
    expect(error.code).toBe("unexpected");
  });
});
