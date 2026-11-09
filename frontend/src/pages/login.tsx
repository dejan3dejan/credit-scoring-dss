import { useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { login } from "@/api/auth";
import { ApiError } from "@/api/client";
import { meKey } from "@/auth/session";
import { BrandMark } from "@/components/brand-mark";
import { LanguageSwitch } from "@/components/language-switch";
import { TextField } from "@/components/text-field";
import { Button } from "@/components/ui/button";
import { applyLanguage, errorMessage } from "@/i18n";
import { safeRedirect } from "@/lib/redirect";

export function LoginPage({ redirect }: { redirect?: string }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");

  const signIn = useMutation({
    mutationFn: () => login(userName, password),
    onSuccess: async (me) => {
      queryClient.setQueryData(meKey, me);
      await applyLanguage(me.preferredLanguage);
      await navigate({ href: safeRedirect(redirect) });
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    signIn.mutate();
  }

  const fieldErrors = signIn.error instanceof ApiError ? signIn.error.fieldErrors : {};

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex h-14 items-center justify-end px-6">
        <LanguageSwitch saveOnAccount={false} />
      </header>
      <main className="flex flex-1 items-start justify-center px-4 pt-[12vh]">
        <div className="w-full max-w-sm">
          <div className="mb-6 flex items-center gap-2.5">
            <BrandMark size={28} />
            <div>
              <div className="text-lg leading-tight font-semibold">{t("app.name")}</div>
              <div className="text-xs text-muted-foreground">{t("app.tagline")}</div>
            </div>
          </div>
          <form onSubmit={submit} noValidate className="grid gap-4 rounded-md border bg-card p-6">
            <h1 className="text-lg font-semibold">{t("login.title")}</h1>
            {signIn.error && (
              <p role="alert" className="rounded border border-reject/30 bg-reject-tint px-3 py-2 text-reject">
                {errorMessage(signIn.error)}
              </p>
            )}
            <TextField
              label={t("login.userName")}
              name="userName"
              autoComplete="username"
              value={userName}
              onChange={(event) => setUserName(event.target.value)}
              errors={fieldErrors.userName}
              autoFocus
            />
            <TextField
              label={t("login.password")}
              name="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              errors={fieldErrors.password}
            />
            <Button type="submit" disabled={signIn.isPending} className="mt-1">
              {t("login.submit")}
            </Button>
          </form>
        </div>
      </main>
    </div>
  );
}
