import type { ReactNode } from "react"
import { cn } from "@/lib/utils"

export const chartAxis = {
  axisLine: false,
  tickLine: false,
  tickMargin: 8,
  tick: { fill: "var(--muted-foreground)", fontSize: 11 },
} as const

export const chartGrid = { vertical: false, stroke: "var(--border)", strokeDasharray: "3 3" } as const

export const chartCursor = { fill: "var(--muted)", radius: 4 } as const

type TooltipItem = { name?: ReactNode; value?: unknown; color?: string; dataKey?: unknown; payload?: unknown }

export function ChartTooltipContent({
  active,
  payload,
  label,
  labelFormatter,
  valueFormatter,
}: {
  active?: boolean
  payload?: readonly TooltipItem[]
  label?: unknown
  labelFormatter?: (label: unknown, payload: readonly TooltipItem[]) => ReactNode
  valueFormatter?: (value: unknown, item: TooltipItem) => ReactNode
}) {
  if (!active || !payload?.length) return null
  const heading = labelFormatter ? labelFormatter(label, payload) : (label as ReactNode)
  return (
    <div role="status" className="min-w-36 rounded-md border bg-popover px-3 py-2 text-xs text-popover-foreground shadow-md">
      {heading && <div className="mb-1.5 font-medium">{heading}</div>}
      <div className="grid gap-1">
        {payload.map((item, index) => (
          <div key={String(item.dataKey ?? index)} className="flex items-center gap-2">
            <span className="size-2.5 shrink-0 rounded-[2px]" style={{ background: item.color }} aria-hidden="true" />
            <span className="text-muted-foreground">{item.name}</span>
            <span className="ml-auto pl-3 font-medium tabular-nums">
              {valueFormatter ? valueFormatter(item.value, item) : (item.value as ReactNode)}
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}

type StackSegment = { x?: number; y?: number; width?: number; height?: number; fill?: string; payload?: Record<string, unknown> }

export function stackTop(keys: readonly string[], key: string, radius = 4) {
  const above = keys.slice(keys.indexOf(key) + 1)
  return function StackTop({ x = 0, y = 0, width = 0, height = 0, fill, payload }: StackSegment) {
    if (height <= 0) return <g />
    const top = above.every((other) => !Number(payload?.[other]))
    const r = top ? Math.min(radius, width / 2, height) : 0
    return (
      <path
        d={`M${x},${y + height}V${y + r}Q${x},${y} ${x + r},${y}H${x + width - r}Q${x + width},${y} ${x + width},${y + r}V${y + height}Z`}
        fill={fill}
        stroke="var(--card)"
        strokeWidth={2}
      />
    )
  }
}

export type ChartLegendItem = { label: string; color: string; dashed?: boolean }

export function ChartLegend({ label, items, className }: { label: string; items: ChartLegendItem[]; className?: string }) {
  return (
    <ul aria-label={label} className={cn("flex flex-wrap items-center justify-center gap-x-4 gap-y-1 pt-3 text-xs text-muted-foreground", className)}>
      {items.map((item) => (
        <li key={item.label} className="flex items-center gap-1.5">
          {item.dashed ? (
            <span className="h-0 w-3 border-t-2 border-dashed" style={{ borderColor: item.color }} aria-hidden="true" />
          ) : (
            <span className="size-2.5 rounded-[2px]" style={{ background: item.color }} aria-hidden="true" />
          )}
          {item.label}
        </li>
      ))}
    </ul>
  )
}
