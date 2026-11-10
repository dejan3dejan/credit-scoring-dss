import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { I18nextProvider } from "react-i18next";
import { i18n } from "@/i18n";
import { Loading } from "@/components/page";

describe("Loading", () => {
  it("is announced as a status and shows the dial loader", () => {
    const { container } = render(
      <I18nextProvider i18n={i18n}>
        <Loading />
      </I18nextProvider>,
    );
    expect(screen.getByRole("status")).toHaveTextContent("Loading");
    expect(container.querySelectorAll("circle.dial-dot")).toHaveLength(3);
  });
});
