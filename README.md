# Credit Scoring Decision Support System

A web application for loan officers. A machine learning model estimates the probability that a loan goes bad and recommends approve, review closely, or reject; the officer makes the final decision, and every application, score and decision is stored in a searchable history.

Undergraduate thesis project, Faculty of Technical Sciences, University of Novi Sad.

## Parts

| Folder | What it is |
|---|---|
| `backend/` | ASP.NET Core 9: Razor Pages UI, REST API, business rules, PostgreSQL through EF Core |
| `inference/` | FastAPI service that turns 18 application inputs into P(bad) and its top factors. No business rules |
| `ml/` | Offline data work, experiments and model training (Python, scikit-learn, XGBoost) |
| `architecture/` | Diagrams of the whole system: context, components, classes, database, flows, ML pipeline |

The model is trained on public Lending Club loan data (36-month loans issued 2012 to 2014, tested once on 2015). See [architecture/](architecture/README.md) for how the parts fit together.

## Run it

Needs Docker Desktop.

```bash
docker compose up --build
```

Then open http://localhost:8080. The first start builds two images and takes a few minutes; later starts take seconds.

- `db`: PostgreSQL 17, also reachable on port 5433
- `inference`: http://localhost:8000 (`/health`, `/v3/models`)
- `web`: http://localhost:8080, Swagger UI at `/swagger`

The database is created and migrated on start. Three demo users are seeded, one per role: `officer`, `manager` and `risk`. Their passwords are in `backend/src/CreditScoring.Api/appsettings.Development.json`. These are development settings only; the compose file is a local demo, not a production setup.

| Role | Can |
|---|---|
| Officer | create clients and applications, record decisions, search history |
| Manager | everything the officer can, plus the dashboard |
| Risk | history, dashboard, and the model registry (register, promote, retire) |

The served model `ml/models/credit_lc_xgb_isotonic_v1.joblib` is committed, so nothing has to be trained to run the app.

## Run without Docker

Start only the database with `docker compose up db`, then:

```bat
cd inference
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
set PYTHONPATH=..\ml
uvicorn app.main:app --port 8000
```

```bash
dotnet run --project backend/src/CreditScoring.Api --launch-profile http
```

The app is then at http://localhost:5276.

## Tests

With the database running (`docker compose up db`):

```bash
dotnet test backend
```

```bash
cd ml && python -m pytest -q
```

```bash
cd inference && python -m pytest -q
```

## Training the model again

The raw data is not committed. Download "All Lending Club loan data" from Kaggle (`wordsforthewise/lending-club`, a Kaggle account is needed) and put `accepted_2007_to_2018Q4.csv` in `ml/data/raw/lending_club/`. Then, from `ml/`:

```bash
python scripts/profile_lending_club.py
```

```bash
python scripts/train_lc_model.py
```

Seeds are fixed (42) and preprocessing is saved inside the model, so the service applies exactly the transforms learned in training.

## Limitations

- The data is US peer-to-peer lending from 2012 to 2015, and every loan in it was already approved and priced by LendingClub. The model ranks risk among approved borrowers; it has never seen a rejected applicant's outcome.
- Probabilities drift between years (2015 defaulted more often than predicted), so a deployed system would need regular recalibration.
- Amounts are in US dollars.
