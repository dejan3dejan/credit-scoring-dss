import { lazy } from "react";

export const LazyDashboard = lazy(() => import("@/pages/dashboard").then((module) => ({ default: module.DashboardPage })));
