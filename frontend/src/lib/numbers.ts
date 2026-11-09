import type { Language } from "@/api/types";

const grouped: Record<Language, RegExp> = {
  en: /^\d{1,3}(,\d{3})+(\.\d+)?$/,
  sr: /^\d{1,3}(\.\d{3})+(,\d+)?$/,
};

export function parseNumber(text: string, language: Language): number | null {
  let normalised = text.replace(/\s/g, "");
  if (!normalised) return null;
  if (grouped[language].test(normalised)) normalised = normalised.split(language === "sr" ? "." : ",").join("");
  normalised = normalised.replace(",", ".");
  return /^\d+(\.\d+)?$/.test(normalised) ? Number(normalised) : null;
}
