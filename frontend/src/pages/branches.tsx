import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { branchesKey, createBranch, deactivateBranch, listBranches, reactivateBranch, updateBranch, type Branch } from "@/api/branches";
import { ApiError } from "@/api/client";
import { ErrorState, Loading, PageHeader } from "@/components/page";
import { Alert, Card, Table, Td, Th } from "@/components/surface";
import { TextField } from "@/components/text-field";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { errorMessage, fieldErrorMessage } from "@/i18n";

function fieldErrorsOf(error: unknown) {
  return error instanceof ApiError ? error.fieldErrors : {};
}

export function BranchesPage() {
  const { t } = useTranslation();
  const branches = useQuery({ queryKey: branchesKey, queryFn: listBranches });

  return (
    <>
      <PageHeader title={t("branches.title")} />
      <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_360px]">
        {branches.isPending ? (
          <Loading />
        ) : branches.isError ? (
          <ErrorState error={branches.error} onRetry={() => void branches.refetch()} />
        ) : (
          <Table label={t("branches.title")}>
            <thead>
              <tr>
                <Th>{t("branches.code")}</Th>
                <Th>{t("branches.name")}</Th>
                <Th>{t("branches.city")}</Th>
                <Th>{t("branches.status")}</Th>
                <Th />
              </tr>
            </thead>
            <tbody>
              {branches.data.map((branch) => (
                <BranchRow key={branch.id} branch={branch} />
              ))}
            </tbody>
          </Table>
        )}
        <NewBranch />
      </div>
    </>
  );
}

function useBranchUpdate() {
  const queryClient = useQueryClient();
  return (fresh: Branch) =>
    queryClient.setQueryData(branchesKey, (current: Branch[] | undefined) =>
      current?.map((branch) => (branch.id === fresh.id ? fresh : branch)),
    );
}

function BranchRow({ branch }: { branch: Branch }) {
  const { t } = useTranslation();
  const update = useBranchUpdate();
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(branch.name);
  const [city, setCity] = useState(branch.city ?? "");
  const save = useMutation({
    mutationFn: () => updateBranch(branch.id, { name: name.trim(), city: city.trim() || null }),
    onSuccess: (fresh) => {
      update(fresh);
      setEditing(false);
      toast.success(t("branches.saved"));
    },
  });
  const status = useMutation({
    mutationFn: () => (branch.isActive ? deactivateBranch(branch.id) : reactivateBranch(branch.id)),
    onSuccess: (fresh) => {
      update(fresh);
      toast.success(t(fresh.isActive ? "branches.reactivated" : "branches.deactivated"));
    },
  });
  const error = save.error ?? status.error;
  const errors = fieldErrorsOf(save.error);

  return (
    <>
      <tr className="group">
        <Td className="font-mono text-xs">{branch.code}</Td>
        {editing ? (
          <>
            <Td>
              <Input aria-label={t("branches.name")} value={name} onChange={(event) => setName(event.target.value)} maxLength={100} aria-invalid={!!errors.name || undefined} />
            </Td>
            <Td>
              <Input aria-label={t("branches.city")} value={city} onChange={(event) => setCity(event.target.value)} maxLength={100} />
            </Td>
          </>
        ) : (
          <>
            <Td>{branch.name}</Td>
            <Td className="text-muted-foreground">{branch.city ?? "–"}</Td>
          </>
        )}
        <Td className={branch.isActive ? "" : "text-muted-foreground"}>{t(branch.isActive ? "branches.active" : "branches.inactive")}</Td>
        <Td align="right" className="whitespace-nowrap">
          {editing ? (
            <span className="inline-flex gap-2">
              <Button size="sm" onClick={() => save.mutate()} disabled={save.isPending}>
                {t("branches.save")}
              </Button>
              <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
                {t("actions.cancel")}
              </Button>
            </span>
          ) : (
            <span className="inline-flex gap-2">
              <Button size="sm" variant="outline" onClick={() => setEditing(true)}>
                {t("branches.edit")}
              </Button>
              <Button size="sm" variant="outline" onClick={() => status.mutate()} disabled={status.isPending}>
                {t(branch.isActive ? "branches.deactivate" : "branches.reactivate")}
              </Button>
            </span>
          )}
        </Td>
      </tr>
      {error && (
        <tr>
          <td colSpan={5} className="border-b px-4 pb-3">
            <Alert>{Object.keys(errors).length > 0 ? Object.values(errors).flat().map(fieldErrorMessage).join(" ") : errorMessage(error)}</Alert>
          </td>
        </tr>
      )}
    </>
  );
}

function NewBranch() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [city, setCity] = useState("");
  const create = useMutation({
    mutationFn: () => createBranch({ code: code.trim(), name: name.trim(), city: city.trim() || null }),
    onSuccess: (branch) => {
      void queryClient.invalidateQueries({ queryKey: branchesKey });
      setCode("");
      setName("");
      setCity("");
      toast.success(t("branches.created", { code: branch.code }));
    },
  });
  const errors = fieldErrorsOf(create.error);

  function submit(event: FormEvent) {
    event.preventDefault();
    create.mutate();
  }

  return (
    <Card title={t("branches.newBranch")}>
      <form onSubmit={submit} noValidate className="grid gap-3">
        {create.error && Object.keys(errors).length === 0 && <Alert>{errorMessage(create.error)}</Alert>}
        <TextField label={t("branches.code")} value={code} onChange={(event) => setCode(event.target.value)} errors={errors.code} hint={t("branches.codeHint")} maxLength={16} autoComplete="off" />
        <TextField label={t("branches.name")} value={name} onChange={(event) => setName(event.target.value)} errors={errors.name} maxLength={100} autoComplete="off" />
        <TextField label={t("branches.city")} value={city} onChange={(event) => setCity(event.target.value)} errors={errors.city} hint={t("clients.optional")} maxLength={100} autoComplete="off" />
        <div>
          <Button type="submit" disabled={create.isPending}>
            {t("branches.create")}
          </Button>
        </div>
      </form>
    </Card>
  );
}
