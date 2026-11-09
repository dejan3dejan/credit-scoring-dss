import { useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { changePassword, getMe } from "@/api/auth";
import { ApiError } from "@/api/client";
import { meKey, useMe } from "@/auth/session";
import { PageHeader } from "@/components/page";
import { TextField } from "@/components/text-field";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/i18n";

export function ChangePasswordPage() {
  const { t } = useTranslation();
  const me = useMe();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");

  const change = useMutation({
    mutationFn: async () => {
      await changePassword(current, next);
      return getMe();
    },
    onSuccess: async (fresh) => {
      queryClient.setQueryData(meKey, fresh);
      toast.success(t("password.done"));
      await navigate({ to: "/" });
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    change.mutate();
  }

  const fieldErrors = change.error instanceof ApiError ? change.error.fieldErrors : {};
  const generalError = change.error && Object.keys(fieldErrors).length === 0;

  return (
    <>
      <PageHeader title={t("password.title")} description={me.mustChangePassword ? t("password.required") : undefined} />
      <form onSubmit={submit} noValidate className="grid max-w-md gap-4 rounded-md border bg-card p-6">
        {generalError && (
          <p role="alert" className="rounded border border-reject/30 bg-reject-tint px-3 py-2 text-reject">
            {errorMessage(change.error)}
          </p>
        )}
        <TextField
          label={t("password.current")}
          type="password"
          autoComplete="current-password"
          value={current}
          onChange={(event) => setCurrent(event.target.value)}
          errors={fieldErrors.currentPassword}
        />
        <TextField
          label={t("password.new")}
          type="password"
          autoComplete="new-password"
          hint={t("password.hint")}
          value={next}
          onChange={(event) => setNext(event.target.value)}
          errors={fieldErrors.newPassword}
        />
        <div>
          <Button type="submit" disabled={change.isPending}>
            {t("password.submit")}
          </Button>
        </div>
      </form>
    </>
  );
}
