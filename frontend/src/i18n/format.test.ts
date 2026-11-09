import { describe, expect, it } from "vitest";
import { formatDate, formatDateTime, formatMoney, formatPercent } from "@/i18n/format";

describe("formatting per language", () => {
  it("formats money with the language's separators and the currency code", () => {
    expect(formatMoney(1234.5, "en")).toMatch(/USD\s1,234\.50/);
    expect(formatMoney(1234.5, "sr")).toMatch(/1\.234,50\sUSD/);
  });

  it("formats dates with the month name in the language", () => {
    expect(formatDate("2026-11-09T10:00:00Z", "en")).toBe("9 November 2026");
    expect(formatDate("2026-11-09T10:00:00Z", "sr")).toMatch(/^9\. novembar 2026\.?$/);
  });

  it("formats a date and time", () => {
    expect(formatDateTime("2026-11-09T10:05:00Z", "en", "UTC")).toBe("9 November 2026, 10:05");
    expect(formatDateTime("2026-11-09T10:05:00Z", "sr", "UTC")).toMatch(/^9\. novembar 2026\.? 10:05$/);
  });

  it("formats a probability as a percentage with one decimal", () => {
    expect(formatPercent(0.2374, "en")).toBe("23.7%");
    expect(formatPercent(0.2374, "sr")).toMatch(/^23,7\s?%$/);
  });
});
