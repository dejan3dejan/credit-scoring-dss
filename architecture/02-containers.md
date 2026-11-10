# 2. Containers and deployment

## Containers

Three running parts. The ASP.NET Core application owns all business rules and data and also serves the React app; the Python service only turns inputs into a probability and its top factors.

```mermaid
flowchart TB
    browser["Browser"]

    subgraph web["ASP.NET Core 9 (C#)"]
        spa["React app under /app<br/>static files from the Vite build,<br/>English and Serbian"]
        pages["Razor Pages UI<br/>until the React pages replace it"]
        api["REST API /api/*<br/>JSON, OpenAPI"]
        pages -- "HTTP + forwarded<br/>sign-in cookie<br/>(ApplicationsApi)" --> api
    end

    db[("PostgreSQL 17<br/>EF Core, snake_case tables")]

    subgraph inference["FastAPI (Python)"]
        fastapi["/v3/models<br/>/v3/models/{id}/predict<br/>/health"]
        artifacts[("Model artifacts<br/>ml/models/*.joblib + .json")]
        fastapi -- "loads at start" --> artifacts
    end

    browser -- "loads page and scripts" --> spa
    browser -- "fetch /api/*, same origin,<br/>sign-in cookie" --> api
    browser -- "HTTP(S), cookie" --> pages
    browser -. "optional: Swagger UI<br/>in Development" .-> api
    api -- "EF Core" --> db
    pages -- "sign-in only:<br/>user lookup" --> db
    api -- "HTTP JSON<br/>(IMlClient, 5 s timeout)" --> fastapi
```

Why both UIs go through the API instead of calling services directly: there is one way in to the business rules, so the API and the UI cannot drift apart, and every rule is tested once. The React app runs in the browser and calls the API on the same origin, so the `HttpOnly`, `SameSite=Strict` cookie works without CORS and the SPA never sees a token.

## Deployment

`docker compose up --build` in the repository root starts all three on one machine. The web image builds the React app in a Node stage and copies it into `wwwroot/app`. Health checks make the web app wait until the database accepts connections and the inference service has a model loaded. In Development the web app migrates the database and seeds the demo users on start, so a fresh clone runs without manual steps. Running on a server is out of scope (supervisor, 2026-11-06).

```mermaid
flowchart LR
    subgraph compose["docker compose"]
        web2["web<br/>ASP.NET Core + React build<br/>port 8080"]
        inf2["inference<br/>FastAPI<br/>port 8000"]
        db2[("db<br/>postgres:17<br/>port 5433")]
        models2[("ml/models<br/>mounted read-only")]
    end
    web2 -- "waits for healthy" --> db2
    web2 -- "waits for healthy" --> inf2
    inf2 --> models2
```

For development the two services can also run directly (`dotnet run`, `uvicorn`) against the Compose database; see the root README.
