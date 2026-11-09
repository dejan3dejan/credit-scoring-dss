import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { EmptyState, PageHeader } from "@/components/page";
import { Button } from "@/components/ui/button";

export function NotBuiltPage({ titleKey }: { titleKey: string }) {
  const { t } = useTranslation();
  return (
    <>
      <PageHeader title={t(titleKey)} />
      <EmptyState title={t("pages.notBuiltTitle")} text={t("pages.notBuiltText")} />
    </>
  );
}

export function NoAccessPage() {
  const { t } = useTranslation();
  return (
    <>
      <PageHeader title={t("noAccess.title")} />
      <EmptyState title={t("noAccess.text")} action={<BackHome label={t("noAccess.back")} />} />
    </>
  );
}

export function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <div className="mx-auto max-w-lg px-6 py-24">
      <PageHeader title={t("notFound.title")} description={t("notFound.text")} />
      <div className="mt-6">
        <BackHome label={t("notFound.back")} />
      </div>
    </div>
  );
}

function BackHome({ label }: { label: string }) {
  return (
    <Button asChild variant="outline">
      <Link to="/">{label}</Link>
    </Button>
  );
}
