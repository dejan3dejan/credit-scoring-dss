import type { TFunction } from "i18next";
import type { ApplicationResponse, Language, ScoreFactor } from "@/api/types";
import { formatAmount, formatMonthYear, formatNumber } from "@/i18n/format";

const enumInputs: Record<string, string> = {
  Purpose: "LoanPurpose",
  EmploymentLength: "EmploymentLength",
  HomeOwnership: "HomeOwnership",
  IncomeVerification: "IncomeVerification",
};

const moneyInputs = new Set(["LoanAmount", "AnnualIncome", "RevolvingBalance"]);
const percentInputs = new Set(["DebtToIncomePercent", "RevolvingUtilizationPercent"]);
const countInputs = new Set([
  "CreditScore",
  "InquiriesLast6Months",
  "DelinquenciesLast2Years",
  "PublicRecords",
  "Bankruptcies",
  "OpenAccounts",
  "TotalAccounts",
  "MortgageAccounts",
]);

function camel(input: string): keyof ApplicationResponse {
  return (input[0].toLowerCase() + input.slice(1)) as keyof ApplicationResponse;
}

export function describeInput(application: ApplicationResponse, input: string, t: TFunction, language: Language) {
  const value = application[camel(input)];
  const label = t(`fields.${input}`);
  if (input in enumInputs) return { label, value: t(`enums.${enumInputs[input]}.${value}`) };
  if (moneyInputs.has(input)) return { label, value: formatAmount(value as number, language) };
  if (percentInputs.has(input)) return { label, value: `${formatNumber(value as number, language, 2)}%` };
  if (countInputs.has(input)) return { label, value: formatNumber(value as number, language) };
  if (input === "EarliestCreditLine")
    return {
      label,
      value: t("details.creditHistory", {
        date: formatMonthYear(value as string, language),
        years: formatNumber(application.creditHistoryYears, language, 1),
      }),
    };
  throw new Error(`No description for application input ${input}`);
}

export function checklist(factors: ScoreFactor[]): string[] {
  return factors.filter((factor) => factor.contribution > 0).map((factor) => factor.input);
}
