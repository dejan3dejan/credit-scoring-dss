import type { StatsFilters } from "@/api/stats";

export type DashboardRange = "30" | "90" | "all";
export type DashboardSearch = { range: DashboardRange; branchId?: string };

export function validateDashboardSearch(search: Record<string, unknown>): DashboardSearch {
  const range = search.range === "90" || search.range === "all" ? search.range : "30";
  return { range, branchId: typeof search.branchId === "string" && search.branchId ? search.branchId : undefined };
}

export function rangeFilters(range: DashboardRange, now: Date): StatsFilters {
  if (range === "all") return {};
  const days = Number(range);
  return {
    from: new Date(now.getFullYear(), now.getMonth(), now.getDate() - days).toISOString(),
    to: new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1).toISOString(),
  };
}
