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

  it("carries an icon next to the word", () => {
    const { container } = renderBadge("Approve");
    expect(container.querySelector("svg")).not.toBeNull();
  });
});
