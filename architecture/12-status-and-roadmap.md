# 12. Status and roadmap

Where the system is on 2026-11-10 and what is planned next. Solid boxes are built and tested; dashed ones are planned.

```mermaid
flowchart LR
    subgraph later["If time allows"]
        direction TB
        l1["Decision report and CSV export"]
        l2["Model monitoring over time"]
        l3["Security headers, HTTPS"]
    end

    subgraph next["Planned, in order"]
        direction TB
        n1["React pages for drafts, notes,<br/>withdrawal, assignment, outcomes"]
        n2["React admin pages:<br/>users, branches, audit log"]
        n3["Browser tests on React<br/>(QA session), then remove<br/>the Razor pages"]
    end

    subgraph done["Built"]
        direction TB
        d1["Application form, validation,<br/>scoring, recommendation"]
        d2["Officer decision with<br/>reason rules"]
        d3["Searchable history"]
        d4["Sign-in with lockout,<br/>four roles, permissions"]
        d5["Clients and their<br/>application timeline"]
        d6["Model registry,<br/>champion and challenger"]
        d7["Dashboard"]
        d8["Review band and<br/>top 5 factors, checklist"]
        d9["ML: Lending Club model,<br/>LC-01 to LC-11"]
        d10["One-command start<br/>(Docker Compose) + README"]
        d11["CI: tests on every push"]
        d12["React app, English and Serbian:<br/>applications, clients, home queue,<br/>models, dashboard"]
        d13["API: branches, audit log, drafts,<br/>sign-off, notes, loan outcomes"]
    end

    classDef plan stroke-dasharray: 5 5
    class n1,n2,n3,l1,l2,l3 plan
```

## What changes when outcome tracking lands

The archive becomes a feedback loop, which is the supervisor's original brief: every stored decision can later be compared with what the borrower actually did.

```mermaid
flowchart LR
    a["Application"] --> s["Score and<br/>recommendation"]
    s --> o["Officer decision"]
    o --> h[("History")]
    h -.-> out["Outcome recorded later:<br/>repaid or defaulted"]
    out -.-> m["Measured: default rate of approved,<br/>overridden and reviewed loans;<br/>reviewer skill vs the 1.27x break-even"]
    m -.-> r["Evidence for recalibration<br/>or a new model"]
    r -.-> s

    classDef plan stroke-dasharray: 5 5
    class out,m,r plan
```

## Test counts (last run)

| Part | Tests |
|---|---|
| Backend (xUnit, with PostgreSQL) | 454 |
| ML (pytest) | 113 |
| Inference (pytest) | 79 |
