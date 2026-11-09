import numpy as np
import pytest
from sklearn.metrics import log_loss, roc_auc_score
from sklearn.model_selection import StratifiedKFold

from src.evaluation.cost_analysis import expected_cost_at
from src.evaluation.cv_evaluate import fold_metrics
from src.models.lending_club_pipelines import XGB_SEARCH_SPACE, sample_xgb_params, xgboost_pipeline


def test_sampled_configurations_are_reproducible_with_the_seed():
    assert sample_xgb_params(5, seed=42) == sample_xgb_params(5, seed=42)
    assert sample_xgb_params(5, seed=42) != sample_xgb_params(5, seed=7)


def test_sampled_values_stay_inside_the_search_space():
    for params in sample_xgb_params(200, seed=0):
        assert set(params) == set(XGB_SEARCH_SPACE)
        for name, value in params.items():
            low, high = XGB_SEARCH_SPACE[name][1:]
            assert low <= value <= high


def test_tree_counts_and_depths_are_whole_numbers():
    for params in sample_xgb_params(20, seed=0):
        assert isinstance(params["n_estimators"], int)
        assert isinstance(params["max_depth"], int)


def test_sampled_parameters_can_be_set_on_the_pipeline():
    params = sample_xgb_params(1, seed=0)[0]
    clf = xgboost_pipeline().set_params(**{f"clf__{k}": v for k, v in params.items()}).named_steps["clf"]
    assert clf.get_params()["max_depth"] == params["max_depth"]
    assert clf.get_params()["random_state"] == 42


def test_fold_metrics_score_each_validation_fold_separately():
    rng = np.random.default_rng(0)
    y = (rng.random(200) < 0.3).astype(int)
    oof = np.clip(0.3 + 0.2 * (y - 0.3) + rng.normal(0, 0.1, 200), 0.01, 0.99)
    folds = list(StratifiedKFold(n_splits=4, shuffle=True, random_state=0).split(np.zeros(200), y))

    table = fold_metrics(y, oof, folds, threshold=0.33, cost_fn=2.0, cost_fp=1.0)

    assert len(table) == 4
    _, val = folds[2]
    row = table.iloc[2]
    assert row["roc_auc"] == pytest.approx(roc_auc_score(y[val], oof[val]))
    assert row["log_loss"] == pytest.approx(log_loss(y[val], oof[val]))
    assert row["expected_cost"] == pytest.approx(expected_cost_at(y[val], oof[val], 0.33, 2.0, 1.0))
    assert {"brier"} <= set(table.columns)
