import "@testing-library/jest-dom/vitest";
import { afterEach, beforeEach, vi } from "vitest";
import { cleanup, configure } from "@testing-library/react";
import { i18n } from "@/i18n";

window.scrollTo = () => {};
configure({ asyncUtilTimeout: 4000 });

beforeEach(async () => {
  localStorage.clear();
  await i18n.changeLanguage("en");
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});
