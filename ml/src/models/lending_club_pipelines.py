import numpy as np
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import Pipeline
from xgboost import XGBClassifier

from src.config import RANDOM_STATE
from src.features.lending_club_preprocessing import build_lc_preprocessing, build_woe_preprocessing

RF_MIN_SAMPLES_LEAF = 100

XGB_SEARCH_SPACE = {
    "n_estimators": ("int", 100, 600),
    "learning_rate": ("log", 0.01, 0.3),
    "max_depth": ("int", 2, 8),
    "min_child_weight": ("log", 1.0, 200.0),
    "subsample": ("uniform", 0.5, 1.0),
    "colsample_bytree": ("uniform", 0.5, 1.0),
}


def scorecard_pipeline() -> Pipeline:
    return Pipeline([
        ("woe", build_woe_preprocessing()),
        ("clf", LogisticRegression(max_iter=1000, random_state=RANDOM_STATE)),
    ])


def logistic_regression_pipeline() -> Pipeline:
    return Pipeline([
        ("preprocess", build_lc_preprocessing()),
        ("clf", LogisticRegression(max_iter=1000, random_state=RANDOM_STATE)),
    ])


def random_forest_pipeline() -> Pipeline:
    return Pipeline([
        ("preprocess", build_lc_preprocessing()),
        ("clf", RandomForestClassifier(
            min_samples_leaf=RF_MIN_SAMPLES_LEAF,
            n_jobs=-1,
            random_state=RANDOM_STATE,
        )),
    ])


def xgboost_pipeline() -> Pipeline:
    return Pipeline([
        ("preprocess", build_lc_preprocessing()),
        ("clf", XGBClassifier(
            eval_metric="logloss",
            n_jobs=-1,
            random_state=RANDOM_STATE,
        )),
    ])


def sample_xgb_params(n, seed=RANDOM_STATE, space=XGB_SEARCH_SPACE):
    rng = np.random.default_rng(seed)
    draws = {
        "int": lambda low, high: int(rng.integers(low, high + 1)),
        "log": lambda low, high: float(np.exp(rng.uniform(np.log(low), np.log(high)))),
        "uniform": lambda low, high: float(rng.uniform(low, high)),
    }
    return [{name: draws[kind](low, high) for name, (kind, low, high) in space.items()} for _ in range(n)]


def build_lc_candidates() -> dict[str, Pipeline]:
    return {
        "Scorecard": scorecard_pipeline(),
        "LogReg": logistic_regression_pipeline(),
        "RandomForest": random_forest_pipeline(),
        "XGBoost": xgboost_pipeline(),
    }
