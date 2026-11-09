import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { ChevronLeft, Plus, Search } from "lucide-react";
import { toast } from "sonner";
import { ApiError } from "@/api/client";
import { clientKey, createClient, getClient, listClients, type ClientFilters } from "@/api/clients";
import type { ClientSummary } from "@/api/types";
import { can, useMe } from "@/auth/session";
import { EmptyState, ErrorState, Loading, PageHeader } from "@/components/page";
import { Alert, Card, Facts, Table, Td, Th } from "@/components/surface";
import { TextField } from "@/components/text-field";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { errorMessage, useLanguage } from "@/i18n";
import { formatDate, formatShortDate } from "@/i18n/format";
import { clientName, clientStatus } from "@/lib/client-name";
import { cn } from "@/lib/utils";
import { ApplicationsTable } from "@/pages/applications-list";

export function ClientsListPage({ filters }: { filters: ClientFilters }) {
  const { t } = useTranslation();
  const me = useMe();
  const navigate = useNavigate();
  const [text, setText] = useState(filters.search ?? "");
  const clients = useQuery({ queryKey: ["clients", filters], queryFn: () => listClients(filters) });

  function search(event: FormEvent) {
    event.preventDefault();
    void navigate({ to: "/clients", search: { search: text.trim() || undefined } });
  }

  return (
    <>
      <PageHeader
        title={t("clients.title")}
        actions={
          can(me, "clients.write") && (
            <Button asChild>
              <Link to="/clients/new">
                <Plus aria-hidden="true" />
                {t("clients.newClient")}
              </Link>
            </Button>
          )
        }
      />
      <form onSubmit={search} className="grid gap-1.5" role="search">
        <label htmlFor="clients-search" className="text-sm font-medium">
          {t("clients.search")}
        </label>
        <div className="flex gap-2">
          <Input
            id="clients-search"
            value={text}
            onChange={(event) => setText(event.target.value)}
            placeholder={t("clients.searchPlaceholder")}
            className="w-80 bg-card"
          />
          <Button type="submit" variant="outline">
            <Search aria-hidden="true" />
            {t("list.searchButton")}
          </Button>
        </div>
      </form>
      {clients.isPending ? (
        <Loading />
      ) : clients.isError ? (
        <ErrorState error={clients.error} onRetry={() => void clients.refetch()} />
      ) : clients.data.items.length === 0 ? (
        filters.search ? (
          <EmptyState title={t("clients.noResultsTitle")} text={t("clients.noResultsText")} />
        ) : (
          <EmptyState title={t("clients.emptyTitle")} text={t("clients.emptyText")} />
        )
      ) : (
        <>
          <ClientsTable items={clients.data.items} />
          <ClientsPager filters={filters} page={clients.data.page} pageSize={clients.data.pageSize} total={clients.data.totalCount} />
        </>
      )}
    </>
  );
}

function ClientsTable({ items }: { items: ClientSummary[] }) {
  const { t } = useTranslation();
  const language = useLanguage();
  return (
    <Table>
      <thead>
        <tr>
          <Th>{t("clients.number")}</Th>
          <Th>{t("clients.name")}</Th>
          <Th>{t("clients.dateOfBirth")}</Th>
          <Th align="right">{t("clients.applications")}</Th>
          <Th>{t("clients.lastApplication")}</Th>
          <Th>{t("clients.status")}</Th>
        </tr>
      </thead>
      <tbody>
        {items.map((client) => (
          <tr key={client.id} className="group hover:bg-muted/50">
            <Td className="font-mono text-xs">{client.clientNumber}</Td>
            <Td>
              <Link to="/clients/$id" params={{ id: client.id }} className="font-medium text-primary underline-offset-4 hover:underline">
                {clientName(client, t)}
              </Link>
            </Td>
            <Td className="text-muted-foreground">{client.dateOfBirth ? formatShortDate(client.dateOfBirth, language, "UTC") : "–"}</Td>
            <Td align="right">{client.applicationCount}</Td>
            <Td className="text-muted-foreground">
              {client.lastApplicationAt ? formatShortDate(client.lastApplicationAt, language) : "–"}
            </Td>
            <Td>
              <ClientStatus client={client} />
            </Td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}

function ClientStatus({ client }: { client: { isActive: boolean; anonymisedAt: string | null } }) {
  const { t } = useTranslation();
  const status = clientStatus(client);
  return (
    <span className={cn("text-sm", status === "active" ? "text-foreground" : "text-muted-foreground")}>{t(`clients.${status}`)}</span>
  );
}

function ClientsPager({ filters, page, pageSize, total }: { filters: ClientFilters; page: number; pageSize: number; total: number }) {
  const { t } = useTranslation();
  const first = (page - 1) * pageSize + 1;
  const last = Math.min(page * pageSize, total);
  const linkClass = "rounded border bg-card px-3 py-1.5 text-sm hover:bg-muted";
  return (
    <div className="flex items-center gap-2 text-sm text-muted-foreground">
      <span>{t("list.range", { from: first, to: last, total })}</span>
      <span className="ml-auto flex gap-2">
        {page > 1 && (
          <Link to="/clients" search={{ ...filters, page: page - 1 > 1 ? page - 1 : undefined }} className={linkClass}>
            {t("list.previous")}
          </Link>
        )}
        {last < total && (
          <Link to="/clients" search={{ ...filters, page: page + 1 }} className={linkClass}>
            {t("list.next")}
          </Link>
        )}
      </span>
    </div>
  );
}

export function ClientDetailsPage({ id }: { id: string }) {
  const { t } = useTranslation();
  const me = useMe();
  const language = useLanguage();
  const client = useQuery({ queryKey: clientKey(id), queryFn: () => getClient(id) });

  if (client.isPending) return <Loading />;
  if (client.isError) return <ErrorState error={client.error} onRetry={() => void client.refetch()} />;
  const c = client.data;
  const latest = [...c.applications].sort((x, y) => y.submittedAt.localeCompare(x.submittedAt))[0];
  const canApply = can(me, "applications.write") && c.isActive;

  return (
    <>
      <div className="flex flex-wrap items-end gap-4">
        <div>
          <Link to="/clients" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
            <ChevronLeft className="size-4" aria-hidden="true" />
            {t("clients.back")}
          </Link>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">{clientName(c, t)}</h1>
          <p className="mt-1 text-muted-foreground">
            <span className="font-mono text-xs">{c.clientNumber}</span> · <ClientStatus client={c} />
          </p>
        </div>
        {canApply && (
          <div className="ml-auto flex gap-2">
            {latest && (
              <Button asChild variant="outline">
                <Link to="/applications/new" search={{ clientId: c.id, from: latest.id }}>
                  {t("clients.startFromLast")}
                </Link>
              </Button>
            )}
            <Button asChild>
              <Link to="/applications/new" search={{ clientId: c.id }}>
                {t("clients.newApplication")}
              </Link>
            </Button>
          </div>
        )}
      </div>
      <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_340px]">
        <section className="grid min-w-0 gap-3 xl:col-start-1 xl:row-start-1">
          <h2 className="font-semibold">{t("clients.applications")}</h2>
          {c.applications.length === 0 ? (
            <EmptyState title={t("clients.noApplicationsTitle")} text={t("clients.noApplicationsText")} />
          ) : (
            <ApplicationsTable items={c.applications} withApplicant={false} />
          )}
        </section>
        <Card title={t("clients.details")} className="xl:col-start-2 xl:row-start-1">
          <Facts
            items={[
              { label: t("clients.dateOfBirth"), value: c.dateOfBirth ? formatDate(c.dateOfBirth, language, "UTC") : "–" },
              { label: t("clients.address"), value: c.address ?? "–" },
              { label: t("clients.phone"), value: c.phone ?? "–" },
              { label: t("clients.email"), value: c.email ?? "–" },
              { label: t("clients.employer"), value: c.employer ?? "–" },
              { label: t("clients.createdAt"), value: formatDate(c.createdAt, language) },
            ]}
          />
        </Card>
      </div>
    </>
  );
}

export function NewClientPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [form, setForm] = useState({ fullName: "", dateOfBirth: "", address: "", phone: "", email: "", employer: "" });
  const set = (name: keyof typeof form) => (value: string) => setForm((current) => ({ ...current, [name]: value }));
  const blankToNull = (value: string) => value.trim() || null;

  const create = useMutation({
    mutationFn: () =>
      createClient({
        fullName: form.fullName.trim(),
        dateOfBirth: form.dateOfBirth || null,
        address: blankToNull(form.address),
        phone: blankToNull(form.phone),
        email: blankToNull(form.email),
        employer: blankToNull(form.employer),
      }),
    onSuccess: async (client) => {
      void queryClient.invalidateQueries({ queryKey: ["clients"] });
      toast.success(t("clients.created", { number: client.clientNumber }));
      await navigate({ to: "/applications/new", search: { clientId: client.id } });
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    create.mutate();
  }

  const errors = create.error instanceof ApiError ? create.error.fieldErrors : {};
  const optional = t("clients.optional");

  return (
    <>
      <PageHeader title={t("clients.newClient")} />
      <form onSubmit={submit} noValidate className="grid max-w-2xl gap-4 rounded-md border bg-card p-6">
        {create.error && Object.keys(errors).length === 0 && <Alert>{errorMessage(create.error)}</Alert>}
        <TextField label={t("clients.fullName")} value={form.fullName} onChange={(e) => set("fullName")(e.target.value)} errors={errors.fullName} maxLength={200} autoComplete="off" />
        <div className="grid gap-4 md:grid-cols-2">
          <TextField label={t("clients.dateOfBirth")} type="date" value={form.dateOfBirth} onChange={(e) => set("dateOfBirth")(e.target.value)} errors={errors.dateOfBirth} hint={optional} />
          <TextField label={t("clients.phone")} type="tel" value={form.phone} onChange={(e) => set("phone")(e.target.value)} errors={errors.phone} hint={optional} maxLength={32} />
          <TextField label={t("clients.email")} type="email" value={form.email} onChange={(e) => set("email")(e.target.value)} errors={errors.email} hint={optional} maxLength={254} />
          <TextField label={t("clients.employer")} value={form.employer} onChange={(e) => set("employer")(e.target.value)} errors={errors.employer} hint={optional} maxLength={200} />
        </div>
        <TextField label={t("clients.address")} value={form.address} onChange={(e) => set("address")(e.target.value)} errors={errors.address} hint={optional} maxLength={200} />
        <div>
          <Button type="submit" disabled={create.isPending}>
            {t("clients.create")}
          </Button>
        </div>
      </form>
    </>
  );
}
