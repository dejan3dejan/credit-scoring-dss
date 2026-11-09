from pathlib import Path

import numpy as np
import pandas as pd
import pytest
import sklearn
from sklearn.calibration import CalibratedClassifierCV
from fastapi.testclient import TestClient

from app.main import create_app
from app.schemas import LC_CATEGORY_CODES, LC_FEATURES
from conftest import VALID_REQUEST, make_artifact
from src.models.artifact import build_metadata, load_artifact, save_artifact
from src.models.explain import top_factors
from src.models.lending_club_pipelines import logistic_regression_pipeline, xgboost_pipeline

LC_REQUEST = {
    "loan_amnt": 12000,
    "purpose": "debt_consolidation",
    "emp_length_years": 4,
    "home_ownership": "RENT",
    "annual_inc": 58000,
    "verification_status": "Source Verified",
    "dti": 18.4,
    "fico": 697,
    "credit_history_years": 11.5,
    "inq_last_6mths": 1,
    "delinq_2yrs": 0,
    "pub_rec": 0,
    "pub_rec_bankruptcies": 0,
    "revol_bal": 9400,
    "revol_util": 52.3,
    "open_acc": 9,
    "total_acc": 21,
    "mort_acc": 0,
}


def lc_training_data(n=600, seed=0, home_ownership=("RENT", "OWN", "MORTGAGE", "OTHER")):
    rng = np.random.default_rng(seed)
    df = pd.DataFrame({
        "loan_amnt": rng.integers(1000, 35001, n).astype(float),
        "purpose": rng.choice(LC_CATEGORY_CODES["purpose"], n),
        "emp_length_years": rng.choice([0.0, 3.0, 10.0, np.nan], n),
        "home_ownership": rng.choice(list(home_ownership), n),
        "annual_inc": rng.lognormal(11, 0.5, n),
        "verification_status": rng.choice(LC_CATEGORY_CODES["verification_status"], n),
        "dti": rng.uniform(0, 40, n),
        "fico": rng.uniform(660, 850, n),
        "credit_history_years": rng.uniform(1, 40, n),
        "inq_last_6mths": rng.integers(0, 6, n).astype(float),
        "delinq_2yrs": rng.integers(0, 3, n).astype(float),
        "pub_rec": rng.integers(0, 2, n).astype(float),
        "pub_rec_bankruptcies": rng.integers(0, 2, n).astype(float),
        "revol_bal": rng.lognormal(9, 1, n),
        "revol_util": rng.choice([10.0, 50.0, 95.0, np.nan], n),
        "open_acc": rng.integers(1, 30, n).astype(float),
        "total_acc": rng.integers(2, 60, n).astype(float),
        "mort_acc": rng.choice([0.0, 1.0, 3.0, np.nan], n),
    })
    y = pd.Series((rng.random(n) < 0.15).astype(int))
    return df, y


def make_lc_artifact(out_dir, model_id="test_lc_v1", columns=None, build=logistic_regression_pipeline, **data_options):
    X, y = lc_training_data(**data_options)
    X = X[columns or LC_FEATURES]
    model = build().fit(X, y)
    metadata = build_metadata(
        model_id=model_id, model_name="LogReg", calibration="none", threshold=0.3286,
        cost_fn=2.0431, cost_fp=1, seed=0, X_train=X, y_train=y, data_sha256="0" * 64,
        created_at="2026-01-01T00:00:00+00:00",
        library_versions={"scikit-learn": sklearn.__version__, "python": "test"},
        evaluation_ref="none", dataset="lending_club", review_from=0.2,
    )
    model_path, _ = save_artifact(model, metadata, out_dir)
    return model_path


def calibrated_xgboost():
    return CalibratedClassifierCV(xgboost_pipeline(), method="isotonic", cv=3)


@pytest.fixture(scope="module")
def models_dir(tmp_path_factory):
    out = tmp_path_factory.mktemp("mixed_models")
    make_artifact(out, "test_model_v1", "LogReg")
    make_lc_artifact(out, "test_lc_v1")
    make_lc_artifact(out, "test_lc_reordered_v1", columns=list(reversed(LC_FEATURES)))
    make_lc_artifact(out, "test_lc_no_other_v1", home_ownership=("RENT", "OWN", "MORTGAGE"))
    make_lc_artifact(out, "test_lc_xgb_v1", build=calibrated_xgboost)
    return out


@pytest.fixture
def v3_client(models_dir):
    app = create_app(models_dir / "test_model_v1.joblib", models_dir=models_dir)
    with TestClient(app, raise_server_exceptions=False) as c:
        yield c


def test_lists_only_lending_club_models_with_their_review_band(v3_client):
    resp = v3_client.get("/v3/models")

    assert resp.status_code == 200
    models = resp.json()["models"]
    assert [m["model_id"] for m in models] == ["test_lc_v1", "test_lc_xgb_v1"]
    assert models[0]["dataset"] == "lending_club"
    assert models[0]["recommended_threshold"]["value"] == pytest.approx(0.3286)
    assert models[0]["recommended_review_band"]["from"] == pytest.approx(0.2)
    assert models[0]["recommended_review_band"]["to"] == pytest.approx(0.3286)


def test_german_models_stay_on_v2(v3_client):
    ids = [m["model_id"] for m in v3_client.get("/v2/models").json()["models"]]
    assert ids == ["test_model_v1"]


def test_artifacts_that_break_the_lending_club_contract_are_skipped(v3_client):
    loaded = v3_client.get("/health").json()["models_loaded"]
    assert "test_lc_reordered_v1" not in loaded
    assert "test_lc_no_other_v1" not in loaded
    assert {"test_model_v1", "test_lc_v1", "test_lc_xgb_v1"} <= set(loaded)


def test_predict_scores_with_the_named_model(v3_client, models_dir):
    resp = v3_client.post("/v3/models/test_lc_v1/predict", json=LC_REQUEST)

    assert resp.status_code == 200
    model, _ = load_artifact(models_dir / "test_lc_v1.joblib")
    expected = model.predict_proba(pd.DataFrame([LC_REQUEST], columns=LC_FEATURES))[0, 1]
    assert resp.json() == {"model_id": "test_lc_v1", "p_bad": pytest.approx(expected, abs=1e-12), "top_factors": None}


def test_calibrated_xgboost_explains_its_score_with_the_top_five_inputs(v3_client, models_dir):
    body = {**LC_REQUEST, "emp_length_years": None}
    resp = v3_client.post("/v3/models/test_lc_xgb_v1/predict", json=body)

    assert resp.status_code == 200
    model, _ = load_artifact(models_dir / "test_lc_xgb_v1.joblib")
    frame = pd.DataFrame([body], columns=LC_FEATURES).astype({c: float for c in LC_FEATURES if c not in LC_CATEGORY_CODES})
    factors = resp.json()["top_factors"]
    assert len(factors) == 5
    assert [f["feature"] for f in factors] == [f["feature"] for f in top_factors(model, frame, LC_FEATURES)]
    for got, expected in zip(factors, top_factors(model, frame, LC_FEATURES)):
        assert got["contribution"] == pytest.approx(expected["contribution"], abs=1e-9)
        assert got["value"] == body[got["feature"]]


@pytest.mark.parametrize("field", ["emp_length_years", "revol_util", "mort_acc"])
def test_fields_the_model_saw_missing_may_be_null(v3_client, field):
    resp = v3_client.post("/v3/models/test_lc_v1/predict", json={**LC_REQUEST, field: None})
    assert resp.status_code == 200


@pytest.mark.parametrize("field", ["loan_amnt", "fico", "dti", "purpose", "pub_rec_bankruptcies"])
def test_other_fields_may_not_be_null(v3_client, field):
    resp = v3_client.post("/v3/models/test_lc_v1/predict", json={**LC_REQUEST, field: None})
    assert resp.status_code == 422
    assert resp.json()["error"]["details"][0]["field"] == field


def test_a_missing_field_is_rejected_even_if_it_could_be_null(v3_client):
    body = {k: v for k, v in LC_REQUEST.items() if k != "mort_acc"}
    resp = v3_client.post("/v3/models/test_lc_v1/predict", json=body)
    assert resp.status_code == 422
    assert resp.json()["error"]["details"] == [{"field": "mort_acc", "issue": "field required"}]


def test_unknown_code_lists_the_lending_club_codes(v3_client):
    resp = v3_client.post("/v3/models/test_lc_v1/predict", json={**LC_REQUEST, "purpose": "educational"})
    assert resp.status_code == 422
    issue = resp.json()["error"]["details"][0]["issue"]
    assert issue.startswith("unknown code 'educational'; allowed: car, credit_card")


@pytest.mark.parametrize("field, value, issue", [
    ("fico", 900, "must be <= 850"),
    ("emp_length_years", 11, "must be <= 10"),
    ("annual_inc", -1, "must be >= 0"),
    ("loan_amnt", 0, "must be >= 1"),
    ("open_acc", 2.5, "must be an integer"),
])
def test_values_the_model_cannot_score_are_rejected(v3_client, field, value, issue):
    resp = v3_client.post("/v3/models/test_lc_v1/predict", json={**LC_REQUEST, field: value})
    assert resp.status_code == 422
    assert resp.json()["error"]["details"] == [{"field": field, "issue": issue}]


def test_extra_fields_are_rejected(v3_client):
    resp = v3_client.post("/v3/models/test_lc_v1/predict", json={**LC_REQUEST, "int_rate": 11.5})
    assert resp.status_code == 422


def test_a_german_model_is_not_found_under_v3(v3_client):
    resp = v3_client.post("/v3/models/test_model_v1/predict", json=LC_REQUEST)
    assert resp.status_code == 404
    assert resp.json()["error"]["code"] == "model_not_found"


def test_a_lending_club_model_is_not_found_under_v2(v3_client):
    resp = v3_client.post("/v2/models/test_lc_v1/predict", json=VALID_REQUEST)
    assert resp.status_code == 404


def test_v1_still_serves_the_german_default_model(v3_client):
    assert v3_client.post("/v1/predict", json=VALID_REQUEST).status_code == 200


REAL_LC_MODEL = Path(__file__).resolve().parents[2] / "ml" / "models" / "credit_lc_xgb_isotonic_v1.joblib"


@pytest.mark.skipif(not REAL_LC_MODEL.exists(), reason="served Lending Club artifact not present")
def test_real_lending_club_artifact_scores_like_direct_predict_proba():
    model, metadata = load_artifact(REAL_LC_MODEL)
    requests = [
        LC_REQUEST,
        {**LC_REQUEST, "emp_length_years": None, "revol_util": None, "mort_acc": None},
        {**LC_REQUEST, "annual_inc": 7_500_000, "revol_util": 892.3, "home_ownership": "OTHER"},
        {**LC_REQUEST, "fico": 662, "dti": 36, "inq_last_6mths": 5, "purpose": "small_business"},
    ]
    frame = pd.DataFrame(requests, columns=LC_FEATURES).astype(
        {c: float for c in LC_FEATURES if c not in LC_CATEGORY_CODES})
    expected = model.predict_proba(frame)[:, 1]

    with TestClient(create_app(models_dir=REAL_LC_MODEL.parent)) as c:
        for body, p in zip(requests, expected):
            resp = c.post(f"/v3/models/{metadata['model_id']}/predict", json=body)
            assert resp.status_code == 200
            got = resp.json()
            assert (got["model_id"], got["p_bad"]) == (metadata["model_id"], pytest.approx(p, abs=1e-12))
            one = frame.iloc[[requests.index(body)]]
            expected = top_factors(model, one, LC_FEATURES)
            assert [f["feature"] for f in got["top_factors"]] == [f["feature"] for f in expected]
            assert [f["contribution"] for f in got["top_factors"]] == pytest.approx([f["contribution"] for f in expected], abs=1e-9)
        listed = c.get("/v3/models").json()["models"]
        assert [m["model_id"] for m in listed] == [metadata["model_id"]]


def test_a_non_finite_contribution_is_an_internal_error(v3_client, monkeypatch):
    import app.main
    monkeypatch.setattr(app.main, "top_factors",
                        lambda *a, **k: [{"feature": "fico", "value": 697.0, "contribution": float("nan")}])

    resp = v3_client.post("/v3/models/test_lc_xgb_v1/predict", json=LC_REQUEST)

    assert resp.status_code == 500
    assert resp.json()["error"]["code"] == "internal_error"


def test_factor_values_are_the_request_values_not_the_float_frame(v3_client, monkeypatch):
    import app.main
    monkeypatch.setattr(app.main, "top_factors", lambda *a, **k: [
        {"feature": "loan_amnt", "value": 12000.0, "contribution": 0.2},
        {"feature": "emp_length_years", "value": None, "contribution": -0.1},
        {"feature": "fico", "value": 697.0, "contribution": 0.1},
    ])

    resp = v3_client.post("/v3/models/test_lc_xgb_v1/predict", json={**LC_REQUEST, "emp_length_years": None})

    assert resp.json()["top_factors"] == [
        {"feature": "loan_amnt", "value": 12000, "contribution": 0.2},
        {"feature": "emp_length_years", "value": None, "contribution": -0.1},
        {"feature": "fico", "value": 697.0, "contribution": 0.1},
    ]
    assert type(resp.json()["top_factors"][0]["value"]) is int


def test_health_is_ok_with_only_a_lending_club_model(tmp_path):
    make_lc_artifact(tmp_path, "test_lc_only_v1")
    app = create_app(tmp_path / "no_default.joblib", models_dir=tmp_path)
    with TestClient(app, raise_server_exceptions=False) as c:
        resp = c.get("/health")
        scored = c.post("/v3/models/test_lc_only_v1/predict", json=LC_REQUEST)
    assert resp.status_code == 200
    assert resp.json() == {"status": "ok", "model_loaded": False, "model_id": None,
                           "models_loaded": ["test_lc_only_v1"]}
    assert scored.status_code == 200
