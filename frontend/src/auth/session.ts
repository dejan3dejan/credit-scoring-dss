import { queryOptions, useSuspenseQuery } from "@tanstack/react-query";
import { getMe } from "@/api/auth";
import type { MeResponse, Permission } from "@/api/types";

export const meKey = ["me"] as const;

export const meQuery = queryOptions({
  queryKey: meKey,
  queryFn: getMe,
  staleTime: 60_000,
  retry: false,
});

export function useMe(): MeResponse {
  return useSuspenseQuery(meQuery).data;
}

export function can(me: MeResponse, permission: Permission): boolean {
  return me.permissions.includes(permission);
}
