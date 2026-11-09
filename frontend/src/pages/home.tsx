import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { Plus } from "lucide-react";
import type { ApplicationSummary } from "@/api/types";
import { getMyWork, getTeamWork } from "@/api/work";
import { homeTitleKey } from "@/auth/home";
import { can, useMe } from "@/auth/session";
import { EmptyState, ErrorState, Loading, PageHeader } from "@/components/page";
import { Table, Td, Th } from "@/components/surface";
import { Button } from "@/components/ui/button";
import { useLanguage } from "@/i18n";
import { formatDate } from "@/i18n/format";
import { ApplicationsTable } from "@/pages/applications-list";

export function HomePage() {
  const { t } = useTranslation();
  const me = useMe();
  const language = useLanguage();
  const title = t(homeTitleKey[me.role]);

  return (
    <>
      <PageHeader
        title={title}
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
      {can(me, "applications.write") ? (
        <>
          <MyWorkSection title={title} />
          {can(me, "applications.assign") && <TeamSection />}
        </>
      ) : (
        <EmptyState title={t("home.emptyTitle")} text={me.role === "Risk" ? t("home.overviewText") : t("home.emptyText")} />
      )}
    </>
  );
}

function Tiles({ label, items }: { label: string; items: { label: string; value: number }[] }) {
  return (
    <ul aria-label={label} className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
      {items.map((item) => (
        <li key={item.label} className="rounded-md border bg-card px-4 py-3">
          <div className="text-xs text-muted-foreground">{item.label}</div>
          <div className="mt-1 text-[26px] leading-none font-semibold">{item.value}</div>
        </li>
      ))}
    </ul>
  );
}

function Queue({ title, items }: { title: string; items: ApplicationSummary[] }) {
  if (items.length === 0) return null;
  return (
    <section className="grid gap-2">
      <h2 className="font-semibold">{title}</h2>
      <ApplicationsTable items={items} />
    </section>
  );
}

function MyWorkSection({ title }: { title: string }) {
  const { t } = useTranslation();
  const work = useQuery({ queryKey: ["work", "mine"], queryFn: getMyWork });
  if (work.isPending) return <Loading />;
  if (work.isError) return <ErrorState error={work.error} onRetry={() => void work.refetch()} />;
  const { counts } = work.data;

  return (
    <>
      <Tiles
        label={title}
        items={[
          { label: t("home.awaitingDecision"), value: counts.awaitingDecision },
          { label: t("home.scoringUnavailable"), value: counts.scoringUnavailable },
          { label: t("home.awaitingSignOff"), value: counts.awaitingSignOff },
          { label: t("home.drafts"), value: counts.drafts },
          { label: t("home.decidedLast7Days"), value: counts.decidedLast7Days },
        ]}
      />
      {work.data.awaitingDecision.length === 0 && work.data.scoringUnavailable.length === 0 ? (
        <EmptyState title={t("home.nothingWaiting")} />
      ) : (
        <>
          <Queue title={t("home.awaitingDecision")} items={work.data.awaitingDecision} />
          <Queue title={t("home.scoringUnavailable")} items={work.data.scoringUnavailable} />
        </>
      )}
      <Queue title={t("home.awaitingSignOff")} items={work.data.awaitingSignOff} />
      <Queue title={t("home.recentlyDecided")} items={work.data.recentlyDecided} />
    </>
  );
}

function TeamSection() {
  const { t } = useTranslation();
  const team = useQuery({ queryKey: ["work", "team"], queryFn: getTeamWork });

  return (
    <section className="grid gap-4 border-t pt-5">
      <h2 className="text-lg font-semibold">{t("home.team")}</h2>
      {team.isPending ? (
        <Loading />
      ) : team.isError ? (
        <ErrorState error={team.error} onRetry={() => void team.refetch()} />
      ) : (
        <>
          <Tiles
            label={t("home.team")}
            items={[
              { label: t("home.teamAwaitingSignOff"), value: team.data.counts.awaitingSignOff },
              { label: t("home.teamUnassigned"), value: team.data.counts.unassigned },
              { label: t("home.teamScoringUnavailable"), value: team.data.counts.scoringUnavailable },
            ]}
          />
          <Queue title={t("home.teamAwaitingSignOff")} items={team.data.awaitingSignOff} />
          <Queue title={t("home.teamUnassigned")} items={team.data.unassigned} />
          {team.data.officers.length > 0 && (
            <section className="grid gap-2">
              <h3 className="font-semibold">{t("home.officers")}</h3>
              <Table label={t("home.officers")}>
                <thead>
                  <tr>
                    <Th>{t("home.officer")}</Th>
                    <Th align="right">{t("home.open")}</Th>
                    <Th align="right">{t("home.signOffs")}</Th>
                  </tr>
                </thead>
                <tbody>
                  {team.data.officers.map((officer) => (
                    <tr key={officer.id} className="group">
                      <Td>{officer.displayName}</Td>
                      <Td align="right">{officer.open}</Td>
                      <Td align="right">{officer.awaitingSignOff}</Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </section>
          )}
        </>
      )}
    </section>
  );
}
