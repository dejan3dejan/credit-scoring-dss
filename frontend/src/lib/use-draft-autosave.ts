import { useCallback, useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import type { NewApplication } from "@/api/applications";
import { ApiError } from "@/api/client";
import { createDraft, draftKey, updateDraft, type DraftResponse } from "@/api/drafts";
import { errorMessage } from "@/i18n";

export const autosaveDelay = 2000;

export type AutosaveState =
  | { status: "idle" }
  | { status: "saving"; savedAt?: string }
  | { status: "saved"; savedAt: string }
  | { status: "failed"; savedAt?: string; error: unknown };

type Saver = {
  draftId: string | undefined;
  pending: NewApplication | null;
  timer: ReturnType<typeof setTimeout> | undefined;
  running: Promise<void> | null;
  stopped: boolean;
  mounted: boolean;
  failed: boolean;
  last: DraftResponse | undefined;
};

function savedAtOf(state: AutosaveState): string | undefined {
  return state.status === "idle" ? undefined : state.savedAt;
}

export function useDraftAutosave(
  draft: DraftResponse | undefined,
  onSaved: (saved: DraftResponse, body: NewApplication, first: boolean) => void,
) {
  const queryClient = useQueryClient();
  const [state, setState] = useState<AutosaveState>(draft ? { status: "saved", savedAt: draft.updatedAt } : { status: "idle" });
  const [draftId, setDraftId] = useState(draft?.id);
  const saver = useRef<Saver>({
    draftId: draft?.id,
    pending: null,
    timer: undefined,
    running: null,
    stopped: false,
    mounted: true,
    failed: false,
    last: draft,
  });
  const saved = useRef(onSaved);

  useEffect(() => {
    saved.current = onSaved;
  });

  const flush = useCallback(async (): Promise<void> => {
    const s = saver.current;
    clearTimeout(s.timer);
    s.timer = undefined;
    while (s.running) await s.running;
    const body = s.pending;
    if (!body || s.stopped) return;
    s.pending = null;
    const first = !s.draftId;
    if (s.mounted) setState((current) => ({ status: "saving", savedAt: savedAtOf(current) }));
    const run = (s.draftId ? updateDraft(s.draftId, body) : createDraft(body))
      .then(
        (draft) => {
          s.draftId = draft.id;
          s.failed = false;
          s.last = draft;
          queryClient.setQueryData(draftKey(draft.id), draft);
          void queryClient.invalidateQueries({ queryKey: ["work"] });
          if (!s.mounted) return;
          setDraftId(draft.id);
          setState({ status: "saved", savedAt: draft.updatedAt });
          saved.current(draft, body, first);
        },
        (error: unknown) => {
          s.pending ??= body;
          s.failed = true;
          if (error instanceof ApiError && error.status === 409) s.stopped = true;
          if (s.mounted) setState((current) => ({ status: "failed", savedAt: savedAtOf(current), error }));
          else toast.error(errorMessage(error));
        },
      )
      .finally(() => {
        s.running = null;
      });
    s.running = run;
    await run;
  }, [queryClient]);

  const schedule = useCallback(
    (body: NewApplication) => {
      const s = saver.current;
      if (s.stopped) return;
      s.pending = body;
      clearTimeout(s.timer);
      s.timer = setTimeout(() => void flush(), autosaveDelay);
    },
    [flush],
  );

  const check = useCallback(
    async (body: NewApplication) => {
      const s = saver.current;
      if (s.stopped) return null;
      s.pending = body;
      await flush();
      return s.failed || !s.last ? null : s.last.fieldErrors;
    },
    [flush],
  );

  const beforeSubmit = useCallback(async () => {
    const s = saver.current;
    clearTimeout(s.timer);
    s.timer = undefined;
    while (s.running) await s.running;
    return s.draftId;
  }, []);

  const submitted = useCallback(() => {
    const s = saver.current;
    s.stopped = true;
    s.pending = null;
  }, []);

  const resume = useCallback(() => {
    const s = saver.current;
    if (s.pending && !s.stopped) s.timer = setTimeout(() => void flush(), autosaveDelay);
  }, [flush]);

  useEffect(() => {
    const s = saver.current;
    s.mounted = true;
    return () => {
      s.mounted = false;
      if (s.timer !== undefined) void flush();
    };
  }, [flush]);

  return { state, draftId, schedule, check, retry: flush, beforeSubmit, submitted, resume };
}
