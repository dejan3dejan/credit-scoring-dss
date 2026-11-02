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


def cost_curve(y_true, p_bad, cost_fn=COST_FN, cost_fp=COST_FP, grid=THRESHOLD_GRID):
    return pd.DataFrame({
        "threshold": grid,
        "expected_cost": [expected_cost_at(y_true, p_bad, t, cost_fn, cost_fp) for t in grid],
    })


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
