import { useId, type ComponentProps } from "react";
import type { FieldError } from "@/api/types";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { fieldErrorMessage } from "@/i18n";
import { cn } from "@/lib/utils";

type TextFieldProps = Omit<ComponentProps<typeof Input>, "id"> & {
  label: string;
  hint?: string;
  suffix?: string;
  errors?: FieldError[];
};

export function TextField({ label, hint, suffix, errors, className, ...inputProps }: TextFieldProps) {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const invalid = !!errors?.length;
  const describedBy = [hint && hintId, invalid && errorId].filter(Boolean).join(" ") || undefined;

  return (
    <div className="grid content-start gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <div className="relative">
        <Input
          id={id}
          aria-invalid={invalid || undefined}
          aria-describedby={describedBy}
          className={cn("bg-card", suffix && "pr-12", className)}
          {...inputProps}
        />
        {suffix && (
          <span className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-xs text-muted-foreground" aria-hidden="true">
            {suffix}
          </span>
        )}
      </div>
      {hint && (
        <p id={hintId} className="text-xs text-muted-foreground">
          {hint}
        </p>
      )}
      {invalid && (
        <div id={errorId} className="text-xs text-reject">
          {errors.map((error, index) => (
            <p key={index}>{fieldErrorMessage(error)}</p>
          ))}
        </div>
      )}
    </div>
  );
}
