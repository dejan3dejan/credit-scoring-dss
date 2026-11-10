import { request } from "@/api/client";
import type { Paged } from "@/api/types";

export type AuditAction = "Created" | "Updated" | "Deleted";

export type AuditEntry = {
  id: string;
  at: string;
  userId: string | null;
  userName: string | null;
  action: AuditAction;
  entity: string;
  entityId: string;
  traceId: string | null;
  changes: Record<string, { before?: unknown; after?: unknown }>;
};

export type AuditFilters = {
  from?: string;
  to?: string;
  userId?: string;
  entity?: string;
  action?: AuditAction;
  entityId?: string;
  page?: number;
};

export function auditQuery(filters: AuditFilters): string {
  const params = new URLSearchParams();
  if (filters.from) params.set("from", new Date(`${filters.from}T00:00:00`).toISOString());
  if (filters.to) {
    const end = new Date(`${filters.to}T00:00:00`);
    end.setDate(end.getDate() + 1);
    params.set("to", end.toISOString());
  }
  if (filters.userId) params.set("userId", filters.userId);
  if (filters.entity) params.set("entity", filters.entity);
  if (filters.action) params.set("action", filters.action);
  if (filters.entityId) params.set("entityId", filters.entityId);
  if (filters.page && filters.page > 1) params.set("page", String(filters.page));
  const query = params.toString();
  return query ? `?${query}` : "";
}

export const listAudit = (filters: AuditFilters) => request<Paged<AuditEntry>>(`/api/audit${auditQuery(filters)}`);
