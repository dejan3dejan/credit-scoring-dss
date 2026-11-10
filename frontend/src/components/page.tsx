import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { BrandLoader } from "@/components/brand-mark";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/i18n";

export function PageHeader({ title, description, actions }: { title: string; description?: string; actions?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-end gap-4">
      <div className="min-w-0">
        <h1 className="text-[22px] leading-tight font-semibold">{title}</h1>
        {description && <p className="mt-1 text-muted-foreground">{description}</p>}
      </div>
      {actions && <div className="ml-auto flex items-center gap-2">{actions}</div>}
    </div>
  );
}

export function EmptyState({ title, text, action }: { title: string; text?: string; action?: ReactNode }) {
  return (
    <div className="rounded-md border border-dashed bg-card px-6 py-12 text-center">
      <p className="font-semibold">{title}</p>
      {text && <p className="mx-auto mt-1 max-w-md text-muted-foreground">{text}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation();
  return (
    <div role="alert" className="rounded-md border border-reject/30 bg-reject-tint px-6 py-6">
      <p className="font-semibold text-reject">{t("states.errorTitle")}</p>
      <p className="mt-1">{errorMessage(error)}</p>
      {onRetry && (
        <Button variant="outline" size="sm" className="mt-3" onClick={onRetry}>
          {t("states.retry")}
        </Button>
      )}
    </div>
  );
}

export function Loading() {
  const { t } = useTranslation();
  return (
    <div className="flex items-center gap-2.5 py-12 text-muted-foreground" role="status">
      <BrandLoader size={22} />
      {t("states.loading")}
    </div>
  );
}
