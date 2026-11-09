import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { ApiError } from "@/api/client";
import { changeModelRole, listAvailableModels, listModelEvents, listModels, modelsKey, registerModel } from "@/api/models";
import type { ModelRole, ScoringModel } from "@/api/types";
import { can, useMe } from "@/auth/session";
import { EmptyState, ErrorState, Loading, PageHeader } from "@/components/page";
import { Alert, Card, Facts, FieldErrors } from "@/components/surface";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { errorMessage, useLanguage } from "@/i18n";
import { formatDateTime, formatNumber, formatPercent, formatShortDate } from "@/i18n/format";
import { cn } from "@/lib/utils";

const costRatioThreshold = 1 / (1 + 2.0431);
const modelNames: Record<string, string> = { RandomForest: "Random forest", LogisticRegression: "Logistic regression", XGBoost: "XGBoost" };
const modelName = (name: string) => modelNames[name] ?? name;

export function ModelsPage() {
  const { t } = useTranslation();
  const me = useMe();
  const models = useQuery({ queryKey: modelsKey, queryFn: listModels });

  return (
    <>
      <PageHeader title={t("models.title")} description={t("models.intro")} />
      {models.isPending ? (
        <Loading />
      ) : models.isError ? (
        <ErrorState error={models.error} onRetry={() => void models.refetch()} />
      ) : (
        <>
          <ModelCard active={models.data.find((m) => m.role === "Active")} />
          {can(me, "models.manage") && <Registry models={models.data} />}
        </>
      )}
      <Limitations />
    </>
  );
}

function ModelCard({ active }: { active?: ScoringModel }) {
  const { t } = useTranslation();
  const language = useLanguage();
  if (!active) return <Alert>{t("models.noActive")}</Alert>;
  const percent = (value: number) => formatPercent(value, language);
  const isCostRatio = Math.abs(active.threshold - costRatioThreshold) < 5e-5;

  return (
    <div className="grid gap-4 md:grid-cols-2">
      <Card title={t("models.deployed")}>
        <Facts
          items={[
            { label: t("models.model"), value: <span className="font-mono text-xs">{active.modelId}</span> },
            { label: t("models.type"), value: modelName(active.displayName) },
            { label: t("models.output"), value: t("models.outputValue") },
            ...(active.reviewFrom !== null
              ? [{ label: t("models.reviewBand"), value: `${percent(active.reviewFrom)} ≤ P(bad) < ${percent(active.threshold)}` }]
              : []),
            { label: t("models.rejectRule"), value: `P(bad) ≥ ${percent(active.threshold)}` },
            { label: t("models.recommendationOnly"), value: t("models.recommendationOnlyValue") },
          ]}
        />
      </Card>
      <section className="rounded-md border bg-card">
        <h2 className="border-b px-4 py-3 text-sm font-semibold">{t("models.whyThreshold", { value: percent(active.threshold) })}</h2>
        <div className="grid gap-2 p-4">
          {isCostRatio ? (
            <>
              <p>{t("models.whyCost1")}</p>
              <p>{t("models.whyCost2")}</p>
              <p className="text-muted-foreground">{t("models.whyCost3")}</p>
            </>
          ) : (
            <p>{t("models.whyFixed")}</p>
          )}
        </div>
      </section>
      <Card title={t("models.training")}>
        <Facts
          items={[
            { label: t("models.dataset"), value: t("models.datasetValue") },
            { label: t("models.datasetId"), value: <span className="font-mono text-xs">{active.dataset}</span> },
            { label: t("models.trainingLoans"), value: t("models.trainingLoansValue") },
            { label: t("models.testedOn"), value: t("models.testedOnValue") },
            { label: t("models.currency"), value: t("models.currencyValue") },
          ]}
        />
      </Card>
      <Card title={t("models.inputs")}>
        <Facts
          items={[
            { label: t("models.loanAmount"), value: `${formatNumber(1000, language)} – ${formatNumber(35000, language)} USD` },
            { label: t("models.loanTerm"), value: t("models.loanTermValue") },
            { label: t("models.creditScore"), value: "300 – 850" },
            { label: t("models.whyRanges"), value: t("models.whyRangesValue") },
          ]}
        />
      </Card>
    </div>
  );
}

const roleTone: Record<ModelRole, string> = {
  Active: "bg-approve-tint text-approve",
  Shadow: "bg-review-tint text-review",
  Retired: "bg-none-tint text-none",
};

function Registry({ models }: { models: ScoringModel[] }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const percent = (value: number) => formatPercent(value, language);

  return (
    <>
      <Card title={t("models.registry")} footer={t("models.registryNote")}>
        <ul className="divide-y">
          {models.map((model) => (
            <li key={model.modelId} className="grid gap-3 py-3 first:pt-0 last:pb-0">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-medium">{modelName(model.displayName)}</span>
                <span className={cn("rounded px-2 py-0.5 text-xs font-semibold", roleTone[model.role])}>{t(`models.role.${model.role}`)}</span>
              </div>
              <div className="text-xs text-muted-foreground">
                <span className="font-mono">{model.modelId}</span> ·{" "}
                {model.reviewFrom !== null
                  ? t("models.meta", {
                      reviewFrom: percent(model.reviewFrom),
                      threshold: percent(model.threshold),
                      date: formatShortDate(model.registeredAt, language),
                    })
                  : t("models.metaNoBand", { threshold: percent(model.threshold), date: formatShortDate(model.registeredAt, language) })}
              </div>
              {model.role === "Shadow" && <RoleChange model={model} />}
            </li>
          ))}
        </ul>
      </Card>
      <Available />
      <ChangeLog />
    </>
  );
}

function RoleChange({ model }: { model: ScoringModel }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [reason, setReason] = useState("");
  const change = useMutation({
    mutationFn: (kind: "promote" | "retire") => changeModelRole(model.modelId, kind, reason.trim() || null),
    onSuccess: (models) => {
      queryClient.setQueryData(modelsKey, models);
      void queryClient.invalidateQueries({ queryKey: ["models", "events"] });
      toast.success(t("models.changed"));
    },
  });
  const fieldErrors = change.error instanceof ApiError ? change.error.fieldErrors : {};
  const errorId = `reason-${model.modelId}`;

  return (
    <div className="grid gap-1.5">
      <div className="flex flex-wrap gap-2">
        <Input
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          placeholder={t("models.reason")}
          aria-label={t("models.reasonFor", { model: model.modelId })}
          aria-invalid={!!fieldErrors.reason || undefined}
          aria-describedby={fieldErrors.reason ? errorId : undefined}
          maxLength={500}
          className="w-80 bg-card"
        />
        <Button variant="outline" onClick={() => change.mutate("promote")} disabled={change.isPending}>
          {t("models.promote")}
        </Button>
        <Button variant="outline" onClick={() => change.mutate("retire")} disabled={change.isPending}>
          {t("models.retire")}
        </Button>
      </div>
      <FieldErrors id={errorId} errors={[...(fieldErrors.reason ?? []), ...(fieldErrors.modelId ?? [])]} />
      {change.error && Object.keys(fieldErrors).length === 0 && <Alert>{errorMessage(change.error)}</Alert>}
    </div>
  );
}

function Available() {
  const { t } = useTranslation();
  const language = useLanguage();
  const queryClient = useQueryClient();
  const available = useQuery({ queryKey: ["models", "available"], queryFn: listAvailableModels, retry: false });
  const register = useMutation({
    mutationFn: registerModel,
    onSuccess: (model) => {
      void queryClient.invalidateQueries({ queryKey: ["models"] });
      toast.success(t("models.registered", { model: model.modelId }));
    },
  });

  return (
    <Card title={t("models.available")} footer={t("models.availableNote")}>
      {available.isPending ? (
        <Loading />
      ) : available.isError ? (
        <EmptyState title={t("models.availableError")} text={t("models.availableErrorText")} />
      ) : available.data.length === 0 ? (
        <EmptyState title={t("models.nothingNew")} text={t("models.nothingNewText")} />
      ) : (
        <ul className="divide-y">
          {available.data.map((model) => (
            <li key={model.modelId} className="flex flex-wrap items-center gap-3 py-3 first:pt-0 last:pb-0">
              <div>
                <div className="font-medium">{modelName(model.modelName)}</div>
                <div className="text-xs text-muted-foreground">
                  <span className="font-mono">{model.modelId}</span> ·{" "}
                  {t("models.availableMeta", {
                    threshold: formatPercent(model.recommendedThreshold, language),
                    date: formatShortDate(model.createdAt, language),
                  })}
                </div>
              </div>
              <Button variant="outline" className="ml-auto" onClick={() => register.mutate(model.modelId)} disabled={register.isPending}>
                {t("models.register")}
              </Button>
            </li>
          ))}
        </ul>
      )}
      {register.error && (
        <div className="mt-3">
          <Alert>{errorMessage(register.error)}</Alert>
        </div>
      )}
    </Card>
  );
}

function ChangeLog() {
  const { t } = useTranslation();
  const language = useLanguage();
  const events = useQuery({ queryKey: ["models", "events"], queryFn: listModelEvents });

  return (
    <Card title={t("models.log")}>
      {events.isPending ? (
        <Loading />
      ) : events.isError ? (
        <ErrorState error={events.error} onRetry={() => void events.refetch()} />
      ) : events.data.length === 0 ? (
        <EmptyState title={t("models.logEmpty")} text={t("models.logEmptyText")} />
      ) : (
        <ol className="grid gap-3">
          {events.data.map((event) => (
            <li key={event.id} className="grid gap-0.5">
              <div>
                <span className="font-mono text-xs">{event.modelId}</span>{" "}
                {event.fromRole
                  ? `${t(`models.role.${event.fromRole}`)} → ${t(`models.role.${event.toRole}`)}`
                  : t("models.logRegistered", { role: t(`models.role.${event.toRole}`).toLowerCase() })}
              </div>
              {event.reason && <div className="text-muted-foreground">“{event.reason}”</div>}
              <div className="text-xs text-muted-foreground">
                {event.changedBy} · {formatDateTime(event.changedAt, language)}
              </div>
            </li>
          ))}
        </ol>
      )}
    </Card>
  );
}

function Limitations() {
  const { t } = useTranslation();
  return (
    <Card title={t("models.limits")}>
      <ul className="grid list-disc gap-2 pl-5">
        {[1, 2, 3, 4, 5, 6].map((n) => (
          <li key={n}>
            <strong className="font-semibold">{t(`models.limit${n}Title`)}</strong> {t(`models.limit${n}`)}
          </li>
        ))}
      </ul>
    </Card>
  );
}
