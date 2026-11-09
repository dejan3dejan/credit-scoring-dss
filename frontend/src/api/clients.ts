import { request } from "@/api/client";
import type { ClientContact, ClientDetail, ClientResponse, ClientSummary, Paged } from "@/api/types";

export type ClientFilters = { search?: string; page?: number };

export function clientsQuery(filters: ClientFilters): string {
  const params = new URLSearchParams();
  if (filters.search) params.set("search", filters.search);
  if (filters.page && filters.page > 1) params.set("page", String(filters.page));
  const query = params.toString();
  return query ? `?${query}` : "";
}

export const listClients = (filters: ClientFilters) => request<Paged<ClientSummary>>(`/api/clients${clientsQuery(filters)}`);

export const clientKey = (id: string) => ["client", id] as const;

export const getClient = (id: string) => request<ClientDetail>(`/api/clients/${id}`);

export type NewClient = ClientContact & { fullName: string; dateOfBirth: string | null };

export const createClient = (client: NewClient) => request<ClientResponse>("/api/clients", { method: "POST", body: client });
