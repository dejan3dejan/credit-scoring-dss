# 3. Backend components

Inside `backend/src/CreditScoring.Api`. Folders are layers: the UI and controllers depend on decisions and domain, never the other way round. The domain has no database or HTTP code.

```mermaid
flowchart TB
    subgraph ui["Ui + Pages (Razor Pages)"]
        pagesC["Pages/*<br/>Login, Applications, Clients,<br/>Dashboard, Model"]
        appsApi["ApplicationsApi<br/>typed HttpClient to /api"]
        fwd["CookieForwardingHandler<br/>copies the sign-in cookie"]
        helpers["Labels, FormErrors,<br/>ApplicationInputs, HistoryFilter"]
        pagesC --> appsApi --> fwd
        pagesC --> helpers
    end

    subgraph controllers["Controllers (REST API)"]
        appsCtl["ApplicationsController<br/>/api/applications"]
        clientsCtl["ClientsController<br/>/api/clients"]
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
        entities["CreditApplication, Client, User,<br/>CreditDecision, ScoreFactor,<br/>OfficerDecision, ScoringModel,<br/>ModelEvent, ShadowScore"]
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
        dbctx["CreditScoringDbContext<br/>EF Core + migrations"]
    end

    subgraph auth["Auth"]
        policies["Policies<br/>CanDecide, CanSeeStats,<br/>CanManageModels"]
        cookie["CookieSetup, Credentials,<br/>UserSeeder"]
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
