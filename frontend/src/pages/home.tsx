import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { Plus } from "lucide-react";
import { listApplications } from "@/api/applications";
import { listAudit } from "@/api/audit";
import { branchesKey, listBranches } from "@/api/branches";
import { listModels, modelsKey } from "@/api/models";
import { getStats, type PeriodStats, type StatsFilters } from "@/api/stats";
import type { ApplicationSummary } from "@/api/types";
import { listUsers, usersKey } from "@/api/users";
import { getMyWork, getTeamWork, type MyWork } from "@/api/work";
import { homeTitleKey } from "@/auth/home";
import { can, useMe } from "@/auth/session";
import { CalibrationTable } from "@/components/calibration-table";
import { NumberTiles } from "@/components/number-tiles";
import { EmptyState, ErrorState, Loading, PageHeader } from "@/components/page";
import { Card, Table, Td, Th } from "@/components/surface";
import { Button } from "@/components/ui/button";
import { useLanguage } from "@/i18n";
import { formatDate, formatDateTime, formatPercent } from "@/i18n/format";
import { calibratedBands } from "@/lib/calibration";
import { rangeFilters } from "@/lib/dashboard-range";
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
      ) : can(me, "users.manage") ? (
        <AdminOverview title={title} />
      ) : (
        <RiskOverview />
      )}
    </>
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
      <NumberTiles
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
      <Drafts drafts={work.data.drafts} />
      <Queue title={t("home.awaitingSignOff")} items={work.data.awaitingSignOff} />
      <Queue title={t("home.recentlyDecided")} items={work.data.recentlyDecided} />
    </>
  );
}

function Drafts({ drafts }: { drafts: MyWork["drafts"] }) {
  const { t } = useTranslation();
  const language = useLanguage();
  if (drafts.length === 0) return null;
  return (
    <section className="grid gap-2">
      <h2 className="font-semibold">{t("drafts.title")}</h2>
      <Table label={t("drafts.title")}>
        <thead>
          <tr>
            <Th>{t("drafts.client")}</Th>
            <Th>{t("drafts.updated")}</Th>
            <Th />
          </tr>
        </thead>
        <tbody>
          {drafts.map((draft) => (
            <tr key={draft.id} className="group">
              <Td>
                <span className="font-medium">{draft.clientName}</span>{" "}
                <span className="font-mono text-xs text-muted-foreground">{draft.clientNumber}</span>
              </Td>
              <Td className="text-muted-foreground">{formatDateTime(draft.updatedAt, language)}</Td>
              <Td align="right">
                <Link
                  to="/applications/new"
                  search={{ draftId: draft.id }}
                  aria-label={`${t("drafts.continue")} ${draft.clientName}`}
                  className="text-primary underline-offset-4 hover:underline"
                >
                  {t("drafts.continue")}
                </Link>
              </Td>
            </tr>
          ))}
        </tbody>
      </Table>
    </section>
  );
}

function useLast30Days() {
  const [filters] = useState(() => rangeFilters("30", new Date()));
  return useQuery({ queryKey: ["stats", filters], queryFn: () => getStats(filters) });
}

function PeriodNumbers({ label }: { label: string }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const stats = useLast30Days();
  if (stats.isPending) return <Loading />;
  if (stats.isError) return <ErrorState error={stats.error} onRetry={() => void stats.refetch()} />;
  if (!stats.data.comparison) return null;
  const { current, previous } = stats.data.comparison;
  const percent = (value: number | null) => (value === null ? t("dashboard.none") : formatPercent(value, language));
  const rate = (label: string, pick: (period: PeriodStats) => number | null) => ({
    label,
    value: percent(pick(current)),
    previous: percent(pick(previous)),
  });

  return (
    <section className="grid gap-2">
      <h2 className="font-semibold">{label}</h2>
      <NumberTiles
        label={label}
        items={[
          { label: t("dashboard.applications"), value: current.applications, previous: String(previous.applications) },
          rate(t("dashboard.approvalRate"), (period) => period.approvalRate),
          rate(t("dashboard.overrideRate"), (period) => period.overrideRate),
          rate(t("dashboard.reviewShare"), (period) => period.reviewShare),
          rate(t("dashboard.unavailableRate"), (period) => period.unavailableRate),
        ]}
      />
    </section>
  );
}

function RiskOverview() {
  const { t } = useTranslation();
  const me = useMe();
  return (
    <>
      {can(me, "outcomes.record") && <WaitingOutcomes />}
      <ActiveModel />
      {can(me, "stats.read") && (
        <>
          <PeriodNumbers label={t("home.allBranchesPeriod")} />
          <CalibrationSnapshot />
        </>
      )}
      <div className="flex flex-wrap gap-2">
        {can(me, "stats.read") && (
          <Button asChild variant="outline">
            <Link to="/dashboard">{t("home.riskDashboard")}</Link>
          </Button>
        )}
        <Button asChild variant="outline">
          <Link to="/models">{t("home.riskModels")}</Link>
        </Button>
      </div>
    </>
  );
}

function WaitingOutcomes() {
  const { t } = useTranslation();
  const waiting = useQuery({
    queryKey: ["applications", { loanOutcome: "Pending", pageSize: 5 }],
    queryFn: () => listApplications({ loanOutcome: "Pending", pageSize: 5 }),
  });
  const title = t("home.riskOutcomes");

  return (
    <section aria-label={title} className="grid gap-2">
      <div className="flex items-baseline gap-3">
        <h2 className="font-semibold">{title}</h2>
        {waiting.data && <span className="text-[26px] leading-none font-semibold">{waiting.data.totalCount}</span>}
        <Link
          to="/applications"
          search={{ loanOutcome: "Pending" }}
          className="ml-auto text-sm text-primary underline-offset-4 hover:underline"
        >
          {t("home.showAll")}
        </Link>
      </div>
      {waiting.isPending ? (
        <Loading />
      ) : waiting.isError ? (
        <ErrorState error={waiting.error} onRetry={() => void waiting.refetch()} />
      ) : waiting.data.items.length === 0 ? (
        <EmptyState title={t("home.noOutcomesWaiting")} />
      ) : (
        <ApplicationsTable items={waiting.data.items} />
      )}
    </section>
  );
}

function ActiveModel() {
  const { t } = useTranslation();
  const language = useLanguage();
  const models = useQuery({ queryKey: modelsKey, queryFn: listModels });
  if (models.isPending) return <Loading />;
  if (models.isError) return <ErrorState error={models.error} onRetry={() => void models.refetch()} />;
  const active = models.data.find((model) => model.role === "Active");

  return (
    <Card title={t("home.activeModel")}>
      {active ? (
        <div className="flex flex-wrap items-baseline gap-x-6 gap-y-1">
          <span className="font-semibold">{active.displayName}</span>
          <span className="text-muted-foreground">{active.dataset}</span>
          <span className="text-muted-foreground">
            {t("home.threshold", { value: formatPercent(active.threshold, language) })}
          </span>
        </div>
      ) : (
        <p className="text-muted-foreground">{t("home.noActiveModel")}</p>
      )}
    </Card>
  );
}

const allTime: StatsFilters = {};

function CalibrationSnapshot() {
  const { t } = useTranslation();
  const stats = useQuery({ queryKey: ["stats", allTime], queryFn: () => getStats(allTime) });
  const title = t("dashboard.calibrationTitle");
  if (stats.isPending) return <Loading />;
  if (stats.isError) return <ErrorState error={stats.error} onRetry={() => void stats.refetch()} />;
  const bands = calibratedBands(stats.data.calibration);

  return (
    <Card title={title} footer={bands.length > 0 ? t("dashboard.calibrationNote") : undefined}>
      {bands.length === 0 ? (
        <p className="text-muted-foreground">{t("dashboard.noOutcomes")}</p>
      ) : (
        <CalibrationTable bands={bands} label={title} />
      )}
    </Card>
  );
}

function AdminOverview({ title }: { title: string }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const users = useQuery({ queryKey: usersKey, queryFn: listUsers });
  const branches = useQuery({ queryKey: branchesKey, queryFn: listBranches });
  if (users.isPending || branches.isPending) return <Loading />;
  if (users.isError) return <ErrorState error={users.error} onRetry={() => void users.refetch()} />;
  if (branches.isError) return <ErrorState error={branches.error} onRetry={() => void branches.refetch()} />;
  const active = users.data.filter((user) => user.isActive);
  const locked = active.filter((user) => user.lockedUntil);

  return (
    <>
      <NumberTiles
        label={title}
        className="xl:grid-cols-4"
        items={[
          { label: t("admin.users"), value: active.length },
          { label: t("admin.lockedUsers"), value: locked.length },
          { label: t("admin.mustChange"), value: active.filter((user) => user.mustChangePassword).length },
          { label: t("admin.activeBranches"), value: branches.data.filter((branch) => branch.isActive).length },
        ]}
      />
      {locked.length > 0 && (
        <section className="grid gap-2">
          <h2 className="font-semibold">{t("admin.lockedUsers")}</h2>
          <Table label={t("admin.lockedUsers")}>
            <thead>
              <tr>
                <Th>{t("home.user")}</Th>
                <Th>{t("home.lockedUntil")}</Th>
              </tr>
            </thead>
            <tbody>
              {locked.map((user) => (
                <tr key={user.id} className="group">
                  <Td>
                    <Link to="/users/$id" params={{ id: user.id }} className="font-medium text-primary underline-offset-4 hover:underline">
                      {user.displayName}
                    </Link>
                  </Td>
                  <Td className="text-muted-foreground">{formatDateTime(user.lockedUntil!, language)}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        </section>
      )}
      <LatestChanges />
      <div className="flex flex-wrap gap-2">
        <Button asChild variant="outline">
          <Link to="/users">{t("admin.openUsers")}</Link>
        </Button>
        <Button asChild variant="outline">
          <Link to="/branches">{t("admin.openBranches")}</Link>
        </Button>
        <Button asChild variant="outline">
          <Link to="/audit">{t("admin.openAudit")}</Link>
        </Button>
      </div>
    </>
  );
}

function LatestChanges() {
  const { t } = useTranslation();
  const language = useLanguage();
  const audit = useQuery({ queryKey: ["audit", { pageSize: 50 }], queryFn: () => listAudit({ pageSize: 50 }) });
  const title = t("home.latestChanges");
  if (audit.isPending) return <Loading />;
  if (audit.isError) return <ErrorState error={audit.error} onRetry={() => void audit.refetch()} />;
  const entries = audit.data.items.filter((entry) => entry.userId !== null).slice(0, 10);
  if (entries.length === 0) return null;

  return (
    <section className="grid gap-2">
      <h2 className="font-semibold">{title}</h2>
      <Table label={title}>
        <thead>
          <tr>
            <Th>{t("home.when")}</Th>
            <Th>{t("home.user")}</Th>
            <Th>{t("home.change")}</Th>
            <Th>{t("home.record")}</Th>
          </tr>
        </thead>
        <tbody>
          {entries.map((entry) => (
            <tr key={entry.id} className="group">
              <Td className="whitespace-nowrap text-muted-foreground">{formatDateTime(entry.at, language)}</Td>
              <Td>{entry.userName}</Td>
              <Td>{t(`audit.actions.${entry.action}`)}</Td>
              <Td>{t(`audit.entities.${entry.entity}`, { defaultValue: entry.entity })}</Td>
            </tr>
          ))}
        </tbody>
      </Table>
    </section>
  );
}

function TeamSection() {
  const { t } = useTranslation();
  const team = useQuery({ queryKey: ["work", "team"], queryFn: getTeamWork });

  return (
    <section className="grid gap-4 border-t pt-5">
      <h2 className="text-lg font-semibold">{t("home.team")}</h2>
      <PeriodNumbers label={t("home.branchPeriod")} />
      {team.isPending ? (
        <Loading />
      ) : team.isError ? (
        <ErrorState error={team.error} onRetry={() => void team.refetch()} />
      ) : (
        <>
          <NumberTiles
            label={t("home.team")}
            className="xl:grid-cols-3"
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
