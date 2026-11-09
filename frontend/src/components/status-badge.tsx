import { CircleCheck, CircleMinus, CircleX, TriangleAlert, type LucideIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import type { Recommendation } from "@/api/types";
import { cn } from "@/lib/utils";

const styles: Record<Recommendation | "none", { icon: LucideIcon; className: string }> = {
  Approve: { icon: CircleCheck, className: "bg-approve-tint text-approve" },
  Review: { icon: TriangleAlert, className: "bg-review-tint text-review" },
  Reject: { icon: CircleX, className: "bg-reject-tint text-reject" },
  none: { icon: CircleMinus, className: "bg-none-tint text-none" },
};

export function StatusBadge({ recommendation }: { recommendation: Recommendation | null }) {
  const { t } = useTranslation();
  const key = recommendation ?? "none";
  const { icon: Icon, className } = styles[key];
  return (
    <span className={cn("inline-flex items-center gap-1.5 rounded px-2 py-0.5 text-xs font-semibold", className)}>
      <Icon className="size-3.5" aria-hidden="true" />
      {t(`recommendation.${key}`)}
    </span>
  );
}
