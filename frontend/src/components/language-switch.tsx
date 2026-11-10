import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { setLanguage } from "@/api/auth";
import type { Language } from "@/api/types";
import { meKey } from "@/auth/session";
import { applyLanguage, errorMessage, languages, useLanguage } from "@/i18n";
import { cn } from "@/lib/utils";

export function LanguageSwitch({ saveOnAccount }: { saveOnAccount: boolean }) {
  const { t } = useTranslation();
  const current = useLanguage();
  const queryClient = useQueryClient();
  const save = useMutation({
    mutationFn: setLanguage,
    onSuccess: (me) => queryClient.setQueryData(meKey, me),
    onError: (error, language) => {
      void applyLanguage(language === "en" ? "sr" : "en");
      toast.error(errorMessage(error));
    },
  });

  function flip() {
    const next: Language = current === "en" ? "sr" : "en";
    void applyLanguage(next);
    if (saveOnAccount) save.mutate(next);
  }

  return (
    <button
      type="button"
      onClick={flip}
      disabled={save.isPending}
      aria-label={t("language.current")}
      title={t("language.switch")}
      className="flex rounded border bg-card p-0.5 text-xs font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
    >
      {languages.map((language) => (
        <span
          key={language}
          lang={language}
          aria-hidden="true"
          className={cn(
            "rounded-sm px-2 py-1",
            language === current ? "bg-primary text-primary-foreground" : "text-muted-foreground",
          )}
        >
          {language.toUpperCase()}
        </span>
      ))}
    </button>
  );
}
