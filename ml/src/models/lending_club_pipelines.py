from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import Pipeline
from xgboost import XGBClassifier

from src.config import RANDOM_STATE
from src.features.lending_club_preprocessing import build_lc_preprocessing, build_woe_preprocessing

RF_MIN_SAMPLES_LEAF = 100


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


def build_lc_candidates() -> dict[str, Pipeline]:
    return {
        "Scorecard": scorecard_pipeline(),
        "LogReg": logistic_regression_pipeline(),
        "RandomForest": random_forest_pipeline(),
        "XGBoost": xgboost_pipeline(),
    }
