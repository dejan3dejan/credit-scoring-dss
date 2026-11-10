import type { DashboardStats } from "@/api/stats";

export function calibratedBands(calibration: DashboardStats["calibration"]) {
  return calibration.filter((band) => band.loans > 0 && band.meanPredicted !== null && band.observedDefaultRate !== null);
}
