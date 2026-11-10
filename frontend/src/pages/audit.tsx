import { useState, type FormEvent } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { listAudit, type AuditEntry, type AuditFilters } from "@/api/audit";
import { can, useMe } from "@/auth/session";
import { EmptyState, ErrorState, Loading, PageHeader } from "@/components/page";
import { SelectField, Table, Td, Th } from "@/components/surface";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useLanguage } from "@/i18n";
import { formatDateTime } from "@/i18n/format";
import { auditActions, auditEntities, isoDate, oneOf } from "@/lib/filters";

export function AuditPage({ filters }: { filters: AuditFilters }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const audit = useQuery({ queryKey: ["audit", filters], queryFn: () => listAudit(filters) });
  const [entityId, setEntityId] = useState(filters.entityId ?? "");
  const filtered = Object.entries(filters).some(([key, value]) => key !== "page" && value !== undefined);

  function update(change: Partial<AuditFilters>) {
    void navigate({ to: "/audit", search: { ...filters, ...change, page: undefined } });
  }

  function searchId(event: FormEvent) {
    event.preventDefault();
    update({ entityId: entityId.trim() || undefined });
  }

  return (
    <>
      <PageHeader title={t("audit.title")} description={t("audit.intro")} />
      <div className="flex flex-wrap items-end gap-3">
        <div className="w-48">
          <SelectField
            label={t("audit.entity")}
            value={filters.entity ?? ""}
            placeholder={t("audit.all")}
            options={auditEntities.map((value) => ({ value, label: t(`audit.entities.${value}`) }))}
            onChange={(event) => update({ entity: oneOf(event.target.value, auditEntities) })}
          />
        </div>
        <div className="w-40">
          <SelectField
            label={t("audit.action")}
            value={filters.action ?? ""}
            placeholder={t("audit.all")}
            options={auditActions.map((value) => ({ value, label: t(`audit.actions.${value}`) }))}
            onChange={(event) => update({ action: oneOf(event.target.value, auditActions) })}
          />
        </div>
        {(["from", "to"] as const).map((key) => (
          <div key={key} className="grid gap-1.5">
            <label htmlFor={`audit-${key}`} className="text-sm font-medium">
              {t(`audit.${key}`)}
            </label>
            <Input
              id={`audit-${key}`}
              type="date"
              value={filters[key] ?? ""}
              onChange={(event) => update({ [key]: isoDate(event.target.value) })}
              className="w-40 bg-card"
            />
          </div>
        ))}
        <form onSubmit={searchId} className="grid gap-1.5">
          <label htmlFor="audit-entity-id" className="text-sm font-medium">
            {t("audit.entityId")}
          </label>
          <div className="flex gap-2">
            <Input id="audit-entity-id" value={entityId} onChange={(event) => setEntityId(event.target.value)} className="w-72 bg-card font-mono text-xs" />
            <Button type="submit" variant="outline">
              {t("list.searchButton")}
            </Button>
          </div>
        </form>
      </div>
      {(filters.userId || filtered) && (
        <div className="flex flex-wrap gap-4 text-sm">
          {filters.userId && (
            <Link to="/audit" search={{ ...filters, userId: undefined, page: undefined }} className="text-primary underline-offset-4 hover:underline">
              {t("audit.clearUser")}
            </Link>
          )}
          {filtered && (
            <Link to="/audit" search={{}} className="text-primary underline-offset-4 hover:underline">
              {t("audit.clear")}
            </Link>
          )}
        </div>
      )}
      {audit.isPending ? (
        <Loading />
      ) : audit.isError ? (
        <ErrorState error={audit.error} onRetry={() => void audit.refetch()} />
      ) : audit.data.items.length === 0 ? (
        <EmptyState title={t("audit.empty")} />
      ) : (
        <>
          <AuditTable entries={audit.data.items} filters={filters} />
          <Pager filters={filters} page={audit.data.page} pageSize={audit.data.pageSize} total={audit.data.totalCount} />
        </>
      )}
    </>
  );
}

function AuditTable({ entries, filters }: { entries: AuditEntry[]; filters: AuditFilters }) {
  const { t } = useTranslation();
  const language = useLanguage();
  return (
    <Table label={t("audit.title")}>
      <thead>
        <tr>
          <Th>{t("audit.when")}</Th>
          <Th>{t("audit.who")}</Th>
          <Th>{t("audit.action")}</Th>
          <Th>{t("audit.what")}</Th>
          <Th>{t("audit.changes")}</Th>
        </tr>
      </thead>
      <tbody>
        {entries.map((entry) => (
          <tr key={entry.id} className="group align-top">
            <Td className="whitespace-nowrap text-muted-foreground">{formatDateTime(entry.at, language)}</Td>
            <Td className="whitespace-nowrap">
              {entry.userId && entry.userName ? (
                <Link
                  to="/audit"
                  search={{ ...filters, userId: entry.userId, page: undefined }}
                  aria-label={t("audit.filterUser", { name: entry.userName })}
                  className="underline-offset-4 hover:underline"
                >
                  {entry.userName}
                </Link>
              ) : (
                <span className="text-muted-foreground">{t("audit.system")}</span>
              )}
            </Td>
            <Td>{t(`audit.actions.${entry.action}`)}</Td>
            <Td>
              <div>{t(`audit.entities.${entry.entity}`, { defaultValue: entry.entity })}</div>
              <RecordLink entity={entry.entity} id={entry.entityId} />
            </Td>
            <Td>
              <Changes changes={entry.changes} />
            </Td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}

function RecordLink({ entity, id }: { entity: string; id: string }) {
  const me = useMe();
  const className = "font-mono text-[11px] break-all";
  if (entity === "CreditApplication" && can(me, "applications.read"))
    return (
      <Link to="/applications/$id" params={{ id }} className={`${className} text-primary underline-offset-4 hover:underline`}>
        {id}
      </Link>
    );
  if (entity === "Client" && can(me, "clients.read"))
    return (
      <Link to="/clients/$id" params={{ id }} className={`${className} text-primary underline-offset-4 hover:underline`}>
        {id}
      </Link>
    );
  if (entity === "User" && can(me, "users.manage"))
    return (
      <Link to="/users/$id" params={{ id }} className={`${className} text-primary underline-offset-4 hover:underline`}>
        {id}
      </Link>
    );
  return <span className={`${className} text-muted-foreground`}>{id}</span>;
}

function Changes({ changes }: { changes: AuditEntry["changes"] }) {
  const entries = Object.entries(changes);
  return (
    <dl className="grid grid-cols-[max-content_minmax(0,1fr)] gap-x-3 gap-y-1 text-xs">
      {entries.map(([property, change]) => (
        <div key={property} className="contents">
          <dt className="font-mono text-muted-foreground">{property}</dt>
          <dd className="flex flex-wrap items-baseline gap-1.5 break-all">
            {"before" in change && <Value value={change.before} muted={"after" in change} />}
            {"before" in change && "after" in change && <span aria-hidden="true">→</span>}
            {"after" in change && <Value value={change.after} />}
          </dd>
        </div>
      ))}
    </dl>
  );
}

function Value({ value, muted = false }: { value: unknown; muted?: boolean }) {
  const { t } = useTranslation();
  let text: string;
  if (value === "[personal]") text = t("audit.personal");
  else if (value === "[redacted]") text = t("audit.redacted");
  else if (value === null || value === undefined) text = "–";
  else if (typeof value === "string") text = value;
  else text = JSON.stringify(value);
  const special = value === "[personal]" || value === "[redacted]";
  return <span className={muted ? "text-muted-foreground line-through decoration-muted-foreground/50" : special ? "italic" : ""}>{text}</span>;
}

function Pager({ filters, page, pageSize, total }: { filters: AuditFilters; page: number; pageSize: number; total: number }) {
  const { t } = useTranslation();
  const first = (page - 1) * pageSize + 1;
  const last = Math.min(page * pageSize, total);
  const linkClass = "rounded border bg-card px-3 py-1.5 text-sm hover:bg-muted";
  return (
    <div className="flex items-center gap-2 text-sm text-muted-foreground">
      <span>{t("list.range", { from: first, to: last, total })}</span>
      <span className="ml-auto flex gap-2">
        {page > 1 && (
          <Link to="/audit" search={{ ...filters, page: page - 1 > 1 ? page - 1 : undefined }} className={linkClass}>
            {t("list.previous")}
          </Link>
        )}
        {last < total && (
          <Link to="/audit" search={{ ...filters, page: page + 1 }} className={linkClass}>
            {t("list.next")}
          </Link>
        )}
      </span>
    </div>
  );
}
