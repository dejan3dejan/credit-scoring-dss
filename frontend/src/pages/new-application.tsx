import { useState, type FormEvent, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { Search } from "lucide-react";
import { toast } from "sonner";
import { applicationKey, createApplication, getApplication, type NewApplication } from "@/api/applications";
import { ApiError } from "@/api/client";
import { clientKey, getClient, listClients } from "@/api/clients";
import { discardDraft, draftKey, getDraft, type DraftResponse } from "@/api/drafts";
import {
  employmentLengths,
  homeOwnerships,
  incomeVerifications,
  loanPurposes,
  type ApplicationAnswers,
  type ApplicationResponse,
  type ClientDetail,
  type FieldError,
  type Language,
} from "@/api/types";
import { can, useMe } from "@/auth/session";
import { EmptyState, ErrorState, Loading, PageHeader } from "@/components/page";
import { Alert, Card, Facts, FieldErrors, SelectField } from "@/components/surface";
import { TextField } from "@/components/text-field";
import { Button } from "@/components/ui/button";
import { Dialog, DialogClose, DialogContent, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { errorMessage, useLanguage } from "@/i18n";
import { formatDateTime, formatShortDate, formatTime } from "@/i18n/format";
import { formatAnswer } from "@/lib/application-inputs";
import { clientName } from "@/lib/client-name";
import { parseNumber } from "@/lib/numbers";
import { useDraftAutosave, type AutosaveState } from "@/lib/use-draft-autosave";
import { cn } from "@/lib/utils";

type Answers = Record<keyof ApplicationAnswers, string>;

const numberFields = [
  "loanAmount",
  "annualIncome",
  "debtToIncomePercent",
  "creditScore",
  "inquiriesLast6Months",
  "delinquenciesLast2Years",
  "publicRecords",
  "bankruptcies",
  "revolvingBalance",
  "revolvingUtilizationPercent",
  "openAccounts",
  "totalAccounts",
  "mortgageAccounts",
] as const;

const emptyAnswers: Answers = {
  loanAmount: "",
  purpose: "",
  employmentLength: "",
  homeOwnership: "",
  annualIncome: "",
  incomeVerification: "",
  debtToIncomePercent: "",
  creditScore: "",
  earliestCreditLine: "",
  inquiriesLast6Months: "",
  delinquenciesLast2Years: "",
  publicRecords: "",
  bankruptcies: "",
  revolvingBalance: "",
  revolvingUtilizationPercent: "",
  openAccounts: "",
  totalAccounts: "",
  mortgageAccounts: "",
};

const reviewSections: { title: string; fields: (keyof ApplicationAnswers)[] }[] = [
  { title: "newApplication.sectionLoan", fields: ["loanAmount", "purpose"] },
  {
    title: "newApplication.sectionIncome",
    fields: ["annualIncome", "incomeVerification", "debtToIncomePercent", "employmentLength", "homeOwnership", "mortgageAccounts"],
  },
  {
    title: "newApplication.sectionReport",
    fields: ["creditScore", "earliestCreditLine", "inquiriesLast6Months", "delinquenciesLast2Years", "publicRecords", "bankruptcies"],
  },
  { title: "newApplication.sectionAccounts", fields: ["revolvingBalance", "revolvingUtilizationPercent", "openAccounts", "totalAccounts"] },
];

const pascal = (name: string) => name[0].toUpperCase() + name.slice(1);

function answersFrom(application: ApplicationResponse): Answers {
  const answers = { ...emptyAnswers };
  for (const key of Object.keys(emptyAnswers) as (keyof ApplicationAnswers)[]) answers[key] = String(application[key]);
  answers.earliestCreditLine = application.earliestCreditLine.slice(0, 7);
  return answers;
}

function answersFromDraft(draft: DraftResponse): Answers {
  const answers = { ...emptyAnswers };
  for (const key of Object.keys(emptyAnswers) as (keyof ApplicationAnswers)[]) {
    const value = draft.answers[key];
    answers[key] = value === null || value === undefined ? "" : String(value);
  }
  answers.earliestCreditLine = draft.answers.earliestCreditLine?.slice(0, 7) ?? "";
  return answers;
}

function toRequest(clientId: string, answers: Answers, language: Language): NewApplication {
  const request: Record<string, unknown> = { clientId };
  for (const [key, value] of Object.entries(answers)) request[key] = value === "" ? null : value;
  for (const key of numberFields) request[key] = parseNumber(answers[key], language);
  request.earliestCreditLine = answers.earliestCreditLine ? `${answers.earliestCreditLine}-01` : null;
  return request as NewApplication;
}

export function NewApplicationPage({ clientId, from, draftId }: { clientId?: string; from?: string; draftId?: string }) {
  const { t } = useTranslation();
  return (
    <>
      <PageHeader title={t("newApplication.title")} />
      {clientId || draftId ? <FormLoader clientId={clientId} from={from} draftId={draftId} /> : <ClientPicker />}
    </>
  );
}

function FormLoader({ clientId, from, draftId }: { clientId?: string; from?: string; draftId?: string }) {
  const { t } = useTranslation();
  const draft = useQuery({ queryKey: draftKey(draftId ?? ""), queryFn: () => getDraft(draftId!), enabled: !!draftId });
  const closed = !!draft.data && draft.data.status !== "Open";
  const resolvedClientId = clientId ?? draft.data?.clientId;
  const client = useQuery({
    queryKey: clientKey(resolvedClientId ?? ""),
    queryFn: () => getClient(resolvedClientId!),
    enabled: !!resolvedClientId && !closed,
  });
  const previous = useQuery({
    queryKey: applicationKey(from ?? ""),
    queryFn: () => getApplication(from!),
    enabled: !!from,
  });

  if (draftId && draft.isPending) return <Loading />;
  if (draftId && draft.isError) return <ErrorState error={draft.error} onRetry={() => void draft.refetch()} />;
  if (draft.data && closed)
    return (
      <EmptyState
        title={t(draft.data.status === "Submitted" ? "drafts.closedSubmitted" : "drafts.closedDiscarded")}
        action={
          draft.data.applicationId && (
            <Button asChild variant="outline">
              <Link to="/applications/$id" params={{ id: draft.data.applicationId }}>
                {t("drafts.openApplication")}
              </Link>
            </Button>
          )
        }
      />
    );
  if (client.isPending || (from && previous.isPending)) return <Loading />;
  if (client.isError) return <ErrorState error={client.error} onRetry={() => void client.refetch()} />;
  if (from && previous.isError) return <ErrorState error={previous.error} onRetry={() => void previous.refetch()} />;
  const prefill = !draft.data && from && previous.data?.clientId === client.data.id ? previous.data : undefined;
  return <ApplicationForm client={client.data} draft={draft.data} prefill={prefill} />;
}

function ClientPicker() {
  const { t } = useTranslation();
  const me = useMe();
  const language = useLanguage();
  const [text, setText] = useState("");
  const [search, setSearch] = useState<string | null>(null);
  const results = useQuery({
    queryKey: ["clients", { search }],
    queryFn: () => listClients({ search: search ?? undefined }),
    enabled: search !== null,
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    setSearch(text.trim());
  }

  return (
    <Card title={t("newApplication.pickClient")}>
      <div className="grid gap-4">
        <div className="flex flex-wrap items-end gap-2">
          <form onSubmit={submit} className="grid gap-1.5" role="search">
            <label htmlFor="client-search" className="text-sm font-medium">
              {t("newApplication.searchClients")}
            </label>
            <div className="flex gap-2">
              <Input
                id="client-search"
                value={text}
                onChange={(event) => setText(event.target.value)}
                placeholder={t("newApplication.searchClientsPlaceholder")}
                className="w-80 bg-card"
                autoFocus
              />
              <Button type="submit" variant="outline">
                <Search aria-hidden="true" />
                {t("list.searchButton")}
              </Button>
            </div>
          </form>
          {can(me, "clients.write") && (
            <Button asChild variant="outline" className="ml-auto">
              <Link to="/clients/new">{t("newApplication.newClient")}</Link>
            </Button>
          )}
        </div>
        {search !== null &&
          (results.isPending ? (
            <Loading />
          ) : results.isError ? (
            <ErrorState error={results.error} onRetry={() => void results.refetch()} />
          ) : results.data.items.length === 0 ? (
            <EmptyState title={t("newApplication.noClients")} />
          ) : (
            <ul className="divide-y rounded-md border">
              {results.data.items.map((client) => (
                <li key={client.id} className="flex items-center gap-4 px-4 py-3">
                  <div className="min-w-0">
                    <div className="font-medium">{clientName(client, t)}</div>
                    <div className="text-xs text-muted-foreground">
                      <span className="font-mono">{client.clientNumber}</span>
                      {client.dateOfBirth && ` · ${formatShortDate(client.dateOfBirth, language, "UTC")}`}
                    </div>
                  </div>
                  {client.isActive ? (
                    <Button asChild size="sm" variant="outline" className="ml-auto">
                      <Link
                        to="/applications/new"
                        search={{ clientId: client.id }}
                        aria-label={`${t("newApplication.choose")} ${clientName(client, t)}`}
                      >
                        {t("newApplication.choose")}
                      </Link>
                    </Button>
                  ) : (
                    <span className="ml-auto text-xs text-muted-foreground">{t("clients.inactive")}</span>
                  )}
                </li>
              ))}
            </ul>
          ))}
      </div>
    </Card>
  );
}

function ApplicationForm({ client, prefill, draft }: { client: ClientDetail; prefill?: ApplicationResponse; draft?: DraftResponse }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [answers, setAnswers] = useState<Answers>(() =>
    draft ? answersFromDraft(draft) : prefill ? answersFrom(prefill) : emptyAnswers,
  );
  const [confirmDiscard, setConfirmDiscard] = useState(false);
  const [reviewing, setReviewing] = useState(false);

  const autosave = useDraftAutosave(draft, (saved) => {
    void navigate({ to: "/applications/new", search: (current: { clientId?: string; from?: string }) => ({ ...current, draftId: saved.id }), replace: true });
  });

  const discard = useMutation({
    mutationFn: async () => {
      const draftId = await autosave.beforeSubmit();
      return discardDraft(draftId!);
    },
    onSuccess: async () => {
      autosave.submitted();
      void queryClient.invalidateQueries({ queryKey: ["work"] });
      toast.success(t("drafts.discarded"));
      await navigate({ to: "/" });
    },
    onError: () => autosave.resume(),
  });

  const submit = useMutation({
    mutationFn: async () => {
      const draftId = await autosave.beforeSubmit();
      return createApplication({ ...toRequest(client.id, answers, language), ...(draftId ? { draftId } : {}) });
    },
    onSuccess: async (created) => {
      autosave.submitted();
      queryClient.setQueryData(applicationKey(created.id), created);
      void queryClient.invalidateQueries({ queryKey: ["applications"] });
      void queryClient.invalidateQueries({ queryKey: ["work"] });
      void queryClient.invalidateQueries({ queryKey: clientKey(client.id) });
      toast.success(t("newApplication.submitted", { reference: created.reference }));
      await navigate({ to: "/applications/$id", params: { id: created.id } });
    },
    onError: () => {
      setReviewing(false);
      autosave.resume();
    },
  });

  const saveError = autosave.state.status === "failed" ? autosave.state.error : null;
  const lastError = submit.error ?? discard.error;
  const errorSource = lastError ?? saveError;
  const errors = errorSource instanceof ApiError ? errorSource.fieldErrors : {};
  const fieldErrors = (name: string): FieldError[] | undefined => {
    const list = [...(errors[name] ?? []), ...(errors[`$.${name}`] ?? [])];
    return list.length ? list : undefined;
  };
  const generalError = lastError && Object.keys(errors).length === 0;

  const set = (name: keyof Answers) => (value: string) => {
    const next = { ...answers, [name]: value };
    setAnswers(next);
    if (client.isActive) autosave.schedule(toRequest(client.id, next, language));
  };

  const text = (name: keyof Answers, props: { suffix?: string; hint?: string; inputMode?: "numeric" | "decimal" } = {}) => (
    <TextField
      label={t(`fields.${pascal(name)}`)}
      value={answers[name]}
      onChange={(event) => set(name)(event.target.value)}
      inputMode={props.inputMode ?? "numeric"}
      suffix={props.suffix}
      hint={props.hint}
      errors={fieldErrors(name)}
      autoComplete="off"
    />
  );

  const select = (name: keyof Answers, enumName: string, values: readonly string[]) => (
    <SelectField
      label={t(`fields.${pascal(name)}`)}
      value={answers[name]}
      onChange={(event) => set(name)(event.target.value)}
      placeholder={t("newApplication.select")}
      options={values.map((value) => ({ value, label: t(`enums.${enumName}.${value}`) }))}
      errors={fieldErrors(name)}
    />
  );

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    setReviewing(true);
  }

  return (
    <form onSubmit={onSubmit} noValidate className="grid gap-5">
      <Card>
        <div className="flex flex-wrap items-center gap-4">
          <div>
            <div className="text-xs text-muted-foreground">{t("newApplication.client")}</div>
            <div className="font-semibold">{clientName(client, t)}</div>
            <div className="font-mono text-xs text-muted-foreground">{client.clientNumber}</div>
          </div>
          <Link to="/applications/new" search={{}} className="ml-auto text-sm text-primary underline-offset-4 hover:underline">
            {t("newApplication.changeClient")}
          </Link>
        </div>
        {!client.isActive && (
          <div className="mt-3">
            <Alert>{t("newApplication.inactiveClient")}</Alert>
          </div>
        )}
      </Card>
      {prefill && (
        <p className="rounded border border-review/30 bg-review-tint px-3 py-2">
          {t("newApplication.prefilled", { reference: prefill.reference })}
        </p>
      )}
      {generalError && <Alert>{errorMessage(lastError)}</Alert>}
      <FieldErrors id="client-error" errors={fieldErrors("clientId")} />

      <Section title={t("newApplication.sectionLoan")}>
        {text("loanAmount", { suffix: "USD", hint: t("newApplication.termHint") })}
        {select("purpose", "LoanPurpose", loanPurposes)}
      </Section>

      <Section title={t("newApplication.sectionIncome")}>
        {text("annualIncome", { suffix: "USD", hint: t("newApplication.incomeHint"), inputMode: "decimal" })}
        <fieldset className="grid gap-1.5">
          <legend className="mb-1.5 text-sm font-medium">{t("fields.IncomeVerification")}</legend>
          <div className="flex flex-wrap gap-2">
            {incomeVerifications.map((value) => (
              <label
                key={value}
                className={cn(
                  "flex cursor-pointer items-center gap-2 rounded-md border px-3 py-1.5 has-focus-visible:ring-[3px] has-focus-visible:ring-ring/50",
                  answers.incomeVerification === value ? "border-foreground bg-muted" : "bg-card",
                )}
              >
                <input
                  type="radio"
                  name="incomeVerification"
                  value={value}
                  checked={answers.incomeVerification === value}
                  onChange={() => set("incomeVerification")(value)}
                  className="accent-foreground"
                />
                {t(`enums.IncomeVerification.${value}`)}
              </label>
            ))}
          </div>
          <p className="text-xs text-muted-foreground">{t("newApplication.verificationHint")}</p>
          <FieldErrors id="verification-error" errors={fieldErrors("incomeVerification")} />
        </fieldset>
        {text("debtToIncomePercent", { suffix: "%", hint: t("newApplication.dtiHint"), inputMode: "decimal" })}
        {select("employmentLength", "EmploymentLength", employmentLengths)}
        {select("homeOwnership", "HomeOwnership", homeOwnerships)}
        {text("mortgageAccounts")}
      </Section>

      <Section title={t("newApplication.sectionReport")}>
        {text("creditScore", { hint: t("newApplication.creditScoreHint", { min: 300, max: 850 }) })}
        <TextField
          label={t("fields.EarliestCreditLine")}
          type="month"
          value={answers.earliestCreditLine}
          onChange={(event) => set("earliestCreditLine")(event.target.value)}
          hint={t("newApplication.creditLineHint")}
          errors={fieldErrors("earliestCreditLine")}
        />
        {text("inquiriesLast6Months")}
        {text("delinquenciesLast2Years", { hint: t("newApplication.delinquenciesHint") })}
        {text("publicRecords")}
        {text("bankruptcies")}
      </Section>

      <Section title={t("newApplication.sectionAccounts")}>
        {text("revolvingBalance", { suffix: "USD", hint: t("newApplication.revolvingBalanceHint"), inputMode: "decimal" })}
        {text("revolvingUtilizationPercent", { suffix: "%", hint: t("newApplication.utilisationHint"), inputMode: "decimal" })}
        {text("openAccounts", { hint: t("newApplication.openHint") })}
        {text("totalAccounts")}
      </Section>

      <div className="sticky bottom-0 -mx-6 flex items-center gap-3 border-t bg-card px-6 py-3">
        <Button type="submit" disabled={submit.isPending || !client.isActive}>
          {t("newApplication.review")}
        </Button>
        <SaveStatus state={autosave.state} onRetry={() => void autosave.retry()} />
        {autosave.draftId &&
          (confirmDiscard ? (
            <span className="ml-auto flex gap-2">
              <Button
                type="button"
                variant="outline"
                className="border-reject text-reject"
                onClick={() => discard.mutate()}
                disabled={discard.isPending}
              >
                {t("drafts.discardConfirm")}
              </Button>
              <Button type="button" variant="ghost" onClick={() => setConfirmDiscard(false)}>
                {t("actions.cancel")}
              </Button>
            </span>
          ) : (
            <Button type="button" variant="ghost" className="ml-auto" onClick={() => setConfirmDiscard(true)}>
              {t("drafts.discard")}
            </Button>
          ))}
      </div>

      <Dialog open={reviewing} onOpenChange={setReviewing}>
        <DialogContent aria-describedby={undefined}>
          <Review client={client} request={toRequest(client.id, answers, language)} />
          <div className="flex flex-wrap gap-3 border-t pt-4">
            <Button type="button" onClick={() => submit.mutate()} disabled={submit.isPending}>
              {t("newApplication.submit")}
            </Button>
            <DialogClose asChild>
              <Button type="button" variant="outline">
                {t("newApplication.backToForm")}
              </Button>
            </DialogClose>
          </div>
        </DialogContent>
      </Dialog>
    </form>
  );
}

function Review({ client, request }: { client: ClientDetail; request: NewApplication }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const value = (name: keyof ApplicationAnswers) => request[name] as unknown;
  const missing = reviewSections.flatMap((section) => section.fields).filter((name) => value(name) === null).length;

  return (
    <>
      <DialogTitle>{t("newApplication.reviewTitle")}</DialogTitle>
      <div>
        <div className="font-semibold">{clientName(client, t)}</div>
        <div className="font-mono text-xs text-muted-foreground">{client.clientNumber}</div>
      </div>
      {missing > 0 && <Alert>{t("newApplication.missingAnswers", { count: missing })}</Alert>}
      <div className="-mx-6 grid min-h-0 flex-1 gap-4 overflow-y-auto px-6">
        {reviewSections.map((section) => (
          <section key={section.title} className="grid gap-2">
            <h3 className="text-sm font-semibold">{t(section.title)}</h3>
            <Facts
              items={section.fields.map((name) => ({
                label: t(`fields.${pascal(name)}`),
                value:
                  value(name) === null ? (
                    <span className="text-reject">{t("newApplication.notEntered")}</span>
                  ) : (
                    formatAnswer(pascal(name), value(name), t, language)
                  ),
              }))}
            />
          </section>
        ))}
      </div>
    </>
  );
}

function SaveStatus({ state, onRetry }: { state: AutosaveState; onRetry: () => void }) {
  const { t } = useTranslation();
  const language = useLanguage();
  if (state.status === "idle") return null;
  if (state.status === "saving")
    return (
      <span role="status" className="text-xs text-muted-foreground">
        {t("drafts.saving")}
      </span>
    );
  if (state.status === "failed")
    return (
      <span role="status" className="flex items-center gap-2 text-xs text-reject">
        <span>{t("drafts.notSaved")}</span>
        {state.error instanceof ApiError && state.error.status === 409 ? (
          <span>{errorMessage(state.error)}</span>
        ) : (
          <Button type="button" variant="link" size="sm" className="h-auto p-0 text-xs" onClick={onRetry}>
            {t("states.retry")}
          </Button>
        )}
      </span>
    );
  const sameDay = new Date(state.savedAt).toDateString() === new Date().toDateString();
  return (
    <span role="status" className="text-xs text-muted-foreground">
      {t("drafts.savedAt", { time: sameDay ? formatTime(state.savedAt, language) : formatDateTime(state.savedAt, language) })}
    </span>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card title={title}>
      <div className="grid gap-4 md:grid-cols-2">{children}</div>
    </Card>
  );
}
