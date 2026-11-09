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

  function choose(language: Language) {
    if (language === current) return;
    void applyLanguage(language);
    if (saveOnAccount) save.mutate(language);
  }

  return (
    <div role="group" aria-label={t("language.label")} className="flex overflow-hidden rounded border text-xs font-semibold">
      {languages.map((language) => (
        <button
          key={language}
          type="button"
          lang={language}
          aria-pressed={language === current}
          onClick={() => choose(language)}
          className={cn(
            "px-2.5 py-1.5 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring",
            language === current ? "bg-primary text-primary-foreground" : "bg-card text-muted-foreground hover:text-foreground",
          )}
        >
          {language.toUpperCase()}
        </button>
      ))}
    </div>
  );
}
