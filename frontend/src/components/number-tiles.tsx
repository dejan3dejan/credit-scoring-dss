import { useTranslation } from "react-i18next";
import { cn } from "@/lib/utils";

export type NumberTile = { label: string; value: string | number; previous?: string; note?: string };

export function NumberTiles({ label, items, className }: { label: string; items: NumberTile[]; className?: string }) {
  const { t } = useTranslation();
  return (
    <ul aria-label={label} className={cn("grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5", className)}>
      {items.map((item) => (
        <li key={item.label} className="rounded-md border bg-card px-4 py-3">
          <div className="text-xs text-muted-foreground">{item.label}</div>
          <div className="mt-1 text-[26px] leading-none font-semibold">{item.value}</div>
          {item.previous !== undefined && (
            <div className="mt-1.5 text-xs text-muted-foreground">{t("dashboard.previous", { value: item.previous })}</div>
          )}
          {item.note && <div className="mt-1.5 text-[11px] leading-snug text-muted-foreground">{item.note}</div>}
        </li>
      ))}
    </ul>
  );
}
