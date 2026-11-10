import { readFileSync } from "node:fs";
import { resolve } from "node:path";

export type DemoUser = "officer" | "officer.bg" | "manager" | "risk" | "admin";

type SeedUser = { UserName: string; Password: string };

const seedFile = process.env.E2E_SEED_FILE ?? resolve(import.meta.dirname, "../../backend/src/CreditScoring.Api/appsettings.Development.json");

let users: SeedUser[] | undefined;

export function passwordOf(user: DemoUser): string {
  if (!users) {
    const text = readFileSync(seedFile, "utf8");
    users = JSON.parse(text.charCodeAt(0) === 0xfeff ? text.slice(1) : text).Seed.Users as SeedUser[];
  }
  const found = users.find((u) => u.UserName === user);
  if (!found) throw new Error(`no seeded user ${user} in ${seedFile}`);
  return found.Password;
}

const letters = "abcdefghijklmnopqrstuvwxyz";

export const runId = Array.from({ length: 6 }, () => letters[Math.floor(Math.random() * letters.length)]).join("");
