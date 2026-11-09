import { QueryClient } from "@tanstack/react-query";
import {
  createRootRouteWithContext,
  createRoute,
  createRouter,
  Outlet,
  redirect,
  type RouterHistory,
} from "@tanstack/react-router";
import { ApiError } from "@/api/client";
import type { Permission } from "@/api/types";
import { meQuery } from "@/auth/session";
import { AppShell } from "@/components/app-shell";
import { ErrorState, Loading } from "@/components/page";
import { Toaster } from "@/components/ui/sonner";
import { applyLanguage } from "@/i18n";
import { ChangePasswordPage } from "@/pages/change-password";
import { HomePage } from "@/pages/home";
import { LoginPage } from "@/pages/login";
import { NoAccessPage, NotBuiltPage, NotFoundPage } from "@/pages/status-pages";
import { ApplicationDetailsPage } from "@/pages/application-details";
import { ApplicationsListPage } from "@/pages/applications-list";
import { ClientDetailsPage, ClientsListPage, NewClientPage } from "@/pages/clients";
import { NewApplicationPage } from "@/pages/new-application";
import { ModelsPage } from "@/pages/models";
import type { ClientFilters } from "@/api/clients";
import { validateApplicationFilters } from "@/lib/filters";

type RouterContext = { queryClient: QueryClient };

const rootRoute = createRootRouteWithContext<RouterContext>()({
  component: () => (
    <>
      <Outlet />
      <Toaster position="top-right" />
    </>
  ),
  notFoundComponent: NotFoundPage,
});

const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "login",
  validateSearch: (search: Record<string, unknown>): { redirect?: string } =>
    typeof search.redirect === "string" ? { redirect: search.redirect } : {},
  component: function Login() {
    const { redirect: target } = loginRoute.useSearch();
    return <LoginPage redirect={target} />;
  },
});

const authedRoute = createRoute({
  getParentRoute: () => rootRoute,
  id: "authed",
  beforeLoad: async ({ context, location }) => {
    let me;
    try {
      me = await context.queryClient.ensureQueryData(meQuery);
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        context.queryClient.removeQueries({ queryKey: meQuery.queryKey });
        throw redirect({ to: "/login", search: { redirect: location.href } });
      }
      throw error;
    }
    await applyLanguage(me.preferredLanguage);
    if (me.mustChangePassword && location.pathname !== "/account/password") throw redirect({ to: "/account/password" });
    return { me };
  },
  component: AppShell,
});

function requires(permission: Permission) {
  return ({ context }: { context: { me: { permissions: Permission[] } } }) => {
    if (!context.me.permissions.includes(permission)) throw redirect({ to: "/no-access", replace: true });
  };
}

function page<TPath extends string>(path: TPath, titleKey: string, permission: Permission) {
  return createRoute({
    getParentRoute: () => authedRoute,
    path,
    beforeLoad: requires(permission),
    component: () => <NotBuiltPage titleKey={titleKey} />,
  });
}

const homeRoute = createRoute({ getParentRoute: () => authedRoute, path: "/", component: HomePage });
const passwordRoute = createRoute({ getParentRoute: () => authedRoute, path: "account/password", component: ChangePasswordPage });
const noAccessRoute = createRoute({ getParentRoute: () => authedRoute, path: "no-access", component: NoAccessPage });

const applicationsRoute = createRoute({
  getParentRoute: () => authedRoute,
  path: "applications",
  validateSearch: validateApplicationFilters,
  beforeLoad: requires("applications.read"),
  component: function Applications() {
    return <ApplicationsListPage filters={applicationsRoute.useSearch()} />;
  },
});

const applicationRoute = createRoute({
  getParentRoute: () => authedRoute,
  path: "applications/$id",
  beforeLoad: requires("applications.read"),
  component: function Application() {
    const { id } = applicationRoute.useParams();
    return <ApplicationDetailsPage id={id} />;
  },
});

const newApplicationRoute = createRoute({
  getParentRoute: () => authedRoute,
  path: "applications/new",
  validateSearch: (search: Record<string, unknown>): { clientId?: string; from?: string } => ({
    clientId: typeof search.clientId === "string" ? search.clientId : undefined,
    from: typeof search.from === "string" ? search.from : undefined,
  }),
  beforeLoad: requires("applications.write"),
  component: function NewApplication() {
    const { clientId, from } = newApplicationRoute.useSearch();
    return <NewApplicationPage key={`${clientId}-${from}`} clientId={clientId} from={from} />;
  },
});

const clientsRoute = createRoute({
  getParentRoute: () => authedRoute,
  path: "clients",
  validateSearch: (search: Record<string, unknown>): ClientFilters => {
    const page = Number(search.page);
    return {
      search: typeof search.search === "string" && search.search ? search.search : undefined,
      page: Number.isInteger(page) && page > 1 ? page : undefined,
    };
  },
  beforeLoad: requires("clients.read"),
  component: function Clients() {
    return <ClientsListPage filters={clientsRoute.useSearch()} />;
  },
});

const newClientRoute = createRoute({
  getParentRoute: () => authedRoute,
  path: "clients/new",
  beforeLoad: requires("clients.write"),
  component: NewClientPage,
});

const clientRoute = createRoute({
  getParentRoute: () => authedRoute,
  path: "clients/$id",
  beforeLoad: requires("clients.read"),
  component: function Client() {
    const { id } = clientRoute.useParams();
    return <ClientDetailsPage id={id} />;
  },
});

const routeTree = rootRoute.addChildren([
  loginRoute,
  authedRoute.addChildren([
    homeRoute,
    passwordRoute,
    noAccessRoute,
    applicationsRoute,
    applicationRoute,
    newApplicationRoute,
    clientsRoute,
    newClientRoute,
    clientRoute,
    page("dashboard", "nav.dashboard", "stats.read"),
    createRoute({ getParentRoute: () => authedRoute, path: "models", beforeLoad: requires("models.read"), component: ModelsPage }),
    page("users", "nav.users", "users.manage"),
    page("branches", "nav.branches", "branches.manage"),
    page("audit", "nav.audit", "audit.read"),
  ]),
]);

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
  });
}

export function createAppRouter(queryClient: QueryClient, history?: RouterHistory) {
  return createRouter({
    routeTree,
    history,
    basepath: "/app",
    context: { queryClient },
    defaultPendingComponent: Loading,
    defaultErrorComponent: ({ error, reset }) => <ErrorState error={error} onRetry={reset} />,
  });
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof createAppRouter>;
  }
}

export type AppRouter = ReturnType<typeof createAppRouter>;
