import pandas as pd
import pytest
from fastapi.testclient import TestClient

from app.main import create_app
from app.schemas import FEATURES
from conftest import VALID_REQUEST, make_artifact
from src.models.artifact import load_artifact


@pytest.fixture(scope="module")
def models_dir(tmp_path_factory):
    out = tmp_path_factory.mktemp("two_models")
    make_artifact(out, "test_model_v1", "LogReg")
    make_artifact(out, "test_challenger_v1", "LogRegStrong", C=0.01)
    return out


@pytest.fixture
def v2_client(models_dir):
    app = create_app(models_dir / "test_model_v1.joblib", models_dir=models_dir)
    with TestClient(app, raise_server_exceptions=False) as c:
        yield c


def test_lists_every_loaded_model_sorted(v2_client):
    resp = v2_client.get("/v2/models")

    assert resp.status_code == 200
    models = resp.json()["models"]
    assert [m["model_id"] for m in models] == ["test_challenger_v1", "test_model_v1"]
    assert models[0]["model_name"] == "LogRegStrong"
    assert models[0]["dataset"] == "statlog_german_credit"
    assert models[0]["recommended_threshold"]["value"] == pytest.approx(1 / 6)
    assert models[0]["created_at"] == "2026-01-01T00:00:00+00:00"
    assert models[0]["library_versions"]["scikit-learn"]


@pytest.mark.parametrize("model_id", ["test_model_v1", "test_challenger_v1"])
def test_predict_scores_with_the_named_model(v2_client, models_dir, model_id):
    resp = v2_client.post(f"/v2/models/{model_id}/predict", json=VALID_REQUEST)

    assert resp.status_code == 200
    model, _ = load_artifact(models_dir / f"{model_id}.joblib")
    expected = model.predict_proba(pd.DataFrame([VALID_REQUEST], columns=FEATURES))[0, 1]
    assert resp.json() == {"model_id": model_id, "p_bad": pytest.approx(expected, abs=1e-12)}


def test_the_two_models_give_different_scores(v2_client):
    a = v2_client.post("/v2/models/test_model_v1/predict", json=VALID_REQUEST).json()["p_bad"]
    b = v2_client.post("/v2/models/test_challenger_v1/predict", json=VALID_REQUEST).json()["p_bad"]

    assert a != b


def test_unknown_model_is_not_found(v2_client):
    resp = v2_client.post("/v2/models/no_such_model/predict", json=VALID_REQUEST)

    assert resp.status_code == 404
    assert resp.json() == {"error": {"code": "model_not_found",
                                     "message": "No loaded model has id 'no_such_model'"}}


def test_v2_validates_like_v1(v2_client):
    resp = v2_client.post("/v2/models/test_model_v1/predict", json={**VALID_REQUEST, "purpose": "A47"})

    assert resp.status_code == 422
    assert resp.json()["error"]["code"] == "validation_error"


def test_broken_artifact_is_skipped_and_the_rest_still_load(tmp_path):
    make_artifact(tmp_path, "test_model_v1")
    broken = make_artifact(tmp_path, "test_broken_v1")
    broken.write_bytes(b"not a joblib file")

    with TestClient(create_app(tmp_path / "test_model_v1.joblib", models_dir=tmp_path)) as c:
        assert [m["model_id"] for m in c.get("/v2/models").json()["models"]] == ["test_model_v1"]
        assert c.get("/health").json()["models_loaded"] == ["test_model_v1"]
        assert c.post("/v2/models/test_broken_v1/predict", json=VALID_REQUEST).status_code == 404


def test_v1_still_serves_the_default_model(v2_client):
    resp = v2_client.post("/v1/predict", json=VALID_REQUEST)

    assert resp.status_code == 200
    assert resp.json()["model_id"] == "test_model_v1"
