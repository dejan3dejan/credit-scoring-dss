import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { addNote, applicationKey, assign, listNotes, notesKey, recordOutcome, withdraw } from "@/api/applications";
import { ApiError } from "@/api/client";
import type { ApplicationResponse, LoanOutcomeKind, MeResponse } from "@/api/types";
import { getTeamWork } from "@/api/work";
import { can } from "@/auth/session";
import { ErrorState, Loading } from "@/components/page";
import { Alert, Card, FieldErrors, SelectField, TextareaField } from "@/components/surface";
import { Button } from "@/components/ui/button";
import { errorMessage, useLanguage } from "@/i18n";
import { formatDateTime } from "@/i18n/format";
import { isOpen } from "@/lib/application-rules";
import { cn } from "@/lib/utils";

function useApplicationUpdate(id: string) {
  const queryClient = useQueryClient();
  return (fresh: ApplicationResponse) => {
    queryClient.setQueryData(applicationKey(id), fresh);
    void queryClient.invalidateQueries({ queryKey: ["applications"] });
    void queryClient.invalidateQueries({ queryKey: ["work"] });
  };
}

function fieldErrorsOf(error: unknown) {
  return error instanceof ApiError ? error.fieldErrors : {};
}

function GeneralError({ error }: { error: unknown }) {
  if (!error || Object.keys(fieldErrorsOf(error)).length > 0) return null;
  return <Alert>{errorMessage(error)}</Alert>;
}


export function ApplicationActions({ application: a, me }: { application: ApplicationResponse; me: MeResponse }) {
  const loanIssued = a.stage === "Decided" && a.officerDecision?.outcome === "Approve";
  return (
    <>
      {can(me, "applications.assign") && isOpen(a) && <AssignCard application={a} />}
      {loanIssued && (a.loanOutcome || can(me, "outcomes.record")) && <OutcomeCard application={a} />}
      <NotesCard application={a} canWrite={can(me, "applications.write")} />
    </>
  );
}

function AssignCard({ application: a }: { application: ApplicationResponse }) {
  const { t } = useTranslation();
  const update = useApplicationUpdate(a.id);
  const team = useQuery({ queryKey: ["work", "team"], queryFn: getTeamWork });
  const [userId, setUserId] = useState("");
  const change = useMutation({
    mutationFn: () => assign(a.id, userId),
    onSuccess: (fresh) => {
      update(fresh);
      setUserId("");
      toast.success(t("actions.assigned", { name: fresh.assignedTo?.displayName }));
    },
  });

  return (
    <Card title={t("actions.assignTitle")}>
      <div className="grid gap-3">
        <p>{a.assignedTo?.displayName ?? t("actions.assignNobody")}</p>
        {team.isPending ? (
          <Loading />
        ) : team.isError ? (
          <ErrorState error={team.error} onRetry={() => void team.refetch()} />
        ) : (
          <form
            className="flex items-end gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              change.mutate();
            }}
          >
            <div className="min-w-0 flex-1">
              <SelectField
                label={t("actions.assignTo")}
                value={userId}
                placeholder={t("newApplication.select")}
                options={team.data.officers
                  .filter((officer) => officer.id !== a.assignedTo?.id)
                  .map((officer) => ({ value: officer.id, label: officer.displayName }))}
                onChange={(event) => setUserId(event.target.value)}
                errors={fieldErrorsOf(change.error).userId}
              />
            </div>
            <Button type="submit" variant="outline" disabled={!userId || change.isPending}>
              {t("actions.assign")}
            </Button>
          </form>
        )}
        <GeneralError error={change.error} />
      </div>
    </Card>
  );
}

export function WithdrawAction({ application: a }: { application: ApplicationResponse }) {
  const { t } = useTranslation();
  const update = useApplicationUpdate(a.id);
  const [confirming, setConfirming] = useState(false);
  const [reason, setReason] = useState("");
  const change = useMutation({
    mutationFn: () => withdraw(a.id, reason.trim()),
    onSuccess: (fresh) => {
      update(fresh);
      toast.success(t("actions.withdrawnToast"));
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    change.mutate();
  }

  return (
    <div className="border-t pt-3">
      {confirming ? (
        <form onSubmit={submit} noValidate className="grid gap-3">
          <TextareaField
            label={t("actions.withdrawReason")}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            maxLength={500}
            rows={2}
            errors={fieldErrorsOf(change.error).reason}
            autoFocus
          />
          <GeneralError error={change.error} />
          <div className="flex gap-2">
            <Button type="submit" variant="outline" className="border-reject text-reject" disabled={change.isPending}>
              {t("actions.withdrawConfirm")}
            </Button>
            <Button type="button" variant="ghost" onClick={() => setConfirming(false)}>
              {t("actions.cancel")}
            </Button>
          </div>
        </form>
      ) : (
        <Button variant="ghost" size="sm" className="-ml-2 text-muted-foreground" onClick={() => setConfirming(true)}>
          {t("actions.withdraw")}
        </Button>
      )}
    </div>
  );
}

function OutcomeCard({ application: a }: { application: ApplicationResponse }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const update = useApplicationUpdate(a.id);
  const [outcome, setOutcome] = useState<LoanOutcomeKind | null>(null);
  const [note, setNote] = useState("");
  const change = useMutation({
    mutationFn: () => recordOutcome(a.id, outcome, note.trim() || null),
    onSuccess: (fresh) => {
      update(fresh);
      toast.success(t("actions.outcomeRecorded"));
    },
  });
  const errors = fieldErrorsOf(change.error);

  if (a.loanOutcome)
    return (
      <Card title={t("actions.outcomeTitle")}>
        <p>
          {t("actions.outcomeShown", {
            outcome: t(`enums.LoanOutcomeKind.${a.loanOutcome.outcome}`),
            name: a.loanOutcome.recordedBy,
            date: formatDateTime(a.loanOutcome.recordedAt, language),
          })}
        </p>
        {a.loanOutcome.note && <blockquote className="mt-2 border-l-2 pl-3 text-muted-foreground">{a.loanOutcome.note}</blockquote>}
      </Card>
    );

  return (
    <Card title={t("actions.outcomeTitle")}>
      <form
        noValidate
        className="grid gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          change.mutate();
        }}
      >
        <fieldset className="grid gap-1.5">
          <legend className="mb-1.5 text-sm font-medium">{t("actions.outcome")}</legend>
          <div className="grid grid-cols-2 gap-2">
            {(["Repaid", "Defaulted"] as const).map((value) => (
              <label
                key={value}
                className={cn(
                  "flex cursor-pointer items-center justify-center gap-2 rounded-md border px-3 py-2 font-medium has-focus-visible:ring-[3px] has-focus-visible:ring-ring/50",
                  outcome === value ? "border-foreground bg-muted" : "bg-card hover:bg-muted/60",
                )}
              >
                <input
                  type="radio"
                  name="loanOutcome"
                  value={value}
                  checked={outcome === value}
                  onChange={() => setOutcome(value)}
                  className="accent-foreground"
                />
                {t(`enums.LoanOutcomeKind.${value}`)}
              </label>
            ))}
          </div>
          <FieldErrors id="loan-outcome-error" errors={errors.outcome} />
        </fieldset>
        <TextareaField
          label={t("actions.outcomeNote")}
          value={note}
          onChange={(event) => setNote(event.target.value)}
          maxLength={500}
          rows={2}
          errors={errors.note}
        />
        <GeneralError error={change.error} />
        <div>
          <Button type="submit" disabled={change.isPending}>
            {t("actions.recordOutcome")}
          </Button>
        </div>
      </form>
    </Card>
  );
}

function NotesCard({ application: a, canWrite }: { application: ApplicationResponse; canWrite: boolean }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const queryClient = useQueryClient();
  const notes = useQuery({ queryKey: notesKey(a.id), queryFn: () => listNotes(a.id) });
  const [text, setText] = useState("");
  const add = useMutation({
    mutationFn: () => addNote(a.id, text.trim()),
    onSuccess: (note) => {
      queryClient.setQueryData(notesKey(a.id), (current: typeof notes.data) => [...(current ?? []), note]);
      setText("");
      toast.success(t("actions.noteAdded"));
    },
  });

  return (
    <Card title={t("actions.notesTitle")}>
      <div className="grid gap-4">
        {notes.isPending ? (
          <Loading />
        ) : notes.isError ? (
          <ErrorState error={notes.error} onRetry={() => void notes.refetch()} />
        ) : notes.data.length === 0 ? (
          <p className="text-muted-foreground">{t("actions.notesEmpty")}</p>
        ) : (
          <ol className="grid gap-3">
            {notes.data.map((note) => (
              <li key={note.id}>
                <p className="whitespace-pre-line">{note.text}</p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {note.author} · {formatDateTime(note.createdAt, language)}
                </p>
              </li>
            ))}
          </ol>
        )}
        {canWrite && (
          <form
            noValidate
            className="grid gap-2 border-t pt-3"
            onSubmit={(event) => {
              event.preventDefault();
              add.mutate();
            }}
          >
            <TextareaField
              label={t("actions.noteLabel")}
              placeholder={t("actions.notePlaceholder")}
              value={text}
              onChange={(event) => setText(event.target.value)}
              maxLength={2000}
              rows={2}
              errors={fieldErrorsOf(add.error).text}
            />
            <GeneralError error={add.error} />
            <div>
              <Button type="submit" variant="outline" disabled={add.isPending}>
                {t("actions.addNote")}
              </Button>
            </div>
          </form>
        )}
      </div>
    </Card>
  );
}
