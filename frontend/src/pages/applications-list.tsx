import { useState, type FormEvent } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { Search } from "lucide-react";
import { listApplications, type ApplicationFilters } from "@/api/applications";
import type { ApplicationSummary } from "@/api/types";
import { isoDate, loanOutcomes, oneOf, recommendations, reviewFilters, stages } from "@/lib/filters";
import { can, useMe } from "@/auth/session";
import { EmptyState, ErrorState, Loading, PageHeader } from "@/components/page";
import { StatusBadge } from "@/components/status-badge";
import { DecisionText, SelectField, Table, Tag, Td, Th } from "@/components/surface";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useLanguage } from "@/i18n";
import { formatAmount, formatPercent, formatShortDate } from "@/i18n/format";
import { cn } from "@/lib/utils";

export function ApplicationsListPage({ filters }: { filters: ApplicationFilters }) {
  const { t } = useTranslation();
  const me = useMe();
  const navigate = useNavigate();
  const [name, setName] = useState(filters.applicantName ?? "");
  const applications = useQuery({ queryKey: ["applications", filters], queryFn: () => listApplications(filters), placeholderData: keepPreviousData });
  const filtered = Object.entries(filters).some(([key, value]) => key !== "page" && value !== undefined);

  function update(change: Partial<ApplicationFilters>) {
    void navigate({ to: "/applications", search: { ...filters, ...change, page: undefined } });
  }

  function search(event: FormEvent) {
    event.preventDefault();
    update({ applicantName: name.trim() || undefined });
  }

  return (
    <>
      <PageHeader title={t("list.title")} />
      <div className="grid gap-3">
        <div className="flex flex-wrap items-end gap-3">
          <form onSubmit={search} className="grid gap-1.5" role="search">
            <label htmlFor="applicant-search" className="text-sm font-medium">
              {t("list.search")}
            </label>
            <div className="flex items-center gap-2">
              <Input
                id="applicant-search"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder={t("list.searchPlaceholder")}
                className="w-72 bg-card"
                maxLength={200}
              />
              <Button type="submit" variant="outline">
                <Search aria-hidden="true" />
                {t("list.searchButton")}
              </Button>
            </div>
          </form>
          <div className="w-44">
            <SelectField
              label={t("list.recommendation")}
              value={filters.recommendation ?? ""}
              placeholder={t("list.all")}
              options={recommendations.map((value) => ({ value, label: t(`recommendation.${value}`) }))}
              onChange={(event) => update({ recommendation: oneOf(event.target.value, recommendations) })}
            />
          </div>
          <div className="w-44">
            <SelectField
              label={t("list.stage")}
              value={filters.stage ?? ""}
              placeholder={t("list.all")}
              options={stages.map((value) => ({ value, label: t(`enums.ApplicationStage.${value}`) }))}
              onChange={(event) => update({ stage: oneOf(event.target.value, stages) })}
            />
          </div>
          {can(me, "outcomes.record") && (
            <div className="w-52">
              <SelectField
                label={t("list.loanOutcome")}
                value={filters.loanOutcome ?? ""}
                placeholder={t("list.all")}
                options={loanOutcomes.map((value) => ({ value, label: t(`enums.LoanOutcomeFilter.${value}`) }))}
                onChange={(event) => update({ loanOutcome: oneOf(event.target.value, loanOutcomes) })}
              />
            </div>
          )}
          <DateFilter label={t("list.from")} value={filters.from} onChange={(from) => update({ from })} />
          <DateFilter label={t("list.to")} value={filters.to} onChange={(to) => update({ to })} />
        </div>
        <div className="flex flex-wrap items-center gap-1">
          <FilterLink active={!filters.review} search={{ ...filters, review: undefined, page: undefined }}>
            {t("list.all")}
          </FilterLink>
          {reviewFilters.map((review) => (
            <FilterLink key={review} active={filters.review === review} search={{ ...filters, review, page: undefined }}>
              {t(`enums.ReviewFilter.${review}`)}
            </FilterLink>
          ))}
          {filtered && (
            <Link to="/applications" search={{}} className="ml-auto text-sm text-primary underline-offset-4 hover:underline">
              {t("list.clear")}
            </Link>
          )}
        </div>
      </div>
      {applications.isPending ? (
        <Loading />
      ) : applications.isError ? (
        <ErrorState error={applications.error} onRetry={() => void applications.refetch()} />
      ) : applications.data.items.length === 0 ? (
        filtered ? (
          <EmptyState title={t("list.noResultsTitle")} text={t("list.noResultsText")} />
        ) : (
          <EmptyState title={t("list.emptyTitle")} text={t("list.emptyText")} />
        )
      ) : (
        <>
          <ApplicationsTable items={applications.data.items} />
          <Pager filters={filters} page={applications.data.page} pageSize={applications.data.pageSize} total={applications.data.totalCount} />
        </>
      )}
    </>
  );
}

export function ApplicationsTable({
  items,
  label,
  withApplicant = true,
}: {
  items: ApplicationSummary[];
  label?: string;
  withApplicant?: boolean;
}) {
  const { t } = useTranslation();
  const language = useLanguage();
  return (
    <Table label={label}>
      <thead>
        <tr>
          <Th>{t("list.reference")}</Th>
          {withApplicant && <Th>{t("list.applicant")}</Th>}
          <Th>{t("list.submitted")}</Th>
          <Th align="right">{t("list.amount")}</Th>
          <Th>{t("list.purpose")}</Th>
          <Th align="right">{t("list.risk")}</Th>
          <Th>{t("list.recommendation")}</Th>
          <Th>{t("list.decision")}</Th>
          {withApplicant && <Th>{t("list.assignedTo")}</Th>}
        </tr>
      </thead>
      <tbody>
        {items.map((item) => (
          <tr key={item.id} className="group hover:bg-muted/50">
            <Td className="whitespace-nowrap">
              <Link
                to="/applications/$id"
                params={{ id: item.id }}
                className="font-mono text-xs font-medium text-primary underline-offset-4 hover:underline"
              >
                {item.reference}
              </Link>
            </Td>
            {withApplicant && <Td className="font-medium">{item.applicantName}</Td>}
            <Td className="whitespace-nowrap text-muted-foreground">{formatShortDate(item.submittedAt, language)}</Td>
            <Td align="right" className="whitespace-nowrap">
              {formatAmount(item.loanAmount, language)}
            </Td>
            <Td>{t(`enums.LoanPurpose.${item.purpose}`)}</Td>
            <Td align="right">{item.pBad === null ? "–" : formatPercent(item.pBad, language)}</Td>
            <Td className="whitespace-nowrap">
              <StatusBadge recommendation={item.recommendation} />
            </Td>
            <Td>
              {item.finalDecision ? (
                <span className="inline-flex items-center gap-2">
                  <DecisionText outcome={item.finalDecision} />
                  {item.isOverride && <Tag>{t("list.override")}</Tag>}
                </span>
              ) : (
                <span className="text-muted-foreground">{t(`enums.ApplicationStage.${item.stage}`)}</span>
              )}
            </Td>
            {withApplicant && <Td className="text-muted-foreground">{item.assignedTo?.displayName ?? "–"}</Td>}
          </tr>
        ))}
      </tbody>
    </Table>
  );
}

function DateFilter({ label, value, onChange }: { label: string; value?: string; onChange: (value: string | undefined) => void }) {
  const id = `filter-${label}`;
  return (
    <div className="grid gap-1.5">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      <Input id={id} type="date" value={value ?? ""} onChange={(event) => onChange(isoDate(event.target.value))} className="w-40 bg-card" />
    </div>
  );
}

function FilterLink({ active, search, children }: { active: boolean; search: ApplicationFilters; children: string }) {
  return (
    <Link
      to="/applications"
      search={search}
      aria-current={active ? "page" : undefined}
      className={cn(
        "rounded px-2.5 py-1.5 text-sm",
        active ? "bg-accent font-semibold text-accent-foreground" : "text-muted-foreground hover:bg-muted hover:text-foreground",
      )}
    >
      {children}
    </Link>
  );
}

function Pager({ filters, page, pageSize, total }: { filters: ApplicationFilters; page: number; pageSize: number; total: number }) {
  const { t } = useTranslation();
  const first = (page - 1) * pageSize + 1;
  const last = Math.min(page * pageSize, total);
  const linkClass = "rounded border bg-card px-3 py-1.5 text-sm hover:bg-muted";
  return (
    <div className="flex items-center gap-2 text-sm text-muted-foreground">
      <span>{t("list.range", { from: first, to: last, total })}</span>
      <span className="ml-auto flex gap-2">
        {page > 1 && (
          <Link to="/applications" search={{ ...filters, page: page - 1 > 1 ? page - 1 : undefined }} className={linkClass}>
            {t("list.previous")}
          </Link>
        )}
        {last < total && (
          <Link to="/applications" search={{ ...filters, page: page + 1 }} className={linkClass}>
            {t("list.next")}
          </Link>
        )}
      </span>
    </div>
  );
}
