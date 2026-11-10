import { describe, expect, it } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { ChartLegend, ChartTooltipContent } from "@/components/ui/chart";

describe("chart parts", () => {
  it("shows the tooltip as one card with the label and a row per series", () => {
    render(
      <ChartTooltipContent
        active
        label="2026-10-26"
        labelFormatter={(label) => `Week of ${label}`}
        valueFormatter={(value) => `${value} apps`}
        payload={[
          { name: "Approve", value: 6, color: "#2c5a85", dataKey: "approve" },
          { name: "Reject", value: 1, color: "#9b1c1c", dataKey: "reject" },
        ]}
      />,
    );
    const card = screen.getByRole("status");
    expect(within(card).getByText("Week of 2026-10-26")).toBeInTheDocument();
    expect(within(card).getByText("Approve")).toBeInTheDocument();
    expect(within(card).getByText("6 apps")).toBeInTheDocument();
    expect(within(card).getByText("1 apps")).toBeInTheDocument();
  });

  it("shows nothing when the pointer is not over a point", () => {
    const { container } = render(<ChartTooltipContent active={false} payload={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("lists the series as chips in grey text", () => {
    render(
      <ChartLegend
        label="Series"
        items={[
          { label: "Approve", color: "var(--status-approve)" },
          { label: "Review closely", color: "var(--status-review)", dashed: true },
        ]}
      />,
    );
    const legend = screen.getByRole("list", { name: "Series" });
    expect(within(legend).getAllByRole("listitem").map((item) => item.textContent)).toEqual(["Approve", "Review closely"]);
  });
});
