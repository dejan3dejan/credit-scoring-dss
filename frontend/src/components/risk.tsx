import { useTranslation } from "react-i18next";
import { ArrowDown, ArrowUp } from "lucide-react";
import type { ApplicationResponse, ScoreFactor } from "@/api/types";
import { useLanguage } from "@/i18n";
import { formatPercent } from "@/i18n/format";
import { describeInput } from "@/lib/application-inputs";
import { cn } from "@/lib/utils";

export function RiskScale({ pBad, reviewFrom, threshold }: { pBad: number; reviewFrom: number | null; threshold: number }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const max = pBad > 0.5 || threshold > 0.5 ? 1 : 0.5;
  const at = (value: number) => `${(Math.min(value, max) / max) * 100}%`;
  const percent = (value: number) => formatPercent(value, language);
  const label = reviewFrom
    ? t("details.scaleLabel", { pBad: percent(pBad), reviewFrom: percent(reviewFrom), threshold: percent(threshold) })
    : t("details.scaleLabelNoBand", { pBad: percent(pBad), threshold: percent(threshold) });

  return (
    <div role="img" aria-label={label}>
      <div className="relative flex h-2 rounded-full">
        <div className="rounded-l-full bg-approve-tint" style={{ width: at(reviewFrom ?? threshold) }} />
        {reviewFrom !== null && <div className="bg-review-tint" style={{ width: `calc(${at(threshold)} - ${at(reviewFrom)})` }} />}
        <div className="flex-1 rounded-r-full bg-reject-tint" />
        <div className="absolute -top-1 h-4 w-[3px] -translate-x-1/2 rounded bg-foreground" style={{ left: at(pBad) }} />
      </div>
      <div className="relative mt-1.5 h-4 text-[11px] text-muted-foreground" aria-hidden="true">
        <span className="absolute left-0">{percent(0)}</span>
        {reviewFrom !== null && (
          <span className="absolute -translate-x-1/2 whitespace-nowrap" style={{ left: at(reviewFrom) }}>
            {t("details.reviewFrom", { value: percent(reviewFrom) })}
          </span>
        )}
        <span className="absolute -translate-x-1/2 whitespace-nowrap" style={{ left: at(threshold) }}>
          {t("details.rejectFrom", { value: percent(threshold) })}
        </span>
        <span className="absolute right-0">{percent(max)}</span>
      </div>
    </div>
  );
}

export function FactorList({ application, factors }: { application: ApplicationResponse; factors: ScoreFactor[] }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const largest = Math.max(...factors.map((factor) => Math.abs(factor.contribution)));

  return (
    <ol className="grid gap-2.5">
      {factors.map((factor) => {
        const { label, value } = describeInput(application, factor.input, t, language);
        const raises = factor.contribution > 0;
        return (
          <li key={factor.input} className="grid grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)_96px_120px] items-center gap-3">
            <span className="truncate">{label}</span>
            <span className="truncate text-muted-foreground">{value}</span>
            <span className="h-1.5 rounded-full bg-muted" aria-hidden="true">
              <span
                className={cn("block h-full rounded-full", raises ? "bg-reject" : "bg-approve")}
                style={{ width: `${(Math.abs(factor.contribution) / largest) * 100}%` }}
              />
            </span>
            <span className={cn("inline-flex items-center gap-1 text-xs font-semibold", raises ? "text-reject" : "text-approve")}>
              {raises ? <ArrowUp className="size-3.5" aria-hidden="true" /> : <ArrowDown className="size-3.5" aria-hidden="true" />}
              {raises ? t("details.raises") : t("details.lowers")}
            </span>
          </li>
        );
      })}
    </ol>
  );
}
