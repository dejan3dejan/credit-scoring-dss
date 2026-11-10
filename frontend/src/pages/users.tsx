import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { ChevronLeft, Plus } from "lucide-react";
import { toast } from "sonner";
import { branchesKey, listBranches } from "@/api/branches";
import { ApiError, editedElsewhere } from "@/api/client";
import type { Language, Role } from "@/api/types";
import {
  createUser,
  deactivateUser,
  getUser,
  listUsers,
  reactivateUser,
  resetPassword,
  updateUser,
  userKey,
  usersKey,
  type UserFields,
  type UserResponse,
} from "@/api/users";
import { EmptyState, ErrorState, Loading, PageHeader } from "@/components/page";
import { Alert, Card, SelectField, Table, Td, Th } from "@/components/surface";
import { TextField } from "@/components/text-field";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { errorMessage, useLanguage } from "@/i18n";
import { formatDateTime } from "@/i18n/format";

const roles: Role[] = ["Officer", "Manager", "Risk", "Admin"];
const branchRoles: Role[] = ["Officer", "Manager"];

function fieldErrorsOf(error: unknown) {
  return error instanceof ApiError ? error.fieldErrors : {};
}

function GeneralError({ error }: { error: unknown }) {
  if (!error || Object.keys(fieldErrorsOf(error)).length > 0) return null;
  return <Alert>{errorMessage(error)}</Alert>;
}

function UserStatus({ user }: { user: UserResponse }) {
  const { t } = useTranslation();
  const language = useLanguage();
  if (!user.isActive) return <span className="text-muted-foreground">{t("users.deactivated")}</span>;
  if (user.lockedUntil) return <span className="text-reject">{t("users.locked", { time: formatDateTime(user.lockedUntil, language) })}</span>;
  if (user.mustChangePassword) return <span className="text-review">{t("users.mustChangePassword")}</span>;
  return <span>{t("users.active")}</span>;
}

export function UsersListPage() {
  const { t } = useTranslation();
  const language = useLanguage();
  const users = useQuery({ queryKey: usersKey, queryFn: listUsers });
  const [filter, setFilter] = useState("");
  const needle = filter.trim().toLowerCase();
  const shown = users.data?.filter(
    (user) => !needle || user.displayName.toLowerCase().includes(needle) || user.userName.includes(needle),
  );

  return (
    <>
      <PageHeader
        title={t("users.title")}
        actions={
          <Button asChild>
            <Link to="/users/new">
              <Plus aria-hidden="true" />
              {t("users.newUser")}
            </Link>
          </Button>
        }
      />
      <div className="grid w-80 gap-1.5">
        <label htmlFor="users-filter" className="text-sm font-medium">
          {t("users.filter")}
        </label>
        <Input
          id="users-filter"
          value={filter}
          onChange={(event) => setFilter(event.target.value)}
          placeholder={t("users.filterPlaceholder")}
          className="bg-card"
        />
      </div>
      {users.isPending ? (
        <Loading />
      ) : users.isError ? (
        <ErrorState error={users.error} onRetry={() => void users.refetch()} />
      ) : shown!.length === 0 ? (
        <EmptyState title={t("users.noResults")} />
      ) : (
        <Table label={t("users.title")}>
          <thead>
            <tr>
              <Th>{t("users.name")}</Th>
              <Th>{t("users.userName")}</Th>
              <Th>{t("users.role")}</Th>
              <Th>{t("users.branch")}</Th>
              <Th>{t("users.status")}</Th>
              <Th>{t("users.lastSignIn")}</Th>
            </tr>
          </thead>
          <tbody>
            {shown!.map((user) => (
              <tr key={user.id} className="group hover:bg-muted/50">
                <Td>
                  <Link to="/users/$id" params={{ id: user.id }} className="font-medium text-primary underline-offset-4 hover:underline">
                    {user.displayName}
                  </Link>
                </Td>
                <Td className="font-mono text-xs">{user.userName}</Td>
                <Td>{t(`roles.${user.role}`)}</Td>
                <Td>{user.branch?.name ?? <span className="text-muted-foreground">{t("users.noBranch")}</span>}</Td>
                <Td>
                  <UserStatus user={user} />
                </Td>
                <Td className="text-muted-foreground">
                  {user.lastSignedInAt ? formatDateTime(user.lastSignedInAt, language) : t("users.never")}
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </>
  );
}

type FormValues = { displayName: string; email: string; role: Role | ""; branchId: string; preferredLanguage: Language };

function UserFieldsForm({
  initial,
  extra,
  submitLabel,
  pending,
  error,
  onSubmit,
}: {
  initial: FormValues;
  extra?: (errors: ReturnType<typeof fieldErrorsOf>) => React.ReactNode;
  submitLabel: string;
  pending: boolean;
  error: unknown;
  onSubmit: (fields: UserFields) => void;
}) {
  const { t } = useTranslation();
  const branches = useQuery({ queryKey: branchesKey, queryFn: listBranches });
  const [form, setForm] = useState<FormValues>(initial);
  const needsBranch = form.role !== "" && branchRoles.includes(form.role);
  const errors = fieldErrorsOf(error);

  function submit(event: FormEvent) {
    event.preventDefault();
    onSubmit({
      displayName: form.displayName.trim(),
      email: form.email.trim() || null,
      role: form.role || null,
      branchId: needsBranch ? form.branchId || null : null,
      preferredLanguage: form.preferredLanguage,
    });
  }

  const activeBranches = (branches.data ?? []).filter((branch) => branch.isActive || branch.id === initial.branchId);

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      <GeneralError error={error} />
      {extra?.(errors)}
      <TextField
        label={t("users.displayName")}
        value={form.displayName}
        onChange={(event) => setForm({ ...form, displayName: event.target.value })}
        errors={errors.displayName}
        maxLength={100}
        autoComplete="off"
      />
      <TextField
        label={t("users.email")}
        type="email"
        value={form.email}
        onChange={(event) => setForm({ ...form, email: event.target.value })}
        errors={errors.email}
        hint={t("clients.optional")}
        maxLength={254}
        autoComplete="off"
      />
      <div className="grid gap-4 md:grid-cols-2">
        <SelectField
          label={t("users.role")}
          value={form.role}
          placeholder={t("newApplication.select")}
          options={roles.map((role) => ({ value: role, label: t(`roles.${role}`) }))}
          onChange={(event) => setForm({ ...form, role: event.target.value as Role | "" })}
          errors={errors.role}
        />
        <div className="grid gap-1.5">
          <SelectField
            label={t("users.branch")}
            value={needsBranch ? form.branchId : ""}
            placeholder={needsBranch ? t("newApplication.select") : t("users.noBranch")}
            options={needsBranch ? activeBranches.map((branch) => ({ value: branch.id, label: branch.name })) : []}
            onChange={(event) => setForm({ ...form, branchId: event.target.value })}
            disabled={!needsBranch}
            errors={errors.branchId}
          />
          <p className="text-xs text-muted-foreground">{t("users.branchHint")}</p>
        </div>
        <SelectField
          label={t("users.language")}
          value={form.preferredLanguage}
          options={(["en", "sr"] as const).map((value) => ({ value, label: t(`users.languages.${value}`) }))}
          onChange={(event) => setForm({ ...form, preferredLanguage: event.target.value as Language })}
          errors={errors.preferredLanguage}
        />
      </div>
      <div>
        <Button type="submit" disabled={pending}>
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}

export function NewUserPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const create = useMutation({
    mutationFn: (fields: UserFields) => createUser({ ...fields, userName: userName.trim(), temporaryPassword: password }),
    onSuccess: async (created) => {
      queryClient.setQueryData(userKey(created.id), created);
      void queryClient.invalidateQueries({ queryKey: usersKey });
      toast.success(t("users.created", { name: created.displayName }));
      await navigate({ to: "/users/$id", params: { id: created.id } });
    },
  });

  return (
    <>
      <BackToUsers />
      <PageHeader title={t("users.newUser")} />
      <div className="max-w-2xl rounded-md border bg-card p-6">
        <UserFieldsForm
          initial={{ displayName: "", email: "", role: "", branchId: "", preferredLanguage: "en" }}
          submitLabel={t("users.create")}
          pending={create.isPending}
          error={create.error}
          onSubmit={(fields) => create.mutate(fields)}
          extra={(errors) => (
            <>
              <TextField
                label={t("users.userName")}
                value={userName}
                onChange={(event) => setUserName(event.target.value)}
                errors={errors.userName}
                hint={t("users.userNameHint")}
                maxLength={64}
                autoComplete="off"
              />
              <TextField
                label={t("users.temporaryPassword")}
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                errors={errors.temporaryPassword}
                hint={t("users.temporaryPasswordHint")}
                maxLength={256}
                autoComplete="new-password"
              />
            </>
          )}
        />
      </div>
    </>
  );
}

function BackToUsers() {
  const { t } = useTranslation();
  return (
    <Link to="/users" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
      <ChevronLeft className="size-4" aria-hidden="true" />
      {t("users.back")}
    </Link>
  );
}

export function UserDetailsPage({ id }: { id: string }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const user = useQuery({ queryKey: userKey(id), queryFn: () => getUser(id) });
  if (user.isPending) return <Loading />;
  if (user.isError) return <ErrorState error={user.error} onRetry={() => void user.refetch()} />;
  const u = user.data;

  return (
    <>
      <div>
        <BackToUsers />
        <h1 className="mt-1 text-[22px] leading-tight font-semibold">{u.displayName}</h1>
        <p className="mt-1 text-muted-foreground">
          <span className="font-mono text-xs">{u.userName}</span> · <UserStatus user={u} /> · {t("users.lastSignIn")}:{" "}
          {u.lastSignedInAt ? formatDateTime(u.lastSignedInAt, language) : t("users.never")}
        </p>
      </div>
      <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_380px]">
        <Card title={t("users.details")}>
          <EditUser user={u} />
        </Card>
        <div className="grid gap-5">
          <PasswordCard user={u} />
          <AccountCard user={u} />
        </div>
      </div>
    </>
  );
}

function useUserUpdate() {
  const queryClient = useQueryClient();
  return (fresh: UserResponse) => {
    queryClient.setQueryData(userKey(fresh.id), fresh);
    void queryClient.invalidateQueries({ queryKey: usersKey, exact: true });
  };
}

function EditUser({ user }: { user: UserResponse }) {
  const { t } = useTranslation();
  const update = useUserUpdate();
  const queryClient = useQueryClient();
  const save = useMutation({
    mutationFn: (fields: UserFields) => updateUser(user.id, fields, user.version),
    onSuccess: (fresh) => {
      update(fresh);
      toast.success(t("users.saved"));
    },
    onError: (error) => {
      if (!editedElsewhere(error)) return;
      toast.error(errorMessage(error), { id: "edited_elsewhere" });
      save.reset();
      void queryClient.invalidateQueries({ queryKey: userKey(user.id) });
    },
  });
  return (
    <UserFieldsForm
      key={`${user.id}-${user.version}`}
      initial={{
        displayName: user.displayName,
        email: user.email ?? "",
        role: user.role,
        branchId: user.branch?.id ?? "",
        preferredLanguage: user.preferredLanguage,
      }}
      submitLabel={t("users.save")}
      pending={save.isPending}
      error={save.error}
      onSubmit={(fields) => save.mutate(fields)}
    />
  );
}

function PasswordCard({ user }: { user: UserResponse }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [password, setPassword] = useState("");
  const reset = useMutation({
    mutationFn: () => resetPassword(user.id, password),
    onSuccess: () => {
      setPassword("");
      void queryClient.invalidateQueries({ queryKey: usersKey });
      toast.success(t("users.passwordReset"));
    },
  });

  return (
    <Card title={t("users.passwordTitle")}>
      <form
        noValidate
        className="grid gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          reset.mutate();
        }}
      >
        <p className="text-muted-foreground">{t("users.passwordText")}</p>
        <GeneralError error={reset.error} />
        <TextField
          label={t("users.temporaryPassword")}
          type="password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          errors={fieldErrorsOf(reset.error).temporaryPassword}
          maxLength={256}
          autoComplete="new-password"
        />
        <div>
          <Button type="submit" variant="outline" disabled={reset.isPending}>
            {t("users.resetPassword")}
          </Button>
        </div>
      </form>
    </Card>
  );
}

function AccountCard({ user }: { user: UserResponse }) {
  const { t } = useTranslation();
  const update = useUserUpdate();
  const [confirming, setConfirming] = useState(false);
  const change = useMutation({
    mutationFn: () => (user.isActive ? deactivateUser(user.id) : reactivateUser(user.id)),
    onSuccess: (fresh) => {
      update(fresh);
      setConfirming(false);
      toast.success(t(fresh.isActive ? "users.reactivatedToast" : "users.deactivatedToast"));
    },
  });

  return (
    <Card title={t("users.statusTitle")}>
      <div className="grid gap-3">
        <GeneralError error={change.error} />
        {user.isActive ? (
          <>
            <p className="text-muted-foreground">{t("users.deactivateText")}</p>
            {confirming ? (
              <div className="flex gap-2">
                <Button variant="outline" className="border-reject text-reject" onClick={() => change.mutate()} disabled={change.isPending}>
                  {t("users.deactivateConfirm")}
                </Button>
                <Button variant="ghost" onClick={() => setConfirming(false)}>
                  {t("actions.cancel")}
                </Button>
              </div>
            ) : (
              <div>
                <Button variant="outline" onClick={() => setConfirming(true)}>
                  {t("users.deactivate")}
                </Button>
              </div>
            )}
          </>
        ) : (
          <div>
            <Button variant="outline" onClick={() => change.mutate()} disabled={change.isPending}>
              {t("users.reactivate")}
            </Button>
          </div>
        )}
      </div>
    </Card>
  );
}
