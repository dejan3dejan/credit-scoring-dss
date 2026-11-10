import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { I18nextProvider } from "react-i18next";
import { i18n } from "@/i18n";
import { StatusBadge } from "@/components/status-badge";

function renderBadge(recommendation: Parameters<typeof StatusBadge>[0]["recommendation"]) {
  return render(
    <I18nextProvider i18n={i18n}>
      <StatusBadge recommendation={recommendation} />
    </I18nextProvider>,
  );
}

describe("StatusBadge", () => {
  it.each([
    ["Approve", "Approve"],
    ["Review", "Review closely"],
    ["Reject", "Reject"],
    [null, "No score"],
  ] as const)("shows %s as the word %s", (recommendation, word) => {
    renderBadge(recommendation);
    expect(screen.getByText(word)).toBeInTheDocument();
  });

  it("shows the word in Serbian", async () => {
    await i18n.changeLanguage("sr");
    renderBadge("Reject");
    expect(screen.getByText("Odbiti")).toBeInTheDocument();
  });

  it.each([
    ["Approve", "lucide-signal-low"],
    ["Review", "lucide-signal-medium"],
    ["Reject", "lucide-signal-high"],
  ] as const)("shows %s with the risk level icon %s, never a check mark", (recommendation, icon) => {
    const { container } = renderBadge(recommendation);
    expect(container.querySelector(`svg.${icon}`)).not.toBeNull();
    expect(container.querySelector("svg.lucide-circle-check, svg.lucide-circle-x, svg.lucide-triangle-alert")).toBeNull();
  });
});
