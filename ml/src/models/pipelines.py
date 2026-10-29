from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import Pipeline
from xgboost import XGBClassifier

from src.config import RANDOM_STATE
from src.features.preprocessing import build_preprocessing_pipeline


def logistic_regression_pipeline() -> Pipeline:
    return Pipeline([
        ("preprocess", build_preprocessing_pipeline()),
        ("clf", LogisticRegression(
            class_weight="balanced",
            random_state=RANDOM_STATE,
            max_iter=1000,
        )),
    ])


def random_forest_pipeline() -> Pipeline:
    return Pipeline([
        ("preprocess", build_preprocessing_pipeline()),
        ("clf", RandomForestClassifier(
            class_weight="balanced",
            random_state=RANDOM_STATE,
        )),
    ])


def xgboost_pipeline(y_train) -> Pipeline:
    neg = (y_train == 0).sum()
    pos = (y_train == 1).sum()
    scale_pos_weight = neg / pos

    return Pipeline([
        ("preprocess", build_preprocessing_pipeline()),
        ("clf", XGBClassifier(
            scale_pos_weight=scale_pos_weight,
            random_state=RANDOM_STATE,
            eval_metric="logloss",
        )),
    ])


def build_candidates(y_train) -> dict[str, Pipeline]:
    return {
        "LogReg": logistic_regression_pipeline(),
        "RandomForest": random_forest_pipeline(),
        "XGBoost": xgboost_pipeline(y_train),
    }
