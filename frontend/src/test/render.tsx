import { render } from "@testing-library/react";
import { createMemoryHistory } from "@tanstack/react-router";
import { AppProviders } from "@/app";
import { createAppRouter, createQueryClient } from "@/router";

export function renderApp(path: string) {
  const queryClient = createQueryClient();
  const history = createMemoryHistory({ initialEntries: [`/app${path}`] });
  const router = createAppRouter(queryClient, history);
  const view = render(<AppProviders queryClient={queryClient} router={router} />);
  return { ...view, router, queryClient };
}
