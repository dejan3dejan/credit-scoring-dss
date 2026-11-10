import type { FieldError, Problem } from "@/api/types";

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly title: string | undefined;
  readonly fieldErrors: Record<string, FieldError[]>;
  readonly lockedUntil: string | undefined;
  readonly members: Record<string, unknown>;

  constructor(status: number, problem: Partial<Problem>) {
    super(`${status} ${problem.code ?? "unexpected"}`);
    this.name = "ApiError";
    this.status = status;
    this.code = problem.code ?? "unexpected";
    this.title = problem.title;
    this.fieldErrors = problem.fieldErrors ?? {};
    this.lockedUntil = problem.lockedUntil;
    this.members = problem;
  }
}

export function editedElsewhere(error: unknown): boolean {
  return error instanceof ApiError && error.code === "edited_elsewhere";
}

type RequestOptions = { method?: "GET" | "POST" | "PUT" | "DELETE"; body?: unknown };

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: "application/json" };
  if (options.body !== undefined) headers["Content-Type"] = "application/json";

  let response: Response;
  try {
    response = await fetch(path, {
      method: options.method ?? "GET",
      credentials: "same-origin",
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    });
  } catch {
    throw new ApiError(0, { code: "network" });
  }

  if (!response.ok) throw new ApiError(response.status, await readProblem(response));
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

async function readProblem(response: Response): Promise<Partial<Problem>> {
  if (!response.headers.get("Content-Type")?.includes("json")) return {};
  try {
    const body = (await response.json()) as Partial<Problem>;
    return typeof body.code === "string" ? body : {};
  } catch {
    return {};
  }
}
