import pandas as pd
from sklearn.base import clone
from sklearn.calibration import CalibratedClassifierCV, calibration_curve
from sklearn.metrics import brier_score_loss
from sklearn.frozen import FrozenEstimator
from sklearn.model_selection import StratifiedKFold

from src.config import RANDOM_STATE


def calibrated(pipeline, method):
    inner_cv = StratifiedKFold(n_splits=5, shuffle=True, random_state=RANDOM_STATE)
    return CalibratedClassifierCV(pipeline, method=method, cv=inner_cv)


def recent_calibrated(pipeline, X_fit, y_fit, X_cal, y_cal, method):
    fitted = clone(pipeline).fit(X_fit, y_fit)
    return CalibratedClassifierCV(FrozenEstimator(fitted), method=method).fit(X_cal, y_cal)


def reliability_table(y_true, p_bad, n_bins=10):
    observed, predicted = calibration_curve(y_true, p_bad, n_bins=n_bins, strategy="quantile")
    return pd.DataFrame({"mean_predicted": predicted, "observed_bad_rate": observed})


def brier_table(y_true, oof_by_model):
    return pd.DataFrame([
        {"model": name, "brier": brier_score_loss(y_true, p), "mean_p_bad": float(p.mean())}
        for name, p in oof_by_model.items()
    ])
