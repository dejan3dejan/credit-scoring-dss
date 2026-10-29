import numpy as np
import pandas as pd
import pytest
from sklearn.linear_model import LogisticRegression

from src.evaluation.cost_analysis import review_band_policy
from src.evaluation.robustness import (
    bootstrap_cost_from_counts,
    bootstrap_saving_vs_reject_all,
    paired_bootstrap_cost_diff,
    repeated_cv_cost,
)


def test_bootstrap_from_counts_brackets_observed_cost():
    out = bootstrap_cost_from_counts([[61, 79], [5, 55]], n_boot=2000)
    assert out["cost"] == pytest.approx(104 / 200)
    assert out["ci_low"] < out["cost"] < out["ci_high"]


def test_bootstrap_of_perfect_classifier_has_zero_width():
    out = bootstrap_cost_from_counts([[140, 0], [0, 60]], n_boot=500)
    assert out["ci_low"] == out["ci_high"] == 0


def test_bootstrap_is_reproducible_with_seed():
    a = bootstrap_cost_from_counts([[61, 79], [5, 55]], n_boot=500, seed=1)
    b = bootstrap_cost_from_counts([[61, 79], [5, 55]], n_boot=500, seed=1)
    assert a == b


def test_saving_vs_reject_all_matches_hand_computation():
    out = bootstrap_saving_vs_reject_all([[61, 79], [5, 55]], n_boot=2000)
    assert out["diff"] == pytest.approx(104 / 200 - 140 / 200)
    assert out["ci_low"] < out["diff"] < out["ci_high"]


def test_reject_all_policy_has_zero_saving_against_itself():
    out = bootstrap_saving_vs_reject_all([[0, 140], [0, 60]], n_boot=500)
    assert out["diff"] == out["ci_low"] == out["ci_high"] == 0


def test_paired_diff_of_identical_predictions_is_zero():
    y = np.array([0, 1, 0, 1, 0, 0, 1, 0])
    p = np.linspace(0.05, 0.9, 8)
    out = paired_bootstrap_cost_diff(y, p, p, threshold=1 / 6, n_boot=300)
    assert out["diff"] == out["ci_low"] == out["ci_high"] == 0


def test_repeated_cv_returns_one_cost_per_fold():
    rng = np.random.default_rng(0)
    X = pd.DataFrame({"x": rng.normal(size=200)})
    y = pd.Series((X["x"] + rng.normal(size=200) > 0.5).astype(int))
    costs = repeated_cv_cost(LogisticRegression(), X, y, threshold=1 / 6, n_splits=5, n_repeats=3)
    assert costs.shape == (15,)
    assert np.all((costs >= 0) & (costs <= 5))


def test_review_band_splits_applicants_three_ways():
    y = np.array([0, 0, 1, 0, 1, 1])
    p = np.array([0.05, 0.10, 0.15, 0.20, 0.40, 0.60])
    out = review_band_policy(y, p, low=0.12, high=0.30)
    assert out["share_approved"] == pytest.approx(2 / 6)
    assert out["share_review"] == pytest.approx(2 / 6)
    assert out["share_rejected"] == pytest.approx(2 / 6)
    assert out["bad_rate_review"] == pytest.approx(1 / 2)
    assert out["cost_per_auto_decision"] == 0


def test_band_of_zero_width_matches_single_threshold():
    y = np.array([0, 1, 0, 1, 0])
    p = np.array([0.1, 0.1, 0.3, 0.5, 0.05])
    out = review_band_policy(y, p, low=0.2, high=0.2)
    assert out["share_review"] == 0
    assert out["cost_per_auto_decision"] == pytest.approx((5 * 1 + 1 * 1) / 5)
