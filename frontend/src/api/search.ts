import { request } from "@/api/client";
import type { ApplicationSummary, ClientSummary } from "@/api/types";

export type SearchResults = { clients: ClientSummary[]; applications: ApplicationSummary[] };

export const searchKey = (q: string) => ["search", q] as const;

export const search = (q: string) => request<SearchResults>(`/api/search?${new URLSearchParams({ q })}`);
