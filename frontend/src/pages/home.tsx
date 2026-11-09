import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { Plus } from "lucide-react";
import { can, useMe } from "@/auth/session";
import { homeTitleKey } from "@/auth/home";
import { EmptyState, PageHeader } from "@/components/page";
import { Button } from "@/components/ui/button";
import { useLanguage } from "@/i18n";
import { formatDate } from "@/i18n/format";

export function HomePage() {
  const { t } = useTranslation();
  const me = useMe();
  const language = useLanguage();

  return (
    <>
      <PageHeader
        title={t(homeTitleKey[me.role])}
        description={formatDate(new Date(), language)}
        actions={
          can(me, "applications.write") && (
            <Button asChild>
              <Link to="/applications/new">
                <Plus aria-hidden="true" />
                {t("home.newApplication")}
              </Link>
            </Button>
          )
        }
      />
      <EmptyState title={t("home.emptyTitle")} text={t("home.emptyText")} />
    </>
  );
}
