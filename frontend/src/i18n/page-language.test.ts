import { describe, expect, it, vi } from "vitest";

describe("page language", () => {
  it("is the stored language as soon as the app starts", async () => {
    localStorage.setItem("language", "sr");
    document.documentElement.lang = "en";
    vi.resetModules();

    const { i18n } = await import("@/i18n");

    expect(i18n.language).toBe("sr");
    expect(document.documentElement.lang).toBe("sr");
  });
});
