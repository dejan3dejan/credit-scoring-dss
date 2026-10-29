# credit-scoring-inference

FastAPI service that scores one applicant and returns P(bad). It implements
`docs/api-contract.md` (v1) and nothing else: no decision, no business rules.

It loads the model with `load_artifact` from `../ml/src`, so `../ml` must be
on `PYTHONPATH` (pytest sets this through `pytest.ini`).

## Setup

```bash
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
```

The model artifact is produced by `ml/scripts/train_final_model.py` and is
not committed. Default path: `../ml/models/credit_rf_isotonic_v1.joblib`;
override with `MODEL_PATH`.

## Run

```powershell
$env:PYTHONPATH = "..\ml"
uvicorn app.main:app --port 8000
```

If the artifact is missing, unreadable, saved under another scikit-learn
version, or its code lists differ from the contract, the service still
starts: `/health` reports `degraded` and `/v1/predict` returns `503
model_unavailable`. The cause is in the server log.

## Test

```bash
python -m pytest -q
```

Tests build their own small artifact, so they do not need `ml/models/`. One
parity test scores real rows with the served artifact and is skipped when it
or `ml/data/raw/german.data` is missing.
