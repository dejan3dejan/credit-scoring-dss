import i18next from "i18next";
import { initReactI18next, useTranslation } from "react-i18next";
import type { Language } from "@/api/types";
import { ApiError } from "@/api/client";
import type { FieldError } from "@/api/types";
import { formatDateTime } from "@/i18n/format";
import en from "@/i18n/en.json";
import sr from "@/i18n/sr.json";

const storageKey = "language";

export const languages: Language[] = ["en", "sr"];

function storedLanguage(): Language {
  try {
    const value = localStorage.getItem(storageKey);
    return value === "sr" ? "sr" : "en";
  } catch {
    return "en";
  }
}

function rememberLanguage(language: Language) {
  try {
    localStorage.setItem(storageKey, language);
  } catch {
    return;
  }
}

export const i18n = i18next.createInstance();

export async function applyLanguage(language: Language) {
  rememberLanguage(language);
  if (i18n.language !== language) await i18n.changeLanguage(language);
}

void i18n.use(initReactI18next).init({
  resources: { en: { translation: en }, sr: { translation: sr } },
  lng: storedLanguage(),
  fallbackLng: "en",
  interpolation: { escapeValue: false },
  initAsync: false,
});

i18n.on("languageChanged", (language) => {
  document.documentElement.lang = language;
});

export function useLanguage(): Language {
  const { i18n: instance } = useTranslation();
  return instance.language === "sr" ? "sr" : "en";
}

export function errorMessage(error: unknown): string {
  const language: Language = i18n.language === "sr" ? "sr" : "en";
  if (!(error instanceof ApiError)) return i18n.t("errors.unexpected");
  if (error.code === "locked_out" && error.lockedUntil)
    return i18n.t("errors.locked_out", { time: formatDateTime(error.lockedUntil, language) });
  const key = `errors.${error.code}`;
  if (i18n.exists(key)) return i18n.t(key);
  return error.title ?? i18n.t("errors.unexpected");
}

export function fieldErrorMessage(error: FieldError): string {
  const code = error.code === "length" && error.minimum === 0 ? "length_max" : error.code;
  const key = `fieldErrors.${code}`;
  return i18n.exists(key) ? i18n.t(key, error) : i18n.t("fieldErrors.invalid_value");
}
