# 3. Backend components

Inside `backend/src/CreditScoring.Api`. Folders are layers: the UI and controllers depend on decisions and domain, never the other way round. The domain has no database or HTTP code.

```mermaid
flowchart TB
    subgraph ui["Ui + Pages (Razor Pages)"]
        pagesC["Pages/*<br/>Login, Applications, Clients,<br/>Dashboard, Model"]
        appsApi["ApplicationsApi<br/>typed HttpClient to /api"]
        fwd["CookieForwardingHandler<br/>copies the sign-in cookie"]
        helpers["Labels, FormErrors,<br/>ApplicationInputs, HistoryFilter"]
        spaHost["SpaHosting<br/>serves the React build<br/>under /app"]
        pagesC --> appsApi --> fwd
        pagesC --> helpers
    end

    subgraph controllers["Controllers (REST API)"]
        appsCtl["Applications, Drafts,<br/>Work, Search, Clients<br/>/api/applications, ..."]
        adminCtl["Auth, Users,<br/>Branches, Audit<br/>/api/auth, /api/users, ..."]
        modelsCtl["ModelsController<br/>/api/models"]
        statsCtl["StatsController<br/>/api/stats"]
    end

    subgraph contracts["Contracts (DTOs)"]
        dtos["CreateApplicationRequest,<br/>ApplicationResponse, ...<br/>BusinessLimits"]
    end

    subgraph decisions["Decisions"]
        svc["ICreditDecisionService<br/>CreditDecisionService"]
        mapper["LendingClubMapper<br/>domain to model inputs"]
        svc --> mapper
    end

    subgraph domain["Domain"]
        entities["CreditApplication, Client, Branch, User,<br/>CreditDecision, ScoreFactor, OfficerDecision,<br/>SignOff, ApplicationNote, ApplicationDraft,<br/>LoanOutcome, ScoringModel, ModelEvent,<br/>ShadowScore, AuditEntry"]
        registry["ModelRegistry<br/>promote, retire"]
    end

    subgraph ml["Ml"]
        imlclient["IMlClient"]
        mlclient["MlClient<br/>HTTP, timeouts,<br/>response checks"]
        mlclient -. implements .-> imlclient
    end

    subgraph reporting["Reporting"]
        dash["DashboardCalculator"]
    end

    subgraph data["Data"]
        dbctx["CreditScoringDbContext<br/>EF Core + migrations,<br/>branch query filter"]
        auditI["AuditInterceptor<br/>records every write"]
        dbctx --> auditI
    end

    subgraph auth["Auth"]
        policies["Permissions + Policies<br/>one policy per permission"]
        cookie["CookieSetup, Credentials,<br/>SignInService (lockout), Seeder"]
    end

    fwd -- HTTP --> controllers
    controllers --> dtos
    appsCtl --> svc
    modelsCtl --> registry
    modelsCtl --> imlclient
    statsCtl --> dash
    svc --> imlclient
    svc --> entities
    controllers --> dbctx
    dbctx --> entities
    controllers -.-> policies
```

## Who owns what

| Concern | Lives in | Not in |
|---|---|---|
| Input validation, business limits | `Contracts` (attributes, `BusinessLimits`) and controllers | Python |
| Recommendation rule (threshold, review band) | `ScoringModel.RecommendationFor`, called by `CreditDecisionService` | controllers, Python |
| When an officer must give a reason | `OfficerDecision.NeedsReason` | pages |
| Model roles (one active, shadows, retired) | `ModelRegistry` | database triggers |
| Turning inputs into a probability | Python inference service | C# |
| Who the user is | the sign-in cookie, read by `Credentials.ActorOf` | the request body |
