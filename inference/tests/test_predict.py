from pathlib import Path

import pandas as pd
import pytest
from fastapi.testclient import TestClient

from app.main import create_app
from app.schemas import FEATURES
from conftest import VALID_REQUEST
from src.models.artifact import load_artifact

ML_DIR = Path(__file__).resolve().parents[2] / "ml"
REAL_MODEL = ML_DIR / "models" / "credit_rf_isotonic_v1.joblib"
REAL_DATA = ML_DIR / "data" / "raw" / "german.data"


def test_valid_request_returns_model_probability(client, artifact_path):
    resp = client.post("/v1/predict", json=VALID_REQUEST)
    assert resp.status_code == 200
    body = resp.json()
    assert set(body) == {"model_id", "p_bad"}
    assert body["model_id"] == "test_model_v1"

    model, _ = load_artifact(artifact_path)
    expected = model.predict_proba(pd.DataFrame([VALID_REQUEST], columns=FEATURES))[0, 1]
    assert body["p_bad"] == pytest.approx(expected, abs=1e-12)
    assert 0.0 <= body["p_bad"] <= 1.0


def test_model_info_serves_metadata(client):
    resp = client.get("/v1/model")
    assert resp.status_code == 200
    body = resp.json()
    assert body["model_id"] == "test_model_v1"
    assert body["output"] == "P(bad), probability of default, class 1"
    assert body["recommended_threshold"] == {
        "value": pytest.approx(1 / 6),
        "rule": "reject iff P(bad) >= value",
        "derivation": "cost_fp / (cost_fp + cost_fn), cost_fn=5, cost_fp=1",
    }
    assert body["features"] == FEATURES
    assert body["library_versions"]["scikit-learn"]


def test_health_ok(client):
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.json() == {"status": "ok", "model_loaded": True, "model_id": "test_model_v1",
                           "models_loaded": ["test_model_v1"]}


@pytest.mark.skipif(not (REAL_MODEL.exists() and REAL_DATA.exists()),
                    reason="served artifact or german.data not present (both are gitignored)")
def test_real_artifact_scores_like_direct_predict_proba():
    from src.data.load import load_raw

    rows = load_raw(REAL_DATA).drop(columns=["raw_class"]).head(5)
    model, metadata = load_artifact(REAL_MODEL)
    expected = model.predict_proba(rows)[:, 1]

    with TestClient(create_app(REAL_MODEL)) as c:
        for record, p in zip(rows.to_dict(orient="records"), expected):
            payload = {k: (int(v) if not isinstance(v, str) else v) for k, v in record.items()}
            resp = c.post("/v1/predict", json=payload)
            assert resp.status_code == 200
            assert resp.json() == {"model_id": metadata["model_id"], "p_bad": pytest.approx(p, abs=1e-12)}
