import { useId, type ComponentProps, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { FieldError, FinalDecision } from "@/api/types";
import { Label } from "@/components/ui/label";
import { fieldErrorMessage } from "@/i18n";
import { cn } from "@/lib/utils";

export function Card({
  title,
  actions,
  footer,
  className,
  children,
}: {
  title?: string;
  actions?: ReactNode;
  footer?: ReactNode;
  className?: string;
  children: ReactNode;
}) {
  return (
    <section className={cn("rounded-md border bg-card", className)}>
      {(title || actions) && (
        <div className="flex items-center gap-3 border-b px-4 py-3">
          {title && <h2 className="text-sm font-semibold">{title}</h2>}
          {actions && <div className="ml-auto flex items-center gap-2">{actions}</div>}
        </div>
      )}
      <div className="p-4">{children}</div>
      {footer && <div className="border-t px-4 py-2.5 text-xs text-muted-foreground">{footer}</div>}
    </section>
  );
}

export function Table({ children, label }: { children: ReactNode; label?: string }) {
  return (
    <div className="overflow-x-auto rounded-md border bg-card">
      <table className="w-full border-collapse text-sm" aria-label={label}>
        {children}
      </table>
    </div>
  );
}

export function Th({ children, align = "left" }: { children?: ReactNode; align?: "left" | "right" }) {
  return (
    <th
      scope="col"
      className={cn(
        "border-b bg-header-row px-4 py-2.5 text-xs font-medium whitespace-nowrap text-muted-foreground",
        align === "right" ? "text-right" : "text-left",
      )}
    >
      {children}
    </th>
  );
}

export function Td({ children, align = "left", className }: { children?: ReactNode; align?: "left" | "right"; className?: string }) {
  return (
    <td className={cn("border-b px-4 py-3 align-middle group-last:border-b-0", align === "right" && "text-right", className)}>
      {children}
    </td>
  );
}

export function Tag({ children }: { children: ReactNode }) {
  return (
    <span className="inline-flex items-center rounded border border-foreground/20 px-1.5 py-px text-[11px] font-semibold tracking-wide text-foreground uppercase">
      {children}
    </span>
  );
}

export function DecisionText({ outcome }: { outcome: FinalDecision }) {
  const { t } = useTranslation();
  return (
    <span className={cn("font-semibold", outcome === "Approve" ? "text-approve" : "text-reject")}>
      {t(`enums.FinalDecision.${outcome}`)}
    </span>
  );
}

export function Facts({ items, stacked = false }: { items: { label: string; value: ReactNode }[]; stacked?: boolean }) {
  if (stacked)
    return (
      <dl className="grid content-start gap-3">
        {items.map((item) => (
          <div key={item.label}>
            <dt className="text-xs text-muted-foreground">{item.label}</dt>
            <dd className="mt-0.5 break-all">{item.value}</dd>
          </div>
        ))}
      </dl>
    );
  return (
    <dl className="grid content-start grid-cols-[minmax(8rem,max-content)_minmax(0,1fr)] gap-x-6 gap-y-2">
      {items.map((item) => (
        <div key={item.label} className="contents">
          <dt className="text-muted-foreground">{item.label}</dt>
          <dd className="text-right">{item.value}</dd>
        </div>
      ))}
    </dl>
  );
}

export function FieldErrors({ id, errors }: { id: string; errors?: FieldError[] }) {
  if (!errors?.length) return null;
  return (
    <div id={id} className="text-xs text-reject">
      {errors.map((error, index) => (
        <p key={index}>{fieldErrorMessage(error)}</p>
      ))}
    </div>
  );
}

type TextareaFieldProps = Omit<ComponentProps<"textarea">, "id"> & { label: string; hint?: string; errors?: FieldError[] };

export function TextareaField({ label, hint, errors, className, ...props }: TextareaFieldProps) {
  const id = useId();
  const invalid = !!errors?.length;
  const describedBy = [hint && `${id}-hint`, invalid && `${id}-error`].filter(Boolean).join(" ") || undefined;
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <textarea
        id={id}
        aria-invalid={invalid || undefined}
        aria-describedby={describedBy}
        className={cn(
          "min-h-20 w-full rounded-md border border-input bg-card px-3 py-2 text-sm outline-none placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive",
          className,
        )}
        {...props}
      />
      {hint && (
        <p id={`${id}-hint`} className="text-xs text-muted-foreground">
          {hint}
        </p>
      )}
      <FieldErrors id={`${id}-error`} errors={errors} />
    </div>
  );
}

type SelectFieldProps = Omit<ComponentProps<"select">, "id"> & {
  label: string;
  options: { value: string; label: string }[];
  placeholder?: string;
  errors?: FieldError[];
  hideLabel?: boolean;
};

export function SelectField({ label, options, placeholder, errors, hideLabel, className, ...props }: SelectFieldProps) {
  const id = useId();
  const invalid = !!errors?.length;
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id} className={cn(hideLabel && "sr-only")}>
        {label}
      </Label>
      <select
        id={id}
        aria-invalid={invalid || undefined}
        aria-describedby={invalid ? `${id}-error` : undefined}
        className={cn(
          "h-9 w-full rounded-md border border-input bg-card px-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive",
          className,
        )}
        {...props}
      >
        {placeholder !== undefined && <option value="">{placeholder}</option>}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      <FieldErrors id={`${id}-error`} errors={errors} />
    </div>
  );
}

export function Alert({ children }: { children: ReactNode }) {
  return (
    <p role="alert" className="rounded border border-reject/30 bg-reject-tint px-3 py-2 text-reject">
      {children}
    </p>
  );
}
