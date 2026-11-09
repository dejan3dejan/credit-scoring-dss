import { useId, type ComponentProps } from "react";
import type { FieldError } from "@/api/types";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { fieldErrorMessage } from "@/i18n";

type TextFieldProps = Omit<ComponentProps<typeof Input>, "id"> & {
  label: string;
  hint?: string;
  errors?: FieldError[];
};

export function TextField({ label, hint, errors, ...inputProps }: TextFieldProps) {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const invalid = !!errors?.length;
  const describedBy = [hint && hintId, invalid && errorId].filter(Boolean).join(" ") || undefined;

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} aria-invalid={invalid || undefined} aria-describedby={describedBy} {...inputProps} />
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
