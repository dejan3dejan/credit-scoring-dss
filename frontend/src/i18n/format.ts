import type { Language } from "@/api/types";

const locales: Record<Language, string> = { en: "en-GB", sr: "sr-Latn-RS" };
const dateTimeSeparator: Record<Language, string> = { en: ", ", sr: " " };

export function formatMoney(amount: number, language: Language, currency = "USD"): string {
  return new Intl.NumberFormat(locales[language], { style: "currency", currency, currencyDisplay: "code" }).format(amount);
}

export function formatAmount(amount: number, language: Language, currency = "USD"): string {
  return new Intl.NumberFormat(locales[language], {
    style: "currency",
    currency,
    currencyDisplay: "code",
    maximumFractionDigits: 0,
    minimumFractionDigits: 0,
  }).format(amount);
}

export function formatNumber(value: number, language: Language, maximumFractionDigits = 0): string {
  return new Intl.NumberFormat(locales[language], { maximumFractionDigits }).format(value);
}

export function formatPercent(fraction: number, language: Language): string {
  return new Intl.NumberFormat(locales[language], {
    style: "percent",
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  }).format(fraction);
}

export function formatDate(value: string | Date, language: Language, timeZone?: string): string {
  return new Intl.DateTimeFormat(locales[language], { day: "numeric", month: "long", year: "numeric", timeZone }).format(
    new Date(value),
  );
}

export function formatShortDate(value: string | Date, language: Language, timeZone?: string): string {
  return new Intl.DateTimeFormat(locales[language], { day: "numeric", month: "short", year: "numeric", timeZone }).format(
    new Date(value),
  );
}

export function formatMonthYear(isoDate: string, language: Language): string {
  return new Intl.DateTimeFormat(locales[language], { month: "short", year: "numeric", timeZone: "UTC" }).format(
    new Date(`${isoDate}T00:00:00Z`),
  );
}

export function formatTime(value: string | Date, language: Language, timeZone?: string): string {
  return new Intl.DateTimeFormat(locales[language], { hour: "2-digit", minute: "2-digit", hourCycle: "h23", timeZone }).format(
    new Date(value),
  );
}

export function formatDateTime(value: string | Date, language: Language, timeZone?: string): string {
  return formatDate(value, language, timeZone) + dateTimeSeparator[language] + formatTime(value, language, timeZone);
}
