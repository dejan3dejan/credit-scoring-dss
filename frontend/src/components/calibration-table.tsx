import { useTranslation } from "react-i18next";
import type { DashboardStats } from "@/api/stats";
import { Table, Td, Th } from "@/components/surface";
import { useLanguage } from "@/i18n";
import { formatPercent } from "@/i18n/format";

type Band = DashboardStats["calibration"][number];

export function CalibrationTable({ bands, label }: { bands: Band[]; label: string }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const pct = (value: number) => formatPercent(value, language);
  return (
    <Table label={label}>
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
  );
}
