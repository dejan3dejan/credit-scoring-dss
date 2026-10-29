import json
import shutil

import pytest
from fastapi.testclient import TestClient

from app.main import create_app
from conftest import VALID_REQUEST

UNAVAILABLE = {"error": {"code": "model_unavailable",
                         "message": "Model is not loaded; the service cannot score"}}


def copy_artifact(artifact_path, dest_dir):
    model_path = dest_dir / artifact_path.name
    shutil.copy(artifact_path, model_path)
    shutil.copy(artifact_path.with_suffix(".json"), model_path.with_suffix(".json"))
    return model_path


def edit_metadata(model_path, edit):
    meta_path = model_path.with_suffix(".json")
    metadata = json.loads(meta_path.read_text(encoding="utf-8"))
    edit(metadata)
    meta_path.write_text(json.dumps(metadata), encoding="utf-8")


def missing_artifact(artifact_path, tmp_path):
    return tmp_path / "does_not_exist.joblib"


def other_sklearn_version(artifact_path, tmp_path):
    model_path = copy_artifact(artifact_path, tmp_path)
    edit_metadata(model_path, lambda m: m["library_versions"].update({"scikit-learn": "0.0.1"}))
    return model_path


def unreadable_model_file(artifact_path, tmp_path):
    model_path = copy_artifact(artifact_path, tmp_path)
    model_path.write_bytes(b"not a joblib file")
    return model_path


def codes_differ_from_contract(artifact_path, tmp_path):
    model_path = copy_artifact(artifact_path, tmp_path)
    edit_metadata(model_path, lambda m: m["features"]["categorical_levels_seen_in_training"]
                  ["purpose"].append("A47"))
    return model_path


@pytest.fixture(params=[missing_artifact, other_sklearn_version, unreadable_model_file,
                        codes_differ_from_contract])
def degraded_client(request, artifact_path, tmp_path):
    model_path = request.param(artifact_path, tmp_path)
    with TestClient(create_app(model_path)) as c:
        yield c


def test_predict_returns_503(degraded_client):
    resp = degraded_client.post("/v1/predict", json=VALID_REQUEST)
    assert resp.status_code == 503
    assert resp.json() == UNAVAILABLE


def test_model_info_returns_503(degraded_client):
    resp = degraded_client.get("/v1/model")
    assert resp.status_code == 503
    assert resp.json() == UNAVAILABLE


def test_health_reports_degraded(degraded_client):
    resp = degraded_client.get("/health")
    assert resp.status_code == 503
    assert resp.json() == {"status": "degraded", "model_loaded": False, "model_id": None}


def test_invalid_request_is_422_even_when_degraded(degraded_client):
    resp = degraded_client.post("/v1/predict", json={**VALID_REQUEST, "purpose": "A47"})
    assert resp.status_code == 422
