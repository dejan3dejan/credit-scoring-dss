import { useState, type FormEvent } from "react";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Search } from "lucide-react";
import { toast } from "sonner";
import { branchesKey, listBranches } from "@/api/branches";
import { anonymiseClient, lookupClients, type ClientLookupItem, type LookupFilters } from "@/api/clients";
import { EmptyState, ErrorState, Loading, PageHeader } from "@/components/page";
import { Alert, Card, Table, Td, Th } from "@/components/surface";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { errorMessage, useLanguage } from "@/i18n";
import { formatDate } from "@/i18n/format";

const lookupKey = (filters: LookupFilters) => ["clients-lookup", filters] as const;

export function AnonymisePage() {
  const { t } = useTranslation();
  const [text, setText] = useState("");
  const [filters, setFilters] = useState<LookupFilters>({});
  const clients = useQuery({ queryKey: lookupKey(filters), queryFn: () => lookupClients(filters), placeholderData: keepPreviousData });
  const branches = useQuery({ queryKey: branchesKey, queryFn: listBranches });

  function submit(event: FormEvent) {
    event.preventDefault();
    setFilters({ search: text.trim() || undefined });
  }

  const codeOf = (branchId: string) => branches.data?.find((branch) => branch.id === branchId)?.code ?? "–";
  const page = clients.data?.page ?? 1;
  const last = clients.data ? Math.max(1, Math.ceil(clients.data.totalCount / clients.data.pageSize)) : 1;

  return (
    <>
      <PageHeader title={t("anonymise.title")} />
      <form onSubmit={submit} role="search" className="flex items-end gap-2">
        <div className="grid gap-1.5">
          <label htmlFor="anonymise-search" className="text-sm font-medium">
            {t("anonymise.search")}
          </label>
          <Input id="anonymise-search" value={text} onChange={(event) => setText(event.target.value)} className="w-80 bg-card" />
        </div>
        <Button type="submit" variant="outline">
          <Search aria-hidden="true" />
          {t("list.searchButton")}
        </Button>
      </form>
      {clients.isPending ? (
        <Loading />
      ) : clients.isError ? (
        <ErrorState error={clients.error} onRetry={() => void clients.refetch()} />
      ) : clients.data.items.length === 0 ? (
        <EmptyState title={t("newApplication.noClients")} />
      ) : (
        <>
          <Table label={t("anonymise.clients")}>
            <thead>
              <tr>
                <Th>{t("clients.number")}</Th>
                <Th>{t("clients.name")}</Th>
                <Th>{t("anonymise.branch")}</Th>
                <Th>{t("anonymise.status")}</Th>
                <Th />
              </tr>
            </thead>
            <tbody>
              {clients.data.items.map((client) => (
                <ClientRow key={client.id} client={client} branchCode={codeOf(client.branchId)} filters={filters} />
              ))}
            </tbody>
          </Table>
          {last > 1 && (
            <div className="flex items-center gap-3 text-sm text-muted-foreground">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setFilters({ ...filters, page: page - 1 })}>
                {t("anonymise.previous")}
              </Button>
              <span>{t("anonymise.page", { page, last })}</span>
              <Button variant="outline" size="sm" disabled={page >= last} onClick={() => setFilters({ ...filters, page: page + 1 })}>
                {t("anonymise.next")}
              </Button>
            </div>
          )}
        </>
      )}
    </>
  );
}

function ClientRow({ client, branchCode, filters }: { client: ClientLookupItem; branchCode: string; filters: LookupFilters }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const queryClient = useQueryClient();
  const [confirming, setConfirming] = useState(false);
  const [typed, setTyped] = useState("");
  const matches = typed.trim().toUpperCase() === client.clientNumber.toUpperCase();
  const anonymise = useMutation({
    mutationFn: () => anonymiseClient(client.id),
    onSuccess: (fresh) => {
      queryClient.setQueryData(lookupKey(filters), (current: { items: ClientLookupItem[] } | undefined) =>
        current && {
          ...current,
          items: current.items.map((item) =>
            item.id === fresh.id ? { ...item, fullName: fresh.fullName, isActive: fresh.isActive, anonymisedAt: fresh.anonymisedAt } : item,
          ),
        },
      );
      setConfirming(false);
      toast.success(t("anonymise.done", { number: client.clientNumber }));
    },
  });
  const label = `${t("anonymise.action")} ${client.fullName}`;

  return (
    <>
      <tr className="group">
        <Td className="font-mono text-xs">{client.clientNumber}</Td>
        <Td className="font-medium">{client.fullName}</Td>
        <Td className="font-mono text-xs">{branchCode}</Td>
        <Td className="text-muted-foreground">
          {client.anonymisedAt
            ? t("anonymise.anonymisedOn", { date: formatDate(client.anonymisedAt, language) })
            : t(client.isActive ? "clients.active" : "clients.inactive")}
        </Td>
        <Td align="right">
          {!client.anonymisedAt && !confirming && (
            <Button size="sm" variant="outline" aria-label={label} onClick={() => setConfirming(true)}>
              {t("anonymise.action")}
            </Button>
          )}
        </Td>
      </tr>
      {confirming && (
        <tr>
          <td colSpan={5} className="border-b px-4 pb-4">
            <Card>
              <div role="group" aria-label={label} className="grid gap-3">
                <Alert>{t("anonymise.warning")}</Alert>
                <div className="grid gap-1.5">
                  <label htmlFor={`confirm-${client.id}`} className="text-sm font-medium">
                    {t("anonymise.confirmLabel", { number: client.clientNumber })}
                  </label>
                  <Input
                    id={`confirm-${client.id}`}
                    value={typed}
                    onChange={(event) => setTyped(event.target.value)}
                    autoComplete="off"
                    className="w-60 bg-card"
                    autoFocus
                  />
                </div>
                {anonymise.isError && <Alert>{errorMessage(anonymise.error)}</Alert>}
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    className="border-reject text-reject"
                    disabled={!matches || anonymise.isPending}
                    onClick={() => anonymise.mutate()}
                  >
                    {t("anonymise.confirm")}
                  </Button>
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setConfirming(false);
                      setTyped("");
                      anonymise.reset();
                    }}
                  >
                    {t("actions.cancel")}
                  </Button>
                </div>
              </div>
            </Card>
          </td>
        </tr>
      )}
    </>
  );
}
