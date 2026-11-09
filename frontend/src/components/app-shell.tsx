import { Link, Outlet, useNavigate } from "@tanstack/react-router";
import { useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import {
  BarChart3,
  Boxes,
  Building2,
  ChevronsUpDown,
  FileText,
  Home,
  KeyRound,
  LogOut,
  ScrollText,
  UserCog,
  Users,
  type LucideIcon,
} from "lucide-react";
import { toast } from "sonner";
import { logout } from "@/api/auth";
import type { MeResponse, Permission } from "@/api/types";
import { homeTitleKey } from "@/auth/home";
import { can, useMe } from "@/auth/session";
import { BrandMark } from "@/components/brand-mark";
import { LanguageSwitch } from "@/components/language-switch";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { errorMessage } from "@/i18n";

type NavItem = {
  to: "/" | "/applications" | "/clients" | "/dashboard" | "/models" | "/users" | "/branches" | "/audit";
  label: string;
  icon: LucideIcon;
  permission?: Permission;
};

const items: NavItem[] = [
  { to: "/applications", label: "nav.applications", icon: FileText, permission: "applications.read" },
  { to: "/clients", label: "nav.clients", icon: Users, permission: "clients.read" },
  { to: "/dashboard", label: "nav.dashboard", icon: BarChart3, permission: "stats.read" },
  { to: "/models", label: "nav.models", icon: Boxes, permission: "models.read" },
  { to: "/users", label: "nav.users", icon: UserCog, permission: "users.manage" },
  { to: "/branches", label: "nav.branches", icon: Building2, permission: "branches.manage" },
  { to: "/audit", label: "nav.audit", icon: ScrollText, permission: "audit.read" },
];

function navFor(me: MeResponse): NavItem[] {
  const home: NavItem = { to: "/", label: homeTitleKey[me.role], icon: Home };
  return [home, ...items.filter((item) => !item.permission || can(me, item.permission))];
}

export function AppShell() {
  const { t } = useTranslation();
  const me = useMe();

  return (
    <div className="flex min-h-screen">
      <aside className="sticky top-0 flex h-screen w-58 shrink-0 flex-col border-r bg-sidebar px-3 py-5">
        <div className="flex items-center gap-2.5 px-2 pb-5">
          <BrandMark />
          <span className="text-base font-semibold">{t("app.name")}</span>
        </div>
        <nav aria-label={t("nav.main")} className="flex flex-col gap-0.5">
          {navFor(me).map((item) => (
            <Link
              key={item.to}
              to={item.to}
              activeOptions={{ exact: item.to === "/" }}
              className="flex items-center gap-2.5 rounded px-2.5 py-2 text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:outline-2 focus-visible:outline-ring"
              activeProps={{ className: "bg-accent font-semibold text-accent-foreground hover:bg-accent hover:text-accent-foreground" }}
            >
              <item.icon className="size-4" aria-hidden="true" />
              {t(item.label)}
            </Link>
          ))}
        </nav>
        <div className="mt-auto border-t pt-3">
          <UserMenu me={me} />
        </div>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-10 flex h-14 shrink-0 items-center gap-4 border-b bg-card px-6">
          <div className="ml-auto">
            <LanguageSwitch saveOnAccount />
          </div>
        </header>
        <main className="mx-auto flex w-full max-w-[1400px] flex-col gap-5 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function UserMenu({ me }: { me: MeResponse }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const subtitle = [t(`roles.${me.role}`), me.branch?.name].filter(Boolean).join(" · ");

  async function signOut() {
    try {
      await logout();
    } catch (error) {
      toast.error(errorMessage(error));
      return;
    }
    queryClient.clear();
    await navigate({ to: "/login", search: {} });
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="flex w-full items-center gap-2 rounded px-2.5 py-2 text-left hover:bg-muted focus-visible:outline-2 focus-visible:outline-ring">
        <span className="min-w-0 flex-1">
          <span className="block truncate font-medium">{me.displayName}</span>
          <span className="block truncate text-xs text-muted-foreground">{subtitle}</span>
        </span>
        <ChevronsUpDown className="size-4 text-muted-foreground" aria-hidden="true" />
      </DropdownMenuTrigger>
      <DropdownMenuContent side="top" align="start" className="w-(--radix-dropdown-menu-trigger-width)">
        <DropdownMenuLabel className="font-normal text-muted-foreground">{me.userName}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => void navigate({ to: "/account/password" })}>
          <KeyRound aria-hidden="true" />
          {t("user.changePassword")}
        </DropdownMenuItem>
        <DropdownMenuItem onSelect={() => void signOut()}>
          <LogOut aria-hidden="true" />
          {t("user.logout")}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
