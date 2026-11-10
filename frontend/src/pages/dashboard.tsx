import type { ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ReferenceLine,
  ResponsiveContainer,
  Scatter,
  ScatterChart,
  Tooltip,
  XAxis,
  YAxis,
  ZAxis,
} from "recharts";
import { listModels, modelsKey } from "@/api/models";
import { listBranches } from "@/api/branches";
import { getStats, type DashboardStats } from "@/api/stats";
import { useMe } from "@/auth/session";
import { EmptyState, ErrorState, Loading, PageHeader } from "@/components/page";
import { Card, SelectField, Table, Td, Th } from "@/components/surface";
import { useLanguage } from "@/i18n";
import { formatPercent, formatShortDate } from "@/i18n/format";
import { rangeFilters, type DashboardSearch } from "@/lib/dashboard-range";
import { cn } from "@/lib/utils";

const ink = "var(--foreground)";
const muted = "var(--muted-foreground)";
const grid = "var(--border)";
const axis = { stroke: grid, tick: { fill: muted, fontSize: 11 }, tickLine: false } as const;
const tooltipStyle = { contentStyle: { border: "1px solid var(--border)", borderRadius: 6, fontSize: 12 } };

type Rate = number | null;

export function DashboardPage({ search }: { search: DashboardSearch }) {
  const { t } = useTranslation();
  const me = useMe();
  const navigate = useNavigate();
  const filters = { ...rangeFilters(search.range, new Date()), branchId: search.branchId };
  const stats = useQuery({ queryKey: ["stats", filters], queryFn: () => getStats(filters) });
  const crossBranch = !me.branch;
  const branches = useQuery({ queryKey: ["branches"], queryFn: listBranches, enabled: crossBranch });

  return (
    <>
      <PageHeader title={t("dashboard.title")} description={t("dashboard.intro")} />
      <div className="flex flex-wrap items-end gap-3">
        <div role="group" aria-label={t("dashboard.range")} className="flex overflow-hidden rounded-md border bg-card">
          {(["30", "90", "all"] as const).map((range) => (
            <Link
              key={range}
              to="/dashboard"
              search={{ ...search, range: range === "30" ? undefined : range }}
              aria-current={search.range === range ? "page" : undefined}
              className={cn(
                "px-3 py-2 text-sm",
                search.range === range ? "bg-primary font-semibold text-primary-foreground" : "text-muted-foreground hover:text-foreground",
              )}
            >
              {t(range === "30" ? "dashboard.last30" : range === "90" ? "dashboard.last90" : "dashboard.allTime")}
            </Link>
          ))}
        </div>
        {crossBranch && branches.data && (
          <div className="w-56">
            <SelectField
              label={t("dashboard.branch")}
              value={search.branchId ?? ""}
              placeholder={t("dashboard.allBranches")}
              options={branches.data.map((branch) => ({ value: branch.id, label: branch.name }))}
              onChange={(event) => void navigate({ to: "/dashboard", search: { ...search, branchId: event.target.value || undefined } })}
            />
          </div>
        )}
      </div>
      {stats.isPending ? (
        <Loading />
      ) : stats.isError ? (
        <ErrorState error={stats.error} onRetry={() => void stats.refetch()} />
      ) : (
        <Numbers stats={stats.data} showBranches={crossBranch} />
      )}
    </>
  );
}

function Numbers({ stats, showBranches }: { stats: DashboardStats; showBranches: boolean }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const percent = (value: Rate) => (value === null ? t("dashboard.none") : formatPercent(value, language));
  const rows = Object.values(stats.matrix);
  const approved = rows.reduce((sum, row) => sum + row.approved, 0);
  const rejected = rows.reduce((sum, row) => sum + row.rejected, 0);
  const approvalRate = approved + rejected > 0 ? approved / (approved + rejected) : null;
  const review = stats.matrix.review;
  const reviewShare = stats.scored > 0 ? (review.approved + review.rejected + review.pending) / stats.scored : null;
  const previous = stats.comparison?.previous;

  const tiles: { label: string; value: string; previous?: string; note?: string }[] = [
    { label: t("dashboard.applications"), value: String(stats.applications), previous: previous && String(previous.applications) },
    { label: t("dashboard.decided"), value: String(stats.decided), previous: previous && String(previous.decided) },
    { label: t("dashboard.approvalRate"), value: percent(approvalRate), previous: previous && percent(previous.approvalRate) },
    {
      label: t("dashboard.overrideRate"),
      value: percent(stats.overrideRate),
      previous: previous && percent(previous.overrideRate),
      note: t("dashboard.overrideSides", { high: percent(stats.highSideOverrideRate), low: percent(stats.lowSideOverrideRate) }),
    },
    { label: t("dashboard.reviewShare"), value: percent(reviewShare), previous: previous && percent(previous.reviewShare) },
    { label: t("dashboard.unavailableRate"), value: percent(stats.unavailableRate), previous: previous && percent(previous.unavailableRate) },
  ];

  return (
    <>
      <ul aria-label={t("dashboard.keyNumbers")} className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        {tiles.map((tile) => (
          <li key={tile.label} className="rounded-md border bg-card px-4 py-3">
            <div className="text-xs text-muted-foreground">{tile.label}</div>
            <div className="mt-1 text-[26px] leading-none font-semibold">{tile.value}</div>
            {tile.previous !== undefined && (
              <div className="mt-1.5 text-xs text-muted-foreground">{t("dashboard.previous", { value: tile.previous })}</div>
            )}
            {tile.note && <div className="mt-1.5 text-[11px] leading-snug text-muted-foreground">{tile.note}</div>}
          </li>
        ))}
      </ul>
      {stats.applications === 0 ? (
        <EmptyState title={t("dashboard.empty")} text={t("dashboard.emptyText")} />
      ) : (
        <>
          <div className="grid gap-5 xl:grid-cols-2">
            <WeeklyChart stats={stats} />
            <RatesChart stats={stats} />
            <Histogram stats={stats} />
            <Matrix stats={stats} />
          </div>
          <div className="grid gap-5 xl:grid-cols-2">
            <Officers stats={stats} />
            {showBranches && <Branches stats={stats} />}
          </div>
          <Outcomes stats={stats} />
          <div className="grid gap-5 xl:grid-cols-2">
            <Calibration stats={stats} />
            <Shadows stats={stats} />
          </div>
        </>
      )}
    </>
  );
}

function ChartCard({ title, note, table, children }: { title: string; note?: string; table: ReactNode; children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <Card title={title} footer={note}>
      <div className="h-64">{children}</div>
      <details className="mt-3 text-sm">
        <summary className="cursor-pointer text-muted-foreground hover:text-foreground">{t("dashboard.showTable")}</summary>
        <div className="mt-2">{table}</div>
      </details>
    </Card>
  );
}

function WeeklyChart({ stats }: { stats: DashboardStats }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const week = (value: string) => formatShortDate(value, language, "UTC");
  const series = [
    { key: "approve", label: t("recommendation.Approve"), color: "var(--status-approve)" },
    { key: "review", label: t("recommendation.Review"), color: "var(--status-review)" },
    { key: "reject", label: t("recommendation.Reject"), color: "var(--status-reject)" },
    { key: "unavailable", label: t("recommendation.none"), color: "var(--status-none)" },
  ] as const;
  const title = t("dashboard.weeklyTitle");

  return (
    <ChartCard
      title={title}
      table={
        <Table label={title}>
          <thead>
            <tr>
              <Th>{t("dashboard.week")}</Th>
              {series.map((s) => (
                <Th key={s.key} align="right">
                  {s.label}
                </Th>
              ))}
            </tr>
          </thead>
          <tbody>
            {stats.weekly.map((w) => (
              <tr key={w.weekStart} className="group">
                <Td>{week(w.weekStart)}</Td>
                {series.map((s) => (
                  <Td key={s.key} align="right">
                    {w[s.key]}
                  </Td>
                ))}
              </tr>
            ))}
          </tbody>
        </Table>
      }
    >
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={stats.weekly} margin={{ top: 8, right: 8, bottom: 0, left: -16 }}>
          <CartesianGrid vertical={false} stroke={grid} />
          <XAxis dataKey="weekStart" tickFormatter={week} {...axis} />
          <YAxis allowDecimals={false} {...axis} />
          <Tooltip labelFormatter={(value) => week(String(value))} {...tooltipStyle} />
          <Legend wrapperStyle={{ fontSize: 12 }} itemSorter={(item) => series.findIndex((s) => s.key === item.dataKey)} />
          {series.map((s, index) => (
            <Bar
              isAnimationActive={false}
              key={s.key}
              dataKey={s.key}
              name={s.label}
              stackId="week"
              fill={s.color}
              stroke="var(--card)"
              strokeWidth={1}
              radius={index === series.length - 1 ? [3, 3, 0, 0] : undefined}
            />
          ))}
        </BarChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}

function RatesChart({ stats }: { stats: DashboardStats }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const week = (value: string) => formatShortDate(value, language, "UTC");
  const percent = (value: Rate) => (value === null ? t("dashboard.none") : formatPercent(value, language));
  const title = t("dashboard.ratesTitle");

  return (
    <ChartCard
      title={title}
      note={t("dashboard.ratesNote")}
      table={
        <Table label={title}>
          <thead>
            <tr>
              <Th>{t("dashboard.week")}</Th>
              <Th align="right">{t("dashboard.overrideRate")}</Th>
              <Th align="right">{t("dashboard.reviewShare")}</Th>
            </tr>
          </thead>
          <tbody>
            {stats.weekly.map((w) => (
              <tr key={w.weekStart} className="group">
                <Td>{week(w.weekStart)}</Td>
                <Td align="right">{percent(w.overrideRate)}</Td>
                <Td align="right">{percent(w.reviewShare)}</Td>
              </tr>
            ))}
          </tbody>
        </Table>
      }
    >
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={stats.weekly} margin={{ top: 8, right: 8, bottom: 0, left: -8 }}>
          <CartesianGrid vertical={false} stroke={grid} />
          <XAxis dataKey="weekStart" tickFormatter={week} {...axis} />
          <YAxis domain={[0, 1]} tickFormatter={(value: number) => formatPercent(value, language)} {...axis} />
          <Tooltip labelFormatter={(value) => week(String(value))} formatter={(value) => percent(value as Rate)} {...tooltipStyle} />
          <Legend wrapperStyle={{ fontSize: 12 }} />
          <Line isAnimationActive={false} dataKey="overrideRate" name={t("dashboard.overrideRate")} stroke={ink} strokeWidth={2} dot={{ r: 4 }} connectNulls={false} />
          <Line
            isAnimationActive={false}
            dataKey="reviewShare"
            name={t("dashboard.reviewShare")}
            stroke={muted}
            strokeWidth={2}
            strokeDasharray="5 4"
            dot={{ r: 4, strokeDasharray: "0" }}
            connectNulls={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}

function Histogram({ stats }: { stats: DashboardStats }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const models = useQuery({ queryKey: modelsKey, queryFn: listModels });
  const active = models.data?.find((m) => m.role === "Active");
  const pct = (value: number) => formatPercent(value, language);
  const data = stats.pBadHistogram.map((bin) => ({ ...bin, middle: (bin.from + bin.to) / 2 }));
  const title = t("dashboard.histogramTitle");

  return (
    <ChartCard
      title={title}
      note={active ? t("dashboard.histogramNote") : undefined}
      table={
        <Table label={title}>
          <thead>
            <tr>
              <Th>{t("dashboard.band")}</Th>
              <Th align="right">{t("dashboard.count")}</Th>
            </tr>
          </thead>
          <tbody>
            {stats.pBadHistogram.map((bin) => (
              <tr key={bin.from} className="group">
                <Td>
                  {pct(bin.from)} – {pct(bin.to)}
                </Td>
                <Td align="right">{bin.count}</Td>
              </tr>
            ))}
          </tbody>
        </Table>
      }
    >
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} margin={{ top: 16, right: 12, bottom: 0, left: -16 }} barCategoryGap={2}>
          <CartesianGrid vertical={false} stroke={grid} />
          <XAxis
            dataKey="middle"
            type="number"
            domain={[0, 1]}
            ticks={[0, 0.2, 0.4, 0.6, 0.8, 1]}
            tickFormatter={(value: number) => formatPercent(value, language)}
            {...axis}
          />
          <YAxis allowDecimals={false} {...axis} />
          <Tooltip
            labelFormatter={(_, payload) => {
              const bin = payload?.[0]?.payload as { from: number; to: number } | undefined;
              return bin ? `${pct(bin.from)} – ${pct(bin.to)}` : "";
            }}
            {...tooltipStyle}
          />
          <Bar isAnimationActive={false} dataKey="count" name={t("dashboard.count")} fill="#7D8782" radius={[3, 3, 0, 0]} barSize={28} />
          {active?.reviewFrom != null && (
            <ReferenceLine
              x={active.reviewFrom}
              stroke="var(--status-review)"
              strokeDasharray="4 3"
              label={{ value: t("details.reviewFrom", { value: pct(active.reviewFrom) }), position: "insideTopRight", fontSize: 11, fill: muted }}
            />
          )}
          {active && (
            <ReferenceLine
              x={active.threshold}
              stroke="var(--status-reject)"
              strokeDasharray="4 3"
              label={{ value: t("details.rejectFrom", { value: pct(active.threshold) }), position: "insideTopLeft", fontSize: 11, fill: muted }}
            />
          )}
        </BarChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}

function Matrix({ stats }: { stats: DashboardStats }) {
  const { t } = useTranslation();
  const rows = [
    { label: t("recommendation.Approve"), row: stats.matrix.approve },
    { label: t("recommendation.Review"), row: stats.matrix.review },
    { label: t("recommendation.Reject"), row: stats.matrix.reject },
    { label: t("recommendation.none"), row: stats.matrix.unavailable },
  ];
  const title = t("dashboard.matrixTitle");
  return (
    <Card title={title}>
      <Table label={title}>
        <thead>
          <tr>
            <Th>{t("list.recommendation")}</Th>
            <Th align="right">{t("dashboard.officerApproved")}</Th>
            <Th align="right">{t("dashboard.officerRejected")}</Th>
            <Th align="right">{t("dashboard.awaiting")}</Th>
          </tr>
        </thead>
        <tbody>
          {rows.map(({ label, row }) => (
            <tr key={label} className="group">
              <th scope="row" className="border-b px-4 py-3 text-left font-medium group-last:border-b-0">
                {label}
              </th>
              <Td align="right">{row.approved}</Td>
              <Td align="right">{row.rejected}</Td>
              <Td align="right">{row.pending}</Td>
            </tr>
          ))}
        </tbody>
      </Table>
    </Card>
  );
}

function Officers({ stats }: { stats: DashboardStats }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const title = t("dashboard.officersTitle");
  return (
    <Card title={title}>
      <Table label={title}>
        <thead>
          <tr>
            <Th>{t("dashboard.officer")}</Th>
            <Th align="right">{t("dashboard.decisions")}</Th>
            <Th align="right">{t("dashboard.overrides")}</Th>
            <Th align="right">{t("dashboard.overrideRate")}</Th>
          </tr>
        </thead>
        <tbody>
          {stats.officers.map((officer) => (
            <tr key={officer.name} className="group">
              <Td>{officer.name}</Td>
              <Td align="right">{officer.decisions}</Td>
              <Td align="right">{officer.overrides}</Td>
              <Td align="right">{officer.overrideRate === null ? "–" : formatPercent(officer.overrideRate, language)}</Td>
            </tr>
          ))}
        </tbody>
      </Table>
    </Card>
  );
}

function Branches({ stats }: { stats: DashboardStats }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const percent = (value: Rate) => (value === null ? "–" : formatPercent(value, language));
  const title = t("dashboard.branchesTitle");
  return (
    <Card title={title}>
      <Table label={title}>
        <thead>
          <tr>
            <Th>{t("dashboard.branch")}</Th>
            <Th align="right">{t("dashboard.applications")}</Th>
            <Th align="right">{t("dashboard.decided")}</Th>
            <Th align="right">{t("dashboard.overrideRate")}</Th>
            <Th align="right">{t("dashboard.reviewShare")}</Th>
          </tr>
        </thead>
        <tbody>
          {stats.byBranch.map((branch) => (
            <tr key={branch.branchId} className="group">
              <Td>{branch.name}</Td>
              <Td align="right">{branch.applications}</Td>
              <Td align="right">{branch.decided}</Td>
              <Td align="right">{percent(branch.overrideRate)}</Td>
              <Td align="right">{percent(branch.reviewShare)}</Td>
            </tr>
          ))}
        </tbody>
      </Table>
    </Card>
  );
}

function Shadows({ stats }: { stats: DashboardStats }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const title = t("dashboard.shadowTitle");
  return (
    <Card title={title} footer={t("dashboard.shadowNote")}>
      {stats.shadowAgreement.length === 0 ? (
        <p className="text-muted-foreground">{t("dashboard.noShadows")}</p>
      ) : (
        <Table label={title}>
          <thead>
            <tr>
              <Th>{t("dashboard.model")}</Th>
              <Th align="right">{t("dashboard.compared")}</Th>
              <Th align="right">{t("dashboard.agreed")}</Th>
              <Th align="right">{t("dashboard.agreement")}</Th>
            </tr>
          </thead>
          <tbody>
            {stats.shadowAgreement.map((model) => (
              <tr key={model.modelId} className="group">
                <Td className="font-mono text-xs">{model.modelId}</Td>
                <Td align="right">{model.compared}</Td>
                <Td align="right">{model.agreed}</Td>
                <Td align="right">{model.agreementRate === null ? "–" : formatPercent(model.agreementRate, language)}</Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </Card>
  );
}

function Outcomes({ stats }: { stats: DashboardStats }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const title = t("dashboard.outcomesTitle");
  return (
    <Card title={title} footer={t("dashboard.outcomesNote")}>
      <Table label={title}>
        <thead>
          <tr>
            <Th>{t("dashboard.path")}</Th>
            <Th align="right">{t("dashboard.approved")}</Th>
            <Th align="right">{t("dashboard.withOutcome")}</Th>
            <Th align="right">{t("dashboard.defaulted")}</Th>
            <Th align="right">{t("dashboard.defaultRate")}</Th>
          </tr>
        </thead>
        <tbody>
          {stats.outcomeByDecision.map((group) => (
            <tr key={group.path} className="group">
              <Td>{t(`dashboard.paths.${group.path}`)}</Td>
              <Td align="right">{group.approved}</Td>
              <Td align="right">{group.withOutcome}</Td>
              <Td align="right">{group.defaulted}</Td>
              <Td align="right">{group.defaultRate === null ? "–" : formatPercent(group.defaultRate, language)}</Td>
            </tr>
          ))}
        </tbody>
      </Table>
    </Card>
  );
}

function Calibration({ stats }: { stats: DashboardStats }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const pct = (value: number) => formatPercent(value, language);
  const bands = stats.calibration.filter((band) => band.loans > 0 && band.meanPredicted !== null && band.observedDefaultRate !== null);
  const title = t("dashboard.calibrationTitle");

  if (bands.length === 0)
    return (
      <Card title={title}>
        <p className="text-muted-foreground">{t("dashboard.noOutcomes")}</p>
      </Card>
    );

  return (
    <ChartCard
      title={title}
      note={t("dashboard.calibrationNote")}
      table={
        <Table label={title}>
          <thead>
            <tr>
              <Th>{t("dashboard.band")}</Th>
              <Th align="right">{t("dashboard.loans")}</Th>
              <Th align="right">{t("dashboard.predicted")}</Th>
              <Th align="right">{t("dashboard.observed")}</Th>
            </tr>
          </thead>
          <tbody>
            {bands.map((band) => (
              <tr key={band.from} className="group">
                <Td>
                  {pct(band.from)} – {pct(band.to)}
                </Td>
                <Td align="right">{band.loans}</Td>
                <Td align="right">{pct(band.meanPredicted!)}</Td>
                <Td align="right">{pct(band.observedDefaultRate!)}</Td>
              </tr>
            ))}
          </tbody>
        </Table>
      }
    >
      <ResponsiveContainer width="100%" height="100%">
        <ScatterChart margin={{ top: 8, right: 12, bottom: 0, left: -8 }}>
          <CartesianGrid stroke={grid} />
          <XAxis
            type="number"
            dataKey="meanPredicted"
            name={t("dashboard.predicted")}
            domain={[0, 1]}
            tickFormatter={(value: number) => pct(value)}
            {...axis}
          />
          <YAxis
            type="number"
            dataKey="observedDefaultRate"
            name={t("dashboard.observed")}
            domain={[0, 1]}
            tickFormatter={(value: number) => pct(value)}
            {...axis}
          />
          <ZAxis type="number" dataKey="loans" range={[60, 260]} name={t("dashboard.loans")} />
          <ReferenceLine segment={[{ x: 0, y: 0 }, { x: 1, y: 1 }]} stroke={muted} strokeDasharray="4 3" />
          <Tooltip formatter={(value, name) => (name === t("dashboard.loans") ? value : pct(value as number))} {...tooltipStyle} />
          <Scatter isAnimationActive={false} data={bands} fill={ink} stroke="var(--card)" strokeWidth={2} />
        </ScatterChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
