import numpy as np
import pandas as pd
import xgboost as xgb
from sklearn.calibration import CalibratedClassifierCV
from sklearn.pipeline import Pipeline

TOP_N = 5
MISSING_PREFIX = "missingindicator_"


def input_of(column, features):
    name = column.split("__", 1)[-1].removeprefix(MISSING_PREFIX)
    if name in features:
        return name
    owners = [f for f in features if name.startswith(f + "_")]
    if not owners:
        raise ValueError(f"model column {column!r} belongs to no input")
    return max(owners, key=len)


def explainable(model):
    folds = getattr(model, "calibrated_classifiers_", None)
    return isinstance(model, CalibratedClassifierCV) and bool(folds) and all(
        isinstance(fold.estimator, Pipeline) and isinstance(fold.estimator[-1], xgb.XGBClassifier) for fold in folds
    )


def fold_contributions(pipeline, X, features):
    preprocess, booster = pipeline[:-1], pipeline[-1].get_booster()
    raw = booster.predict(xgb.DMatrix(preprocess.transform(X)), pred_contribs=True)
    owners = [input_of(c, features) for c in preprocess.get_feature_names_out()]
    per_column = pd.DataFrame(raw[:, :-1].astype(float), index=X.index, columns=owners)
    per_input = per_column.T.groupby(level=0).sum().T.reindex(columns=features, fill_value=0.0)
    return per_input, raw[:, -1].astype(float)


def contributions(model, X, features):
    folds = [fold_contributions(fold.estimator, X, features)[0] for fold in model.calibrated_classifiers_]
    return sum(folds) / len(folds)


def top_factors(model, X, features, n=TOP_N):
    if not explainable(model):
        raise TypeError(f"{type(model).__name__} is not a calibrated XGBoost pipeline")
    if len(X) != 1:
        raise ValueError(f"top factors explain one row, got {len(X)}")
    row = contributions(model, X, features).iloc[0]
    entered = X.iloc[0]
    order = row.abs().sort_values(ascending=False, kind="stable").index[:n]
    return [
        {"feature": f, "value": None if pd.isna(entered[f]) else _plain(entered[f]), "contribution": float(row[f])}
        for f in order
    ]


def _plain(value):
    return value.item() if isinstance(value, np.generic) else value


def global_importance(contribs):
    return contribs.abs().mean().sort_values(ascending=False, kind="stable")


def effect_by_group(groups, contribution, target):
    frame = pd.DataFrame({"group": groups, "contribution": contribution, "target": target})
    return frame.groupby("group", sort=True).agg(
        loans=("target", "size"), mean_contribution=("contribution", "mean"), bad_rate=("target", "mean"))
