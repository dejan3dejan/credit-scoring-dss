import numpy as np
import pandas as pd
import pytest
import sklearn
from fastapi.testclient import TestClient
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import Pipeline

from app.main import create_app
from app.schemas import CATEGORY_CODES, FEATURES
from src.features.preprocessing import build_preprocessing_pipeline
from src.models.artifact import build_metadata, save_artifact

VALID_REQUEST = {
    "checking_account_status": "A12",
    "duration_months": 24,
    "credit_history": "A32",
    "purpose": "A43",
    "credit_amount": 3500,
    "savings_account": "A61",
    "employment_duration": "A73",
    "installment_rate": 3,
    "personal_status_sex": "A93",
    "other_debtors_guarantors": "A101",
    "present_residence_since": 2,
    "property": "A123",
    "age": 35,
    "other_installment_plans": "A143",
    "housing": "A152",
    "existing_credits_count": 1,
    "job": "A173",
    "num_people_liable": 1,
    "telephone": "A191",
    "foreign_worker": "A201",
}


def synthetic_training_data(n=400, seed=0):
    rng = np.random.default_rng(seed)
    df = pd.DataFrame({col: rng.choice(codes, size=n) for col, codes in CATEGORY_CODES.items()})
    df["duration_months"] = rng.integers(4, 61, size=n)
    df["credit_amount"] = rng.integers(250, 18425, size=n)
    df["installment_rate"] = rng.integers(1, 5, size=n)
    df["present_residence_since"] = rng.integers(1, 5, size=n)
    df["age"] = rng.integers(19, 76, size=n)
    df["existing_credits_count"] = rng.integers(1, 5, size=n)
    df["num_people_liable"] = rng.integers(1, 3, size=n)
    y = pd.Series((rng.random(n) < 0.3).astype(int))
    return df[FEATURES], y


def make_artifact(out_dir, model_id="test_model_v1", model_name="LogReg", C=1.0):
    X, y = synthetic_training_data()
    model = Pipeline([
        ("preprocess", build_preprocessing_pipeline()),
        ("clf", LogisticRegression(C=C, max_iter=1000)),
    ]).fit(X, y)
    metadata = build_metadata(
        model_id=model_id, model_name=model_name, calibration="none", threshold=1 / 6,
        cost_fn=5, cost_fp=1, seed=0, X_train=X, y_train=y, data_sha256="0" * 64,
        created_at="2026-01-01T00:00:00+00:00",
        library_versions={"scikit-learn": sklearn.__version__, "python": "test"},
        evaluation_ref="none",
    )
    model_path, _ = save_artifact(model, metadata, out_dir)
    return model_path


@pytest.fixture(scope="session")
def artifact_path(tmp_path_factory):
    return make_artifact(tmp_path_factory.mktemp("models"))


@pytest.fixture
def client(artifact_path):
    with TestClient(create_app(artifact_path), raise_server_exceptions=False) as c:
        yield c
