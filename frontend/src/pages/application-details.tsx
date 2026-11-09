import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { ChevronLeft, Info } from "lucide-react";
import { toast } from "sonner";
import { applicationKey, getApplication, getShadowScores, recordDecision, rescore, signOff } from "@/api/applications";
import { ApiError } from "@/api/client";
import type { ApplicationResponse, FinalDecision, MeResponse, Recommendation, SignOffResponse } from "@/api/types";
import { can, useMe } from "@/auth/session";
import { ErrorState, Loading } from "@/components/page";
import { FactorList, RiskScale } from "@/components/risk";
import { StatusBadge } from "@/components/status-badge";
import { Alert, Card, DecisionText, Facts, FieldErrors, Tag, TextareaField } from "@/components/surface";
import { Button } from "@/components/ui/button";
import { errorMessage, useLanguage } from "@/i18n";
import { formatAmount, formatDateTime, formatPercent } from "@/i18n/format";
import { checklist, describeInput } from "@/lib/application-inputs";
import { cn } from "@/lib/utils";

export function ApplicationDetailsPage({ id }: { id: string }) {
  const application = useQuery({ queryKey: applicationKey(id), queryFn: () => getApplication(id) });
  if (application.isPending) return <Loading />;
  if (application.isError) return <ErrorState error={application.error} onRetry={() => void application.refetch()} />;
  return <ApplicationView application={application.data} />;
}

function ApplicationView({ application: a }: { application: ApplicationResponse }) {
  const { t } = useTranslation();
  const me = useMe();
  const language = useLanguage();

  return (
    <>
      <div className="flex flex-wrap items-end gap-4">
        <div className="min-w-0">
          <Link to="/applications" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
            <ChevronLeft className="size-4" aria-hidden="true" />
            {t("details.back")}
          </Link>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">
            <Link to="/clients/$id" params={{ id: a.clientId }} className="hover:underline">
              {a.applicantName}
            </Link>
          </h1>
          <p className="mt-1 text-muted-foreground">
            <span className="font-mono text-xs">{a.reference}</span>
            {" · "}
            <span className="font-mono text-xs">{a.clientNumber}</span>
            {" · "}
            {t("details.summary", {
              amount: formatAmount(a.loanAmount, language),
              term: t("units.months", { count: a.termMonths }),
              purpose: t(`enums.LoanPurpose.${a.purpose}`),
              by: a.submittedBy,
            })}
          </p>
        </div>
        <span className="ml-auto rounded border bg-card px-2.5 py-1 text-sm">{t(`enums.ApplicationStage.${a.stage}`)}</span>
      </div>

      <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_380px]">
        <div className="min-w-0">
          <RiskPanel application={a} />
        </div>
        <div className="grid gap-5 xl:col-start-2 xl:row-span-2 xl:row-start-1">
          <DecisionPanel application={a} me={me} />
          <Timeline application={a} />
        </div>
        <div className="grid min-w-0 gap-5 xl:row-start-2">
          {a.decision && a.decision.factors.length > 0 && (
            <Card title={t("details.factorsTitle")} footer={t("details.factorsNote")}>
              <FactorList application={a} factors={a.decision.factors} />
            </Card>
          )}
          <div className="flex gap-3 rounded-md border bg-card px-4 py-3 text-muted-foreground">
            <Info className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <p>
              <strong className="font-semibold text-foreground">{t("details.supportTitle")}</strong> {t("details.supportText")}
            </p>
          </div>
          {can(me, "stats.read") && <ShadowScores application={a} />}
          <AsSubmitted application={a} />
        </div>
      </div>
    </>
  );
}

function RiskPanel({ application: a }: { application: ApplicationResponse }) {
  const { t } = useTranslation();
  const me = useMe();
  const language = useLanguage();
  const queryClient = useQueryClient();
  const d = a.decision;
  const again = useMutation({
    mutationFn: () => rescore(a.id),
    onSuccess: (fresh) => {
      queryClient.setQueryData(applicationKey(a.id), fresh);
      void queryClient.invalidateQueries({ queryKey: ["shadow-scores", a.id] });
      toast.success(t("details.rescored"));
    },
  });

  if (d && d.status === "Scored" && d.pBad !== null && d.thresholdUsed !== null && d.recommendation) {
    const explain: Record<Recommendation, string> = {
      Approve: d.reviewFromUsed !== null ? t("details.explainApprove") : t("details.explainApproveNoBand"),
      Review: t("details.explainReview"),
      Reject: t("details.explainReject"),
    };
    return (
      <Card>
        <div className="grid gap-6 md:grid-cols-[minmax(0,1fr)_220px]">
          <div className="grid gap-4">
            <div>
              <div className="text-xs text-muted-foreground">{t("details.risk")}</div>
              <div className="mt-1 flex flex-wrap items-center gap-3">
                <span className="text-[28px] leading-none font-semibold">{formatPercent(d.pBad, language)}</span>
                <StatusBadge recommendation={d.recommendation} />
              </div>
              <p className="mt-2 max-w-prose text-muted-foreground">{explain[d.recommendation]}</p>
            </div>
            <RiskScale pBad={d.pBad} reviewFrom={d.reviewFromUsed} threshold={d.thresholdUsed} />
          </div>
          <Facts
            stacked
            items={[
              { label: t("details.model"), value: <span className="font-mono text-xs">{d.modelId}</span> },
              { label: t("details.scored"), value: formatDateTime(d.evaluatedAt, language) },
            ]}
          />
        </div>
      </Card>
    );
  }

  const canRescore = can(me, "applications.write") && a.stage === "Submitted" && !a.officerDecision;
  const code = d?.errorCode ?? null;
  return (
    <Card>
      <div className="grid gap-4 md:grid-cols-[minmax(0,1fr)_220px]">
        <div className="grid gap-2">
          <div className="flex items-center gap-3">
            <StatusBadge recommendation={null} />
            <h2 className="font-semibold">{t("details.unavailableTitle")}</h2>
          </div>
          <p className="max-w-prose text-muted-foreground">
            {code ? t(`scoringErrors.${code}`, { defaultValue: t("scoringErrors.unknown") }) : t("details.tlNoScore")}{" "}
            {t("details.unavailableText")} {canRescore && t("details.unavailableRescore")}
          </p>
          {again.error && <Alert>{errorMessage(again.error)}</Alert>}
          {canRescore && (
            <div>
              <Button onClick={() => again.mutate()} disabled={again.isPending}>
                {t("details.rescore")}
              </Button>
            </div>
          )}
        </div>
        <Facts
          stacked
          items={[
            { label: t("details.errorCode"), value: <span className="font-mono text-xs">{code ?? "–"}</span> },
            { label: t("details.attempted"), value: d ? formatDateTime(d.evaluatedAt, language) : "–" },
            ...(a.scoringFailures.length > 0 ? [{ label: t("details.earlierAttempts"), value: a.scoringFailures.length }] : []),
          ]}
        />
      </div>
    </Card>
  );
}

function DecisionPanel({ application: a, me }: { application: ApplicationResponse; me: MeResponse }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const pending = a.signOffs.find((s) => s.status === "Pending");
  const returned = a.stage === "Submitted" ? a.signOffs.findLast((s) => s.status === "Returned") : undefined;

  let body;
  if (a.stage === "Withdrawn" && a.withdrawal) {
    body = (
      <>
        <p>{t("details.withdrawn", { name: a.withdrawal.by, date: formatDateTime(a.withdrawal.at, language) })}</p>
        <blockquote className="border-l-2 pl-3 text-muted-foreground">{a.withdrawal.reason}</blockquote>
      </>
    );
  } else if (a.stage === "Decided" && a.officerDecision) {
    const od = a.officerDecision;
    body = (
      <>
        <div className="flex items-center gap-2 text-base">
          <DecisionText outcome={od.outcome} />
          {od.isOverride && <Tag>{t("details.override")}</Tag>}
        </div>
        {od.reason && <blockquote className="border-l-2 pl-3">{od.reason}</blockquote>}
        <p className="text-muted-foreground">{t("details.decidedBy", { name: od.decidedBy, date: formatDateTime(od.decidedAt, language) })}</p>
        {od.signedOffBy && od.signedOffAt && (
          <p className="text-muted-foreground">
            {t("details.signedOffBy", { name: od.signedOffBy, date: formatDateTime(od.signedOffAt, language) })}
          </p>
        )}
      </>
    );
  } else if (a.stage === "AwaitingSignOff" && pending) {
    body = <PendingSignOff application={a} request={pending} canSignOff={can(me, "applications.approve-escalated")} />;
  } else if (!can(me, "applications.decide")) {
    body = <p className="text-muted-foreground">{t("details.cannotDecide")}</p>;
  } else if (a.assignedTo?.id !== me.id && !can(me, "applications.assign")) {
    body = (
      <p className="text-muted-foreground">
        {a.assignedTo ? t("details.notAssignee", { name: a.assignedTo.displayName }) : t("details.unassigned")}
      </p>
    );
  } else {
    body = (
      <>
        {returned && (
          <div className="rounded border border-review/30 bg-review-tint px-3 py-2">
            <p className="font-semibold text-review">{t("details.tlSignOffReturned", { name: returned.reviewedBy })}</p>
            {returned.note && <p className="mt-1">{returned.note}</p>}
          </div>
        )}
        <DecisionForm application={a} me={me} />
      </>
    );
  }

  return (
    <Card title={t("details.decisionTitle")}>
      <div className="grid gap-3">{body}</div>
    </Card>
  );
}

function DecisionForm({ application: a, me }: { application: ApplicationResponse; me: MeResponse }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [outcome, setOutcome] = useState<FinalDecision | null>(null);
  const [reason, setReason] = useState("");
  const recommended = a.decision?.recommendation ?? null;
  const prompts = recommended === "Review" && a.decision ? checklist(a.decision.factors) : [];

  const decide = useMutation({
    mutationFn: () => recordDecision(a.id, outcome, reason.trim() || null),
    onSuccess: (fresh) => {
      queryClient.setQueryData(applicationKey(a.id), fresh);
      void queryClient.invalidateQueries({ queryKey: ["applications"] });
      toast.success(fresh.stage === "AwaitingSignOff" ? t("details.sentForSignOff") : t("details.recorded"));
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    decide.mutate();
  }

  const fieldErrors = decide.error instanceof ApiError ? decide.error.fieldErrors : {};
  const generalError = decide.error && Object.keys(fieldErrors).length === 0;
  const hint =
    recommended === "Review"
      ? t("details.reasonRequiredReview")
      : recommended
        ? t("details.reasonRequiredAgainst", { recommendation: t(`recommendation.${recommended}`) })
        : t("details.reasonRequiredNone");

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      <p className="text-muted-foreground">{t("details.decisionIntro")}</p>
      {prompts.length > 0 && (
        <div className="rounded border border-review/30 bg-review-tint/60 px-3 py-2.5">
          <p className="font-semibold">{t("details.checklistTitle")}</p>
          <p className="mt-0.5 text-muted-foreground">{t("details.checklistIntro")}</p>
          <ChecklistItems application={a} inputs={prompts} />
        </div>
      )}
      {generalError && <Alert>{errorMessage(decide.error)}</Alert>}
      <fieldset className="grid gap-1.5">
        <legend className="mb-1.5 text-sm font-medium">{t("details.outcome")}</legend>
        <div className="grid grid-cols-2 gap-2">
          {(["Approve", "Reject"] as const).map((value) => (
            <label
              key={value}
              className={cn(
                "flex cursor-pointer items-center justify-center gap-2 rounded-md border px-3 py-2 font-medium has-focus-visible:ring-[3px] has-focus-visible:ring-ring/50",
                outcome === value ? "border-foreground bg-muted" : "bg-card hover:bg-muted/60",
              )}
            >
              <input
                type="radio"
                name="outcome"
                value={value}
                checked={outcome === value}
                onChange={() => setOutcome(value)}
                className="accent-foreground"
              />
              {t(value === "Approve" ? "details.approve" : "details.reject")}
            </label>
          ))}
        </div>
        <FieldErrors id="outcome-error" errors={fieldErrors.outcome} />
      </fieldset>
      <TextareaField
        label={t("details.reason")}
        placeholder={t("details.reasonPlaceholder")}
        hint={hint}
        value={reason}
        onChange={(event) => setReason(event.target.value)}
        maxLength={500}
        rows={3}
        errors={fieldErrors.reason}
      />
      <div className="grid gap-2">
        <Button type="submit" disabled={decide.isPending}>
          {t("details.record")}
        </Button>
        <p className="text-xs text-muted-foreground">{t("details.recordedAs", { name: me.displayName })}</p>
      </div>
    </form>
  );
}

function ChecklistItems({ application, inputs }: { application: ApplicationResponse; inputs: string[] }) {
  const { t } = useTranslation();
  const language = useLanguage();
  return (
    <ul className="mt-2 grid list-disc gap-1.5 pl-5">
      {inputs.map((input) => {
        const { label, value } = describeInput(application, input, t, language);
        return (
          <li key={input}>
            <strong className="font-semibold">
              {label}: {value}.
            </strong>{" "}
            {t(`prompts.${input}`)}
          </li>
        );
      })}
    </ul>
  );
}

function PendingSignOff({
  application: a,
  request,
  canSignOff,
}: {
  application: ApplicationResponse;
  request: SignOffResponse;
  canSignOff: boolean;
}) {
  const { t } = useTranslation();
  const language = useLanguage();
  const queryClient = useQueryClient();
  const [note, setNote] = useState("");
  const review = useMutation({
    mutationFn: (approve: boolean) => signOff(a.id, approve, note.trim() || null),
    onSuccess: (fresh) => {
      queryClient.setQueryData(applicationKey(a.id), fresh);
      void queryClient.invalidateQueries({ queryKey: ["applications"] });
      toast.success(t("details.signOffDone"));
    },
  });
  const fieldErrors = review.error instanceof ApiError ? review.error.fieldErrors : {};
  const generalError = review.error && Object.keys(fieldErrors).length === 0;

  return (
    <>
      <h3 className="font-semibold text-review">{t("details.signOffTitle")}</h3>
      <p>
        {t("details.signOffRequested", {
          name: request.requestedBy,
          date: formatDateTime(request.requestedAt, language),
          why: t(`enums.SignOffReason.${request.why}`),
        })}
      </p>
      <div className="flex items-center gap-2">
        <DecisionText outcome={request.outcome} />
      </div>
      {request.reason && <blockquote className="border-l-2 pl-3">{request.reason}</blockquote>}
      {canSignOff ? (
        <div className="grid gap-3">
          {generalError && <Alert>{errorMessage(review.error)}</Alert>}
          <TextareaField
            label={t("details.signOffNote")}
            hint={t("details.signOffNoteHint")}
            value={note}
            onChange={(event) => setNote(event.target.value)}
            maxLength={500}
            rows={2}
            errors={fieldErrors.note}
          />
          <div className="grid grid-cols-2 gap-2">
            <Button onClick={() => review.mutate(true)} disabled={review.isPending}>
              {t("details.signOffApprove")}
            </Button>
            <Button variant="outline" onClick={() => review.mutate(false)} disabled={review.isPending}>
              {t("details.signOffReturn")}
            </Button>
          </div>
        </div>
      ) : (
        <p className="text-muted-foreground">{t("details.signOffWaiting")}</p>
      )}
    </>
  );
}

type TimelineEntry = { at: string | null; text: string; tone: "done" | "failed" | "pending" };

function Timeline({ application: a }: { application: ApplicationResponse }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const entries: TimelineEntry[] = [{ at: a.submittedAt, text: t("details.tlSubmitted", { name: a.submittedBy }), tone: "done" }];

  for (const failure of a.scoringFailures)
    entries.push({ at: failure.attemptedAt, text: t("details.tlFailed", { code: failure.errorCode }), tone: "failed" });
  const d = a.decision;
  if (!d) entries.push({ at: null, text: t("details.tlNoScore"), tone: "failed" });
  else if (d.status === "Scored" && d.pBad !== null)
    entries.push({
      at: d.evaluatedAt,
      text: t("details.tlScored", {
        pBad: formatPercent(d.pBad, language),
        recommendation: d.recommendation ? t(`recommendation.${d.recommendation}`) : t("recommendation.none"),
      }),
      tone: "done",
    });
  else entries.push({ at: d.evaluatedAt, text: t("details.tlFailed", { code: d.errorCode ?? "–" }), tone: "failed" });

  for (const s of a.signOffs) {
    entries.push({ at: s.requestedAt, text: t("details.tlSignOffRequested", { name: s.requestedBy }), tone: "done" });
    if (s.status === "Approved" && s.reviewedAt)
      entries.push({ at: s.reviewedAt, text: t("details.tlSignOffApproved", { name: s.reviewedBy }), tone: "done" });
    if (s.status === "Returned" && s.reviewedAt)
      entries.push({ at: s.reviewedAt, text: t("details.tlSignOffReturned", { name: s.reviewedBy }), tone: "failed" });
  }
  const od = a.officerDecision;
  if (od)
    entries.push({
      at: od.decidedAt,
      text: t(od.isOverride ? "details.tlOverride" : "details.tlDecided", {
        decision: t(`enums.FinalDecision.${od.outcome}`),
        name: od.decidedBy,
      }),
      tone: "done",
    });
  if (a.withdrawal) entries.push({ at: a.withdrawal.at, text: t("details.tlWithdrawn", { name: a.withdrawal.by }), tone: "failed" });
  if (a.stage === "Submitted") entries.push({ at: null, text: t("details.tlAwaiting"), tone: "pending" });

  const sorted = [...entries.filter((e) => e.at).sort((x, y) => x.at!.localeCompare(y.at!)), ...entries.filter((e) => !e.at)];
  const dot = { done: "bg-primary", failed: "bg-reject", pending: "border border-muted-foreground bg-card" };

  return (
    <Card title={t("details.timeline")}>
      <ol className="grid gap-3">
        {sorted.map((entry, index) => (
          <li key={index} className="grid grid-cols-[10px_minmax(0,1fr)] gap-x-3">
            <span className={cn("mt-1.5 size-2.5 rounded-full", dot[entry.tone])} aria-hidden="true" />
            <span>{entry.text}</span>
            {entry.at && <span className="col-start-2 text-xs text-muted-foreground">{formatDateTime(entry.at, language)}</span>}
          </li>
        ))}
      </ol>
    </Card>
  );
}

function ShadowScores({ application: a }: { application: ApplicationResponse }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const shadows = useQuery({ queryKey: ["shadow-scores", a.id], queryFn: () => getShadowScores(a.id) });

  return (
    <Card title={t("details.otherModels")} footer={t("details.otherModelsNote")}>
      {shadows.isPending ? (
        <Loading />
      ) : shadows.isError ? (
        <ErrorState error={shadows.error} onRetry={() => void shadows.refetch()} />
      ) : shadows.data.length === 0 ? (
        <p className="text-muted-foreground">{t("details.noShadows")}</p>
      ) : (
        <ul className="grid gap-3">
          {shadows.data.map((s) => {
            const agrees = s.recommendation !== null && s.recommendation === a.decision?.recommendation;
            return (
              <li key={s.modelId} className="flex flex-wrap items-center gap-3">
                <div className="min-w-0">
                  <div className="font-mono text-xs">{s.modelId}</div>
                  <div className="text-xs text-muted-foreground">
                    {formatDateTime(s.scoredAt, language)}
                    {s.thresholdUsed !== null && ` · ${t("details.rejectsAt", { value: formatPercent(s.thresholdUsed, language) })}`}
                  </div>
                </div>
                <div className="ml-auto flex items-center gap-2">
                  {s.status === "Scored" && s.pBad !== null && s.recommendation ? (
                    <>
                      <span className="font-medium">{formatPercent(s.pBad, language)}</span>
                      <StatusBadge recommendation={s.recommendation} />
                      <Tag>{agrees ? t("details.agrees") : t("details.differs")}</Tag>
                    </>
                  ) : (
                    <span className="text-muted-foreground">{t("details.notScored", { code: s.errorCode ?? "–" })}</span>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </Card>
  );
}

function AsSubmitted({ application: a }: { application: ApplicationResponse }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const describe = (input: string) => describeInput(a, input, t, language);
  const cards: { title: string; inputs: string[]; extra?: { label: string; value: string }[] }[] = [
    {
      title: t("details.cardLoan"),
      inputs: ["LoanAmount", "Purpose"],
      extra: [{ label: t("fields.TermMonths"), value: t("units.months", { count: a.termMonths }) }],
    },
    {
      title: t("details.cardIncome"),
      inputs: ["AnnualIncome", "IncomeVerification", "DebtToIncomePercent", "EmploymentLength", "HomeOwnership", "MortgageAccounts"],
    },
    {
      title: t("details.cardReport"),
      inputs: ["CreditScore", "EarliestCreditLine", "InquiriesLast6Months", "DelinquenciesLast2Years", "PublicRecords", "Bankruptcies"],
    },
    { title: t("details.cardAccounts"), inputs: ["RevolvingBalance", "RevolvingUtilizationPercent", "OpenAccounts", "TotalAccounts"] },
  ];

  return (
    <section className="grid gap-3">
      <h2 className="font-semibold">{t("details.asSubmitted")}</h2>
      <div className="grid gap-4 md:grid-cols-2">
        {cards.map((card) => (
          <Card key={card.title} title={card.title}>
            <Facts items={[...card.inputs.map(describe), ...(card.extra ?? [])]} />
          </Card>
        ))}
      </div>
    </section>
  );
}
