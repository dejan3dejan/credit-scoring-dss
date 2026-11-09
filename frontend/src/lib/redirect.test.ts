import { describe, expect, it } from "vitest";
import { safeRedirect } from "@/lib/redirect";

describe("safeRedirect", () => {
  it("keeps a path inside the app", () => {
    expect(safeRedirect("/clients?q=a")).toBe("/clients?q=a");
  });

  it.each([undefined, "", "https://evil.example", "//evil.example", "clients"])("replaces %s with the start page", (value) => {
    expect(safeRedirect(value)).toBe("/");
  });
});
