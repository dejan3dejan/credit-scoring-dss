# 12. Status and roadmap

Where the system is on 2026-11-09 and what is planned next. Solid boxes are built and tested; dashed ones are planned.

```mermaid
flowchart LR
    subgraph later["If time allows"]
        direction TB
        l1["Decision report and CSV export"]
        l2["Model monitoring over time"]
        l3["Four-eyes sign-off"]
        l4["User management page"]
        l5["Browser tests"]
    end

    subgraph next["Planned, in order"]
        direction TB
        n3["Loan outcome tracking:<br/>repaid or defaulted"]
        n4["Security basics:<br/>lockout, headers, HTTPS"]
    end

    subgraph done["Built"]
        direction TB
        d1["Application form, validation,<br/>scoring, recommendation"]
        d2["Officer decision with<br/>reason rules"]
        d3["Searchable history"]
        d4["Sign-in, three roles"]
        d5["Clients and their<br/>application timeline"]
        d6["Model registry,<br/>champion and challenger"]
        d7["Dashboard"]
        d8["Review band and<br/>top 5 factors, checklist"]
        d9["ML: Lending Club model,<br/>LC-01 to LC-11"]
        d10["One-command start<br/>(Docker Compose) + README"]
        d11["CI: tests on every push"]
    end

    classDef plan stroke-dasharray: 5 5
    class n3,n4,l1,l2,l3,l4,l5 plan
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
