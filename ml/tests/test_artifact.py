import json

import numpy as np
import pandas as pd
import pytest
import sklearn
from sklearn.linear_model import LogisticRegression

from src.models.artifact import build_metadata, load_artifact, save_artifact


@pytest.fixture
def fitted():
    X = pd.DataFrame({"amount": [1.0, 2.0, 3.0, 4.0, 5.0, 6.0], "kind": list("ababab")})
    y = pd.Series([0, 0, 1, 0, 1, 1])
    model = LogisticRegression().fit(X[["amount"]], y)
    meta = build_metadata(
        model_id="test_model", model_name="LogReg", calibration="none", threshold=1 / 6,
        cost_fn=5, cost_fp=1, seed=42, X_train=X, y_train=y, data_sha256="0" * 64,
        created_at="2026-01-01T00:00:00+00:00",
        library_versions={"scikit-learn": sklearn.__version__},
        evaluation_ref="none",
    )
    return model, meta, X


def test_round_trip_preserves_predictions_and_metadata(fitted, tmp_path):
    model, meta, X = fitted
    model_path, _ = save_artifact(model, meta, tmp_path)
    loaded, loaded_meta = load_artifact(model_path)
    assert np.allclose(loaded.predict_proba(X[["amount"]]), model.predict_proba(X[["amount"]]))
    assert loaded_meta == meta
    assert loaded_meta["features"]["categorical_levels_seen_in_training"] == {"kind": ["a", "b"]}
    assert loaded_meta["recommended_threshold"]["value"] == pytest.approx(1 / 6)


def test_load_refuses_other_sklearn_version(fitted, tmp_path):
    model, meta, _ = fitted
    model_path, meta_path = save_artifact(model, meta, tmp_path)
    tampered = json.loads(meta_path.read_text())
    tampered["library_versions"]["scikit-learn"] = "0.0.1"
    meta_path.write_text(json.dumps(tampered))
    with pytest.raises(RuntimeError, match="refusing to load"):
        load_artifact(model_path)


def lc_metadata(**overrides):
    X = pd.DataFrame({"amount": [1.0, 2.0], "kind": ["a", "b"]})
    return build_metadata(
        model_id="lc_model", model_name="XGBoost", calibration="isotonic", threshold=0.3286,
        cost_fn=2.0431, cost_fp=1, seed=42, X_train=X, y_train=pd.Series([0, 1]), data_sha256="0" * 64,
        created_at="2026-01-01T00:00:00+00:00", library_versions={"scikit-learn": sklearn.__version__},
        evaluation_ref="none", **overrides,
    )


def test_metadata_names_its_dataset_with_german_credit_as_the_default(fitted):
    _, meta, _ = fitted
    assert meta["dataset"] == "statlog_german_credit"
    assert lc_metadata(dataset="lending_club")["dataset"] == "lending_club"


def test_metadata_carries_a_review_band_only_when_given(fitted):
    _, meta, _ = fitted
    assert "recommended_review_band" not in meta
    band = lc_metadata(review_from=0.2)["recommended_review_band"]
    assert band["from"] == 0.2 and band["to"] == pytest.approx(0.3286)


def test_review_band_must_start_below_the_threshold():
    with pytest.raises(ValueError, match="below the threshold"):
        lc_metadata(review_from=0.4)
