# 2. Containers and deployment

## Containers

Three running parts. The ASP.NET Core application owns all business rules and data; the Python service only turns inputs into a probability and its top factors.

```mermaid
flowchart TB
    browser["Browser<br/>Razor Pages HTML, no SPA"]

    subgraph web["ASP.NET Core 9 (C#)"]
        pages["Razor Pages UI<br/>/applications, /clients,<br/>/dashboard, /model"]
        api["REST API /api/*<br/>JSON, OpenAPI"]
        pages -- "HTTP + forwarded<br/>sign-in cookie<br/>(ApplicationsApi)" --> api
    end

    db[("PostgreSQL 17<br/>EF Core, snake_case tables")]

    subgraph inference["FastAPI (Python)"]
        fastapi["/v3/models<br/>/v3/models/{id}/predict<br/>/health"]
        artifacts[("Model artifacts<br/>ml/models/*.joblib + .json")]
        fastapi -- "loads at start" --> artifacts
    end

    browser -- "HTTP(S), cookie" --> pages
    browser -. "optional: Swagger UI<br/>in Development" .-> api
    api -- "EF Core" --> db
    pages -- "sign-in only:<br/>user lookup" --> db
    api -- "HTTP JSON<br/>(IMlClient, 5 s timeout)" --> fastapi
```

Why the pages call the API over HTTP instead of calling services directly: there is one way in to the business rules, so the API and the UI cannot drift apart, and every rule is tested once.

## Deployment today

Everything runs on one developer machine. Only PostgreSQL runs in Docker.

```mermaid
flowchart LR
    subgraph machine["Developer machine"]
        subgraph docker["Docker"]
            pg[("postgres:17<br/>port 5433")]
        end
        dotnet["dotnet run<br/>CreditScoring.Api<br/>port 5276"]
        uvicorn["uvicorn app.main:app<br/>port 8000"]
        files[("ml/models/")]
    end
    dotnet --> pg
    dotnet --> uvicorn
    uvicorn --> files
```

## Deployment planned

One `docker compose up` starts all three, with health checks so the web app waits for its dependencies. Not built yet.

```mermaid
flowchart LR
    subgraph compose["docker compose, planned"]
        web2["web<br/>ASP.NET Core<br/>port 8080"]
        inf2["inference<br/>FastAPI<br/>port 8000"]
        db2[("db<br/>postgres:17")]
        models2[("ml/models<br/>mounted read-only")]
    end
    web2 -.-> db2
    web2 -.-> inf2
    inf2 -.-> models2
```
