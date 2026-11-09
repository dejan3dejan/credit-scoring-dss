import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/app";
import { createAppRouter, createQueryClient } from "@/router";
import "@/styles.css";

const queryClient = createQueryClient();
const router = createAppRouter(queryClient);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders queryClient={queryClient} router={router} />
  </StrictMode>,
);
