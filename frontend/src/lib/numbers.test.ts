import { describe, expect, it } from "vitest";
import { parseNumber } from "@/lib/numbers";

describe("parseNumber", () => {
  it.each([
    ["20000", "en", 20000],
    ["20,000", "en", 20000],
    ["18.4", "en", 18.4],
    ["20.000", "sr", 20000],
    ["18,4", "sr", 18.4],
    ["1 234,5", "sr", 1234.5],
    ["1.234,5", "sr", 1234.5],
    ["1,234.5", "en", 1234.5],
    ["18.4", "sr", 18.4],
    ["18,4", "en", 18.4],
  ] as const)("reads %s in %s as %s", (text, language, value) => {
    expect(parseNumber(text, language)).toBe(value);
  });

  it.each(["", "  ", "abc", "12x", "-5", "1.2.3"])("gives null for %j", (text) => {
    expect(parseNumber(text, "en")).toBeNull();
  });
});
