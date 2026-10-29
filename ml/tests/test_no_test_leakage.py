import numpy as np
import pandas as pd
from sklearn.base import BaseEstimator, ClassifierMixin
from sklearn.model_selection import RepeatedStratifiedKFold

from src.config import RANDOM_STATE
from src.evaluation.calibration import calibrated
from src.evaluation.cost_analysis import optimal_threshold
from src.evaluation.cv_evaluate import oof_predict
from src.evaluation.robustness import repeated_cv_cost
from src.evaluation.splits import get_cv_and_holdout

FIT_LOG = []


class SpyClassifier(ClassifierMixin, BaseEstimator):
    def fit(self, X, y):
        self.classes_ = np.array([0, 1])
        self.seen_ = set(X["row_id"])
        FIT_LOG.append(self.seen_)
        return self

    def predict_proba(self, X):
        p = np.where(X["row_id"].isin(self.seen_), 0.9, 0.1)
        return np.column_stack([1 - p, p])


def synthetic_split():
    rng = np.random.default_rng(0)
    n = 200
    df = pd.DataFrame({
        "row_id": np.arange(n),
        "noise": rng.normal(size=n),
        "target": (np.arange(n) % 10 < 3).astype(int),
    })
    return get_cv_and_holdout(df)


def test_oof_predictions_are_out_of_fold():
    X_train, _, y_train, _, skf = synthetic_split()
    oof = oof_predict(SpyClassifier(), X_train, y_train, skf)
    assert np.allclose(oof, 0.1)


def test_calibrator_never_fits_on_holdout_or_scored_fold():
    FIT_LOG.clear()
    X_train, X_test, y_train, _, skf = synthetic_split()

    oof_predict(calibrated(SpyClassifier(), "isotonic"), X_train, y_train, skf)

    holdout_ids = set(X_test["row_id"])
    assert FIT_LOG, "spy was never fitted"
    assert all(seen.isdisjoint(holdout_ids) for seen in FIT_LOG)

    folds = list(skf.split(X_train, y_train))
    fits_per_fold = len(FIT_LOG) // len(folds)
    for k, (_, val_idx) in enumerate(folds):
        val_ids = set(X_train["row_id"].iloc[val_idx])
        for seen in FIT_LOG[k * fits_per_fold:(k + 1) * fits_per_fold]:
            assert seen.isdisjoint(val_ids)


def test_repeated_cv_fits_never_see_their_validation_rows():
    FIT_LOG.clear()
    X_train, X_test, y_train, _, _ = synthetic_split()
    repeated_cv_cost(SpyClassifier(), X_train, y_train, threshold=0.5, n_splits=5, n_repeats=2)

    folds = list(RepeatedStratifiedKFold(n_splits=5, n_repeats=2, random_state=RANDOM_STATE)
                 .split(X_train, y_train))
    assert len(FIT_LOG) == len(folds)
    holdout_ids = set(X_test["row_id"])
    for seen, (_, val_idx) in zip(FIT_LOG, folds):
        assert seen.isdisjoint(set(X_train["row_id"].iloc[val_idx]))
        assert seen.isdisjoint(holdout_ids)


def test_threshold_is_chosen_from_train_oof_only():
    X_train, X_test, y_train, _, skf = synthetic_split()
    oof = oof_predict(SpyClassifier(), X_train, y_train, skf)
    assert len(oof) == len(X_train) and len(X_train) + len(X_test) == 200
    optimal_threshold(y_train, oof)
