import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { Search } from "lucide-react";
import { search, searchKey } from "@/api/search";
import { StatusBadge } from "@/components/status-badge";
import { Input } from "@/components/ui/input";
import { errorMessage } from "@/i18n";
import { clientName } from "@/lib/client-name";
import { cn } from "@/lib/utils";

const debounce = 250;

type Option = { id: string; to: "/clients/$id" | "/applications/$id"; params: { id: string } };

function typing(target: EventTarget | null) {
  return target instanceof HTMLElement && (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));
}

export function GlobalSearch() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const [text, setText] = useState("");
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);

  useEffect(() => {
    const wanted = text.trim();
    const timer = setTimeout(() => setQuery(wanted.length >= 2 ? wanted : ""), debounce);
    return () => clearTimeout(timer);
  }, [text]);

  useEffect(() => {
    function onKey(event: globalThis.KeyboardEvent) {
      if (event.key !== "/" || typing(event.target)) return;
      event.preventDefault();
      input.current?.focus();
    }
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  const results = useQuery({ queryKey: searchKey(query), queryFn: () => search(query), enabled: query !== "", placeholderData: keepPreviousData });
  const data = query !== "" ? results.data : undefined;
  const options: Option[] = [
    ...(data?.clients ?? []).map((client) => ({ id: `${id}-c-${client.id}`, to: "/clients/$id" as const, params: { id: client.id } })),
    ...(data?.applications ?? []).map((application) => ({
      id: `${id}-a-${application.id}`,
      to: "/applications/$id" as const,
      params: { id: application.id },
    })),
  ];
  const showList = open && query !== "";

  function go(option: Option) {
    setText("");
    setQuery("");
    setOpen(false);
    setActive(-1);
    input.current?.blur();
    void navigate({ to: option.to, params: option.params });
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Escape") {
      setOpen(false);
      setActive(-1);
      return;
    }
    if (!options.length) return;
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      setOpen(true);
      const step = event.key === "ArrowDown" ? 1 : -1;
      setActive((current) => (current + step + options.length) % options.length);
    } else if (event.key === "Enter") {
      event.preventDefault();
      go(options[active >= 0 ? active : 0]);
    }
  }

  const optionClass = (index: number) =>
    cn("flex cursor-pointer items-center gap-3 rounded px-2.5 py-2", index === active ? "bg-accent text-accent-foreground" : "hover:bg-muted");
  const clientCount = data?.clients.length ?? 0;

  return (
    <div className="relative w-full max-w-md">
      <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
      <Input
        ref={input}
        role="combobox"
        aria-label={t("search.label")}
        aria-expanded={showList}
        aria-controls={`${id}-list`}
        aria-autocomplete="list"
        aria-activedescendant={showList && active >= 0 ? options[active]?.id : undefined}
        value={text}
        placeholder={t("search.placeholder")}
        autoComplete="off"
        className="bg-card pr-8 pl-9"
        onChange={(event) => {
          setText(event.target.value);
          setOpen(true);
          setActive(-1);
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        onKeyDown={onKeyDown}
      />
      <kbd className="pointer-events-none absolute top-1/2 right-2.5 -translate-y-1/2 rounded border px-1.5 text-[11px] text-muted-foreground" aria-hidden="true">
        /
      </kbd>
      {showList && (
        <div className="absolute top-full right-0 left-0 z-20 mt-1 max-h-96 overflow-y-auto rounded-md border bg-popover p-1 text-sm shadow-md">
          {results.isError ? (
            <p className="px-2.5 py-2 text-reject">{errorMessage(results.error)}</p>
          ) : !data ? (
            <p className="px-2.5 py-2 text-muted-foreground">{t("search.searching")}</p>
          ) : options.length === 0 ? (
            <p className="px-2.5 py-2 text-muted-foreground">{t("search.none")}</p>
          ) : null}
          <ul id={`${id}-list`} role="listbox" aria-label={t("search.results")} hidden={!options.length}>
            {data && data.clients.length > 0 && (
              <li role="presentation" className="px-2.5 pt-1.5 pb-1 text-xs font-medium text-muted-foreground">
                {t("search.clients")}
              </li>
            )}
            {data?.clients.map((client, index) => (
              <li
                key={client.id}
                id={options[index].id}
                role="option"
                aria-selected={index === active}
                className={optionClass(index)}
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => go(options[index])}
              >
                <span className="font-medium">{clientName(client, t)}</span>
                <span className="font-mono text-xs text-muted-foreground">{client.clientNumber}</span>
              </li>
            ))}
            {data && data.applications.length > 0 && (
              <li role="presentation" className="px-2.5 pt-2 pb-1 text-xs font-medium text-muted-foreground">
                {t("search.applications")}
              </li>
            )}
            {data?.applications.map((application, offset) => {
              const index = clientCount + offset;
              return (
                <li
                  key={application.id}
                  id={options[index].id}
                  role="option"
                  aria-selected={index === active}
                  className={optionClass(index)}
                  onMouseDown={(event) => event.preventDefault()}
                  onClick={() => go(options[index])}
                >
                  <span className="font-mono text-xs font-medium">{application.reference}</span>
                  <span>{application.applicantName}</span>
                  <span className="ml-auto">
                    <StatusBadge recommendation={application.recommendation} />
                  </span>
                </li>
              );
            })}
          </ul>
        </div>
      )}
    </div>
  );
}
