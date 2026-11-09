import { describe, expect, it } from "vitest";
import en from "@/i18n/en.json";
import sr from "@/i18n/sr.json";

function keys(value: unknown, prefix = ""): string[] {
  if (typeof value !== "object" || value === null) return [prefix];
  return Object.entries(value).flatMap(([key, child]) => keys(child, prefix ? `${prefix}.${key}` : key));
}

describe("translations", () => {
  it("have the same keys in English and Serbian", () => {
    expect(keys(sr).sort()).toEqual(keys(en).sort());
  });

  it("have no empty strings", () => {
    const empty = (json: object) => keys(json).filter((key) => key.split(".").reduce<unknown>((node, part) => (node as Record<string, unknown>)[part], json) === "");
    expect(empty(en)).toEqual([]);
    expect(empty(sr)).toEqual([]);
  });
});
