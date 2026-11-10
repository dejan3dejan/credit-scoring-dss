import { request } from "@/api/client";

export type Range = { minimum: number; maximum: number };

export type Limits = {
  application: {
    ranges: Record<string, Range>;
    termMonths: number;
    creditLineMaximumYears: number;
    creditLineMinimumAge: number;
  };
  client: { minimumAge: number; maximumAge: number };
};

export const limitsKey = ["limits"] as const;

export const getLimits = () => request<Limits>("/api/limits");
