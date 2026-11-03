# Architecture

Diagrams of the Credit Scoring Decision Support System, drawn from the code as it stands. Each file is Mermaid, which GitHub renders directly; exported SVG and PNG copies for the thesis are in [`img/`](img/).

The system is a web application for loan officers. A machine learning model estimates the probability that a loan goes bad and recommends approve, review closely, or reject. A person always makes the final decision, and every application, score and decision is stored.

| # | File | What it shows |
|---|---|---|
| 1 | [System context](01-system-context.md) | Who uses the system and what it depends on |
| 2 | [Containers and deployment](02-containers.md) | The running parts, how they talk, and where they run |
| 3 | [Backend components](03-backend-components.md) | Inside the ASP.NET Core application |
| 4 | [Black boxes](04-black-boxes.md) | Each part as inputs and outputs only, with its endpoints |
| 5 | [Domain classes](05-domain-classes.md) | The C# domain model |
| 6 | [Database](06-database.md) | PostgreSQL tables and keys |
| 7 | [Flows](07-flows.md) | Sign-in, scoring, officer decision, outage and rescore, model promotion, dashboard |
| 8 | [Decision logic and states](08-decision-logic.md) | The recommendation rule, the reason rule, and the life cycles |
| 9 | [ML pipeline](09-ml-pipeline.md) | From raw loan data to the served model, and inside one prediction |
| 10 | [Data flow](10-data-flow.md) | How one application's data moves end to end |
| 11 | [Roles and access](11-roles-and-access.md) | What each role may do |
| 12 | [Status and roadmap](12-status-and-roadmap.md) | What is built and what is planned |

## Conventions

- Solid boxes and arrows are built and tested. Dashed ones are planned and not built yet.
- Names in diagrams are the names in the code, so a reader can search for them.
