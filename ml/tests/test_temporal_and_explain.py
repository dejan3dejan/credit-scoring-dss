import numpy as np
import pandas as pd
import pytest
from sklearn.linear_model import LogisticRegression

from src.evaluation.calibration import recent_calibrated
from src.models.explain import effect_by_group, global_importance


@pytest.fixture
def years():
    rng = np.random.default_rng(0)
    X = pd.DataFrame({"x": rng.normal(size=2000)})
    y = pd.Series((rng.random(2000) < 1 / (1 + np.exp(-(X["x"] - 1.5)))).astype(int))
    return X.iloc[:1000], y.iloc[:1000], X.iloc[1000:], y.iloc[1000:]


def test_recent_calibration_keeps_the_model_fitted_on_the_earlier_years(years):
    X_fit, y_fit, X_cal, y_cal = years
    model = recent_calibrated(LogisticRegression(), X_fit, y_fit, X_cal, y_cal, "isotonic")
    alone = LogisticRegression().fit(X_fit, y_fit)
    frozen = model.calibrated_classifiers_[0].estimator
    assert np.allclose(frozen.predict_proba(X_cal), alone.predict_proba(X_cal))


def test_recent_calibration_matches_the_calibration_year_on_average(years):
    X_fit, y_fit, X_cal, _ = years
    y_shifted = pd.Series(np.r_[np.ones(300, dtype=int), np.zeros(700, dtype=int)], index=X_cal.index)
    model = recent_calibrated(LogisticRegression(), X_fit, y_fit, X_cal, y_shifted, "isotonic")
    assert model.predict_proba(X_cal)[:, 1].mean() == pytest.approx(0.3, abs=1e-6)


def test_global_importance_ranks_inputs_by_mean_absolute_contribution():
    contribs = pd.DataFrame({"a": [0.1, -0.1, 0.1], "b": [-0.5, 0.5, 0.2], "c": [0.0, 0.0, 0.0]})
    out = global_importance(contribs)
    assert list(out.index) == ["b", "a", "c"]
    assert out["b"] == pytest.approx(0.4)


def test_effect_by_group_reports_count_contribution_and_bad_rate():
    groups = pd.Series(["0", "0", "1", "1", "1"])
    contribution = pd.Series([-0.2, 0.0, 0.3, 0.1, 0.2])
    target = pd.Series([0, 1, 1, 0, 0])
    out = effect_by_group(groups, contribution, target)
    assert list(out.index) == ["0", "1"]
    assert list(out["loans"]) == [2, 3]
    assert out.loc["0", "mean_contribution"] == pytest.approx(-0.1)
    assert out.loc["1", "bad_rate"] == pytest.approx(1 / 3)
