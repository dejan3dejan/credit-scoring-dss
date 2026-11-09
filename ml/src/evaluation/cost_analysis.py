import numpy as np
import pandas as pd
from sklearn.metrics import confusion_matrix

from src.evaluation.cv_evaluate import COST_FN, COST_FP, expected_cost

THRESHOLD_GRID = np.round(np.arange(0.01, 1.00, 0.01), 2)


def theoretical_threshold(cost_fn=COST_FN, cost_fp=COST_FP):
    return cost_fp / (cost_fp + cost_fn)


def expected_cost_at(y_true, p_bad, threshold, cost_fn=COST_FN, cost_fp=COST_FP):
    y_pred = (np.asarray(p_bad) >= threshold).astype(int)
    cm = confusion_matrix(y_true, y_pred, labels=[0, 1])
    return expected_cost(cm, cost_fn, cost_fp)


def policy_cost_curve(p_bad, cost_if_approved, cost_if_rejected, grid, weights=None):
    p_bad = np.asarray(p_bad, dtype=float)
    weights = np.ones(len(p_bad)) if weights is None else np.asarray(weights, dtype=float)
    order = np.argsort(p_bad, kind="stable")
    approved = np.searchsorted(p_bad[order], np.asarray(grid, dtype=float), side="left")
    approved_cost = np.r_[0.0, np.cumsum((weights * np.asarray(cost_if_approved, dtype=float))[order])]
    rejected_cost = np.r_[0.0, np.cumsum((weights * np.asarray(cost_if_rejected, dtype=float))[order])]
    return (approved_cost[approved] + rejected_cost[-1] - rejected_cost[approved]) / weights.sum()


def cost_curve(y_true, p_bad, cost_fn=COST_FN, cost_fp=COST_FP, grid=THRESHOLD_GRID):
    y_true = np.asarray(y_true)
    return pd.DataFrame({
        "threshold": grid,
        "expected_cost": policy_cost_curve(p_bad, cost_fn * (y_true == 1), cost_fp * (y_true == 0), grid),
    })


def dollar_curve(result, p_bad, grid=THRESHOLD_GRID):
    result = np.asarray(result, dtype=float)
    return pd.DataFrame({
        "threshold": grid,
        "result_per_applicant": -policy_cost_curve(p_bad, -result, np.zeros(len(result)), grid),
        "share_approved": policy_cost_curve(p_bad, np.ones(len(result)), np.zeros(len(result)), grid),
    })


def near_optimal_range(thresholds, costs, tolerance=0.01):
    thresholds, costs = np.asarray(thresholds), np.asarray(costs)
    best = costs.min()
    near = thresholds[costs <= best + tolerance * abs(best)]
    return float(near.min()), float(near.max())


def cost_ratios(result, target, funded):
    result, target, funded = (np.asarray(a, dtype=float) for a in (result, target, funded))
    bad, good = target == 1, target == 0
    if not bad.any() or not good.any():
        raise ValueError("cost ratios need both bad and good loans")
    return {
        "mean": -result[bad].mean() / result[good].mean(),
        "median": -np.median(result[bad]) / np.median(result[good]),
        "per_dollar": (-result[bad].sum() / funded[bad].sum()) / (result[good].sum() / funded[good].sum()),
    }


def edge_for_capacity(p_bad, high, capacity, grid):
    p_bad = np.asarray(p_bad)
    below_high = p_bad < high
    for edge in sorted(grid):
        if ((p_bad >= edge) & below_high).mean() <= capacity:
            return float(edge)
    raise ValueError(f"no edge on the grid keeps the review share within capacity {capacity}")


def optimal_threshold(y_true, p_bad, cost_fn=COST_FN, cost_fp=COST_FP, grid=THRESHOLD_GRID):
    curve = cost_curve(y_true, p_bad, cost_fn, cost_fp, grid)
    best = curve["expected_cost"].idxmin()
    return float(curve.loc[best, "threshold"]), float(curve.loc[best, "expected_cost"])


def sensitivity_table(y_true, oof_by_model, cost_ratios=(1, 2, 5, 10)):
    rows = []
    for name, p in oof_by_model.items():
        for r in cost_ratios:
            t_theory = theoretical_threshold(r, 1)
            t_opt, c_opt = optimal_threshold(y_true, p, r, 1)
            rows.append({
                "model": name,
                "cost_ratio": f"{r}:1",
                "theoretical_threshold": t_theory,
                "cost_at_theoretical": expected_cost_at(y_true, p, t_theory, r, 1),
                "tuned_threshold": t_opt,
                "cost_at_tuned": c_opt,
            })
    return pd.DataFrame(rows)


def acceptance_rate_policy(y_true, p_bad, acceptance_rate):
    y_true = np.asarray(y_true)
    p_bad = np.asarray(p_bad)
    threshold = float(np.quantile(p_bad, acceptance_rate))
    accepted = p_bad < threshold
    return {
        "target_acceptance": acceptance_rate,
        "threshold": threshold,
        "realised_acceptance": float(accepted.mean()),
        "bad_rate_among_accepted": float(y_true[accepted].mean()),
        "expected_cost": expected_cost_at(y_true, p_bad, threshold),
    }


def review_band_policy(y_true, p_bad, low, high, cost_fn=COST_FN, cost_fp=COST_FP):
    y_true = np.asarray(y_true)
    p_bad = np.asarray(p_bad)
    approve = p_bad < low
    reject = p_bad >= high
    review = ~approve & ~reject
    auto = approve | reject
    auto_cost = cost_fn * (approve & (y_true == 1)).sum() + cost_fp * (reject & (y_true == 0)).sum()
    return {
        "low": low,
        "high": high,
        "share_approved": float(approve.mean()),
        "share_rejected": float(reject.mean()),
        "share_review": float(review.mean()),
        "bad_rate_review": float(y_true[review].mean()) if review.any() else float("nan"),
        "bad_rate_approved": float(y_true[approve].mean()) if approve.any() else float("nan"),
        "share_of_bad_flagged": float((~approve & (y_true == 1)).sum() / (y_true == 1).sum()),
        "cost_per_auto_decision": float(auto_cost / auto.sum()) if auto.any() else float("nan"),
    }


def review_band_table(y_true, p_bad, bands, cost_fn=COST_FN, cost_fp=COST_FP):
    return pd.DataFrame([review_band_policy(y_true, p_bad, low, high, cost_fn, cost_fp) for low, high in bands])


def acceptance_table(y_true, oof_by_model, rates=(0.6, 0.7, 0.8)):
    return pd.DataFrame([
        {"model": name, **acceptance_rate_policy(y_true, p, r)}
        for name, p in oof_by_model.items()
        for r in rates
    ])
