import numpy as np
from sklearn.base import clone
from sklearn.model_selection import RepeatedStratifiedKFold

from src.config import RANDOM_STATE
from src.evaluation.cost_analysis import expected_cost_at, policy_cost_curve
from src.evaluation.cv_evaluate import COST_FN, COST_FP


def bootstrap_cost_from_counts(cm, n_boot=10000, seed=RANDOM_STATE, cost_fn=COST_FN, cost_fp=COST_FP):
    cm = np.asarray(cm)
    n = int(cm.sum())
    probs = cm.ravel() / n
    draws = np.random.default_rng(seed).multinomial(n, probs, size=n_boot)
    costs = (cost_fn * draws[:, 2] + cost_fp * draws[:, 1]) / n
    low, high = np.percentile(costs, [2.5, 97.5])
    return {"cost": (cost_fn * cm[1, 0] + cost_fp * cm[0, 1]) / n, "ci_low": low, "ci_high": high}


def bootstrap_saving_vs_reject_all(cm, n_boot=10000, seed=RANDOM_STATE, cost_fn=COST_FN, cost_fp=COST_FP):
    cm = np.asarray(cm)
    n = int(cm.sum())
    per_row_diff = np.array([-cost_fp, 0, cost_fn, 0])
    draws = np.random.default_rng(seed).multinomial(n, cm.ravel() / n, size=n_boot)
    diffs = draws @ per_row_diff / n
    low, high = np.percentile(diffs, [2.5, 97.5])
    return {
        "diff": float(cm.ravel() @ per_row_diff / n),
        "ci_low": low,
        "ci_high": high,
        "share_model_cheaper": float((diffs < 0).mean()),
    }


def paired_bootstrap_cost_diff(y_true, p_a, p_b, threshold, n_boot=10000, seed=RANDOM_STATE):
    y_true = np.asarray(y_true)
    p_a = np.asarray(p_a)
    p_b = np.asarray(p_b)
    rng = np.random.default_rng(seed)
    n = len(y_true)
    diffs = np.empty(n_boot)
    for i in range(n_boot):
        idx = rng.integers(0, n, size=n)
        diffs[i] = (expected_cost_at(y_true[idx], p_a[idx], threshold)
                    - expected_cost_at(y_true[idx], p_b[idx], threshold))
    low, high = np.percentile(diffs, [2.5, 97.5])
    return {
        "diff": expected_cost_at(y_true, p_a, threshold) - expected_cost_at(y_true, p_b, threshold),
        "ci_low": low,
        "ci_high": high,
        "share_a_cheaper": float((diffs < 0).mean()),
    }


def repeated_cv_cost(pipeline, X, y, threshold, n_splits=5, n_repeats=5, seed=RANDOM_STATE):
    rskf = RepeatedStratifiedKFold(n_splits=n_splits, n_repeats=n_repeats, random_state=seed)
    costs = []
    for train_idx, val_idx in rskf.split(X, y):
        model = clone(pipeline)
        model.fit(X.iloc[train_idx], y.iloc[train_idx])
        p = model.predict_proba(X.iloc[val_idx])[:, 1]
        costs.append(expected_cost_at(y.iloc[val_idx], p, threshold))
    return np.array(costs)


def bootstrap_best_threshold(p_bad, cost_if_approved, cost_if_rejected, grid, n_boot=1000, seed=RANDOM_STATE):
    grid = np.asarray(grid)
    n = len(p_bad)
    rng = np.random.default_rng(seed)
    best = np.empty(n_boot)
    for i in range(n_boot):
        weights = np.bincount(rng.integers(0, n, size=n), minlength=n)
        best[i] = grid[np.argmin(policy_cost_curve(p_bad, cost_if_approved, cost_if_rejected, grid, weights))]
    return best
