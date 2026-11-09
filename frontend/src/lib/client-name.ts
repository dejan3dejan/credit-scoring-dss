import type { TFunction } from "i18next";

export function clientName(client: { fullName: string; anonymisedAt: string | null }, t: TFunction): string {
  return client.anonymisedAt ? t("clients.anonymisedName") : client.fullName;
}

export function clientStatus(client: { isActive: boolean; anonymisedAt: string | null }): "anonymised" | "active" | "inactive" {
  if (client.anonymisedAt) return "anonymised";
  return client.isActive ? "active" : "inactive";
}
