import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { ChevronLeft, Plus, Search } from "lucide-react";
import { toast } from "sonner";
import { ApiError } from "@/api/client";
import {
  clientKey,
  createClient,
  deactivateClient,
  getClient,
  listClients,
  reactivateClient,
  updateClient,
  type ClientFilters,
  type NewClient,
} from "@/api/clients";
import type { ClientDetail, ClientResponse, ClientSummary } from "@/api/types";
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
  const client = useQuery({ queryKey: clientKey(id), queryFn: () => getClient(id) });

  if (client.isPending) return <Loading />;
  if (client.isError) return <ErrorState error={client.error} onRetry={() => void client.refetch()} />;
  const c = client.data;
  const latest = [...c.applications].sort((x, y) => y.submittedAt.localeCompare(x.submittedAt))[0];
  const canApply = can(me, "applications.write") && c.isActive;
  const canManage = can(me, "clients.write") && !c.anonymisedAt;

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
      <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_360px]">
        <section className="grid min-w-0 gap-3 xl:col-start-1 xl:row-start-1">
          <h2 className="font-semibold">{t("clients.applications")}</h2>
          {c.applications.length === 0 ? (
            <EmptyState title={t("clients.noApplicationsTitle")} text={t("clients.noApplicationsText")} />
          ) : (
            <ApplicationsTable items={c.applications} withApplicant={false} />
          )}
        </section>
        <div className="grid gap-5 xl:col-start-2 xl:row-start-1">
          <ClientDetailsCard client={c} canEdit={canManage} />
          {canManage && <ClientStatusCard client={c} />}
        </div>
      </div>
    </>
  );
}

function useClientUpdate(id: string) {
  const queryClient = useQueryClient();
  return (fresh: ClientResponse) => {
    queryClient.setQueryData(clientKey(id), (current: ClientDetail | undefined) => (current ? { ...current, ...fresh } : current));
    void queryClient.invalidateQueries({ queryKey: ["clients"] });
  };
}

function ClientDetailsCard({ client: c, canEdit }: { client: ClientDetail; canEdit: boolean }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const [editing, setEditing] = useState(false);
  const update = useClientUpdate(c.id);
  const save = useMutation({
    mutationFn: (values: NewClient) => updateClient(c.id, values),
    onSuccess: (fresh) => {
      update(fresh);
      setEditing(false);
      toast.success(t("clients.saved"));
    },
  });

  return (
    <Card
      title={t("clients.details")}
      actions={
        canEdit &&
        !editing && (
          <Button size="sm" variant="outline" onClick={() => setEditing(true)}>
            {t("clients.edit")}
          </Button>
        )
      }
    >
      {editing ? (
        <ClientForm
          initial={c}
          submitLabel={t("clients.save")}
          pending={save.isPending}
          error={save.error}
          onSubmit={(values) => save.mutate(values)}
          onCancel={() => setEditing(false)}
          stacked
        />
      ) : (
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
      )}
    </Card>
  );
}

function ClientStatusCard({ client: c }: { client: ClientDetail }) {
  const { t } = useTranslation();
  const update = useClientUpdate(c.id);
  const [confirming, setConfirming] = useState(false);
  const change = useMutation({
    mutationFn: () => (c.isActive ? deactivateClient(c.id) : reactivateClient(c.id)),
    onSuccess: (fresh) => {
      update(fresh);
      setConfirming(false);
      toast.success(t(fresh.isActive ? "clients.reactivated" : "clients.deactivated"));
    },
  });

  return (
    <Card title={t("clients.manage")}>
      <div className="grid gap-3">
        {change.error && <Alert>{errorMessage(change.error)}</Alert>}
        {c.isActive ? (
          <>
            <p className="text-muted-foreground">{t("clients.deactivateText")}</p>
            {confirming ? (
              <div className="flex gap-2">
                <Button variant="outline" className="border-reject text-reject" onClick={() => change.mutate()} disabled={change.isPending}>
                  {t("clients.deactivateConfirm")}
                </Button>
                <Button variant="ghost" onClick={() => setConfirming(false)}>
                  {t("actions.cancel")}
                </Button>
              </div>
            ) : (
              <div>
                <Button variant="outline" onClick={() => setConfirming(true)}>
                  {t("clients.deactivate")}
                </Button>
              </div>
            )}
          </>
        ) : (
          <div>
            <Button variant="outline" onClick={() => change.mutate()} disabled={change.isPending}>
              {t("clients.reactivate")}
            </Button>
          </div>
        )}
      </div>
    </Card>
  );
}

type ClientFormValues = { fullName: string; dateOfBirth: string; address: string; phone: string; email: string; employer: string };

function ClientForm({
  initial,
  submitLabel,
  pending,
  error,
  onSubmit,
  onCancel,
  stacked = false,
}: {
  initial?: Partial<NewClient>;
  submitLabel: string;
  pending: boolean;
  error: unknown;
  onSubmit: (values: NewClient) => void;
  onCancel?: () => void;
  stacked?: boolean;
}) {
  const { t } = useTranslation();
  const [form, setForm] = useState<ClientFormValues>({
    fullName: initial?.fullName ?? "",
    dateOfBirth: initial?.dateOfBirth ?? "",
    address: initial?.address ?? "",
    phone: initial?.phone ?? "",
    email: initial?.email ?? "",
    employer: initial?.employer ?? "",
  });
  const set = (name: keyof ClientFormValues) => (value: string) => setForm((current) => ({ ...current, [name]: value }));
  const blankToNull = (value: string) => value.trim() || null;
  const errors = error instanceof ApiError ? error.fieldErrors : {};
  const optional = t("clients.optional");

  function submit(event: FormEvent) {
    event.preventDefault();
    onSubmit({
      fullName: form.fullName.trim(),
      dateOfBirth: form.dateOfBirth || null,
      address: blankToNull(form.address),
      phone: blankToNull(form.phone),
      email: blankToNull(form.email),
      employer: blankToNull(form.employer),
    });
  }

  const field = (name: keyof ClientFormValues, label: string, props: { type?: string; maxLength?: number; hint?: string } = {}) => (
    <TextField
      label={label}
      type={props.type}
      value={form[name]}
      onChange={(event) => set(name)(event.target.value)}
      errors={errors[name]}
      hint={props.hint}
      maxLength={props.maxLength}
      autoComplete="off"
    />
  );

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      {!!error && Object.keys(errors).length === 0 && <Alert>{errorMessage(error)}</Alert>}
      {field("fullName", t("clients.fullName"), { maxLength: 200 })}
      <div className={cn("grid gap-4", !stacked && "md:grid-cols-2")}>
        {field("dateOfBirth", t("clients.dateOfBirth"), { type: "date", hint: optional })}
        {field("phone", t("clients.phone"), { type: "tel", maxLength: 32, hint: optional })}
        {field("email", t("clients.email"), { type: "email", maxLength: 254, hint: optional })}
        {field("employer", t("clients.employer"), { maxLength: 200, hint: optional })}
      </div>
      {field("address", t("clients.address"), { maxLength: 200, hint: optional })}
      <div className="flex gap-2">
        <Button type="submit" disabled={pending}>
          {submitLabel}
        </Button>
        {onCancel && (
          <Button type="button" variant="ghost" onClick={onCancel}>
            {t("actions.cancel")}
          </Button>
        )}
      </div>
    </form>
  );
}

export function NewClientPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const create = useMutation({
    mutationFn: createClient,
    onSuccess: async (client) => {
      void queryClient.invalidateQueries({ queryKey: ["clients"] });
      toast.success(t("clients.created", { number: client.clientNumber }));
      await navigate({ to: "/applications/new", search: { clientId: client.id } });
    },
  });

  return (
    <>
      <PageHeader title={t("clients.newClient")} />
      <div className="max-w-2xl rounded-md border bg-card p-6">
        <ClientForm submitLabel={t("clients.create")} pending={create.isPending} error={create.error} onSubmit={(values) => create.mutate(values)} />
      </div>
    </>
  );
}
