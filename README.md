# Credit Scoring Decision Support System

A web application for loan officers. A machine learning model estimates the probability that a loan goes bad and recommends approve, review closely, or reject; the officer makes the final decision, and every application, score and decision is stored in a searchable history.

Undergraduate thesis project, Faculty of Technical Sciences, University of Novi Sad.

## Parts

| Folder | What it is |
|---|---|
| `frontend/` | React app (Vite, TypeScript), in English and Serbian, served by the backend under `/app` |
| `backend/` | ASP.NET Core 9: REST API, business rules, PostgreSQL through EF Core; serves the React app and sends every non-API path to it |
| `inference/` | FastAPI service that turns 18 application inputs into P(bad) and its top factors. No business rules |
| `ml/` | Offline data work, experiments and model training (Python, scikit-learn, XGBoost) |
| `architecture/` | Diagrams of the whole system: context, components, classes, database, flows, ML pipeline |

The model is trained on public Lending Club loan data (36-month loans issued 2012 to 2014, tested once on 2015). See [architecture/](architecture/README.md) for how the parts fit together.

## Run it

Needs Docker Desktop.

```bash
docker compose up --build
```

Then open http://localhost:8080/app (http://localhost:8080 redirects there). The first start builds two images and takes a few minutes; later starts take seconds.

- `db`: PostgreSQL 17, also reachable on port 5433
- `inference`: http://localhost:8000 (`/health`, `/v3/models`)
- `web`: http://localhost:8080, Swagger UI at `/swagger`

The database is created and migrated on start. Five demo users are seeded: `admin`, `officer` and `manager` (head office), `officer.bg` (Beograd centar branch) and `risk`. Their passwords are in `backend/src/CreditScoring.Api/appsettings.Development.json`. These are development settings only; the compose file is a local demo, not a production setup.

| Role | Can |
|---|---|
| Officer | create clients and applications, save drafts, record decisions; sees only their own branch |
| Manager | everything the officer can in their branch, plus assigning work, signing off risky approvals and the dashboard |
| Risk | read all applications, record and correct loan outcomes, the dashboard, the model registry and the audit log |
| Admin | users, branches and the audit log; can look up and anonymise a client, but cannot read client files or applications |

The served model `ml/models/credit_lc_xgb_isotonic_v1.joblib` is committed, so nothing has to be trained to run the app.

### Demo data

To fill an empty database with 40 real 2015 LendingClub loans, run this with the stack up (Python 3, standard library only):

```bash
python demo/seed_demo.py
```

The cases in `demo/cases.json` are a random draw from the 2015 test year, so the recommendations match real data: 34 Approve, 5 Review and 1 Reject. The test year as a whole is 84.0% Approve, 15.9% Review and 0.04% Reject; the one Reject was added on purpose so the demo can show it. Names and birth dates are invented. The officer decides 30 cases, the manager signs off the one that needs it, and the risk user records each approved loan's real LendingClub outcome. 10 cases stay open for a live demo. The script checks that every score matches the one in the file and refuses a database it has already seeded. `ml/scripts/make_demo_cases.py` rebuilds the file from the data.

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

The app is then at http://localhost:5276. For the React app with live reload, run the Vite dev server next to it; it forwards `/api` to port 5276:

```bash
cd frontend && npm install && npm run dev
```

and open http://localhost:5173/app. `npm run build` writes the production build into `backend/src/CreditScoring.Api/wwwroot/app`, where the backend serves it.

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

```bash
cd frontend && npm run lint && npm test && npm run build
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
