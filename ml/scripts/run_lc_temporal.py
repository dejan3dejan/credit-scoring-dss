import sys
import time
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from sklearn.metrics import brier_score_loss, log_loss, roc_auc_score

from src.data.lending_club import COST_FN, COST_FP, FEATURES, TEST_YEAR, TRAIN_FROM, load_dataset
from src.evaluation.calibration import calibrated, recent_calibrated, reliability_table
from src.evaluation.cost_analysis import expected_cost_at, review_band_policy, theoretical_threshold
from src.models.lending_club_pipelines import xgboost_pipeline

FIG_DIR = Path(__file__).resolve().parents[1] / "experiments" / "figures"
THRESHOLD = theoretical_threshold(COST_FN, COST_FP)
REVIEW_FROM = 0.20


def years(df, *wanted):
    rows = df[df["issue_year"].isin(wanted)]
    return rows[FEATURES], rows["target"]


def scores(y, p):
    reliability = reliability_table(y, p)
    gaps = reliability["observed_bad_rate"] - reliability["mean_predicted"]
    band = review_band_policy(y, p, REVIEW_FROM, THRESHOLD, COST_FN, COST_FP)
    return {
        "loans": len(y),
        "bad_rate": y.mean(),
        "mean_p_bad": p.mean(),
        "gap": y.mean() - p.mean(),
        "roc_auc": roc_auc_score(y, p),
        "log_loss": log_loss(y, p),
        "brier": brier_score_loss(y, p),
        "max_bin_gap": gaps.abs().max(),
        "top_bin_pred": reliability["mean_predicted"].iloc[-1],
        "top_bin_obs": reliability["observed_bad_rate"].iloc[-1],
        "cost": expected_cost_at(y, p, THRESHOLD, COST_FN, COST_FP),
        "cost_approve_all": COST_FN * y.mean(),
        "share_review": band["share_review"],
        "bad_rate_review": band["bad_rate_review"],
        "share_rejected": band["share_rejected"],
        "share_of_bad_flagged": band["share_of_bad_flagged"],
    }


def main():
    df = load_dataset()
    df = df[(df["issue_year"] >= TRAIN_FROM) & (df["issue_year"] < TEST_YEAR)]
    print(f"36-month loans issued {TRAIN_FROM} to {TEST_YEAR - 1}; {TEST_YEAR} not loaded into any metric")
    print(df.groupby("issue_year")["target"].agg(loans="size", bad_rate="mean").round(4).to_string())
    print(f"served configuration (XGBoost defaults, isotonic); threshold {THRESHOLD:.4f}, review from {REVIEW_FROM}")

    X12, y12 = years(df, 2012)
    X13, y13 = years(df, 2013)
    X1213, y1213 = years(df, 2012, 2013)
    X14, y14 = years(df, 2014)
    variants = [
        ("A: 2012 -> 2013, calibrated on 2012 (inner 5-fold)", lambda: calibrated(xgboost_pipeline(), "isotonic").fit(X12, y12), X13, y13),
        ("B: 2012 -> 2014, calibrated on 2012 (inner 5-fold)", lambda: calibrated(xgboost_pipeline(), "isotonic").fit(X12, y12), X14, y14),
        ("C: 2012 -> 2014, model on 2012, isotonic refit on 2013", lambda: recent_calibrated(xgboost_pipeline(), X12, y12, X13, y13, "isotonic"), X14, y14),
        ("D: 2012-2013 -> 2014, calibrated on both (inner 5-fold)", lambda: calibrated(xgboost_pipeline(), "isotonic").fit(X1213, y1213), X14, y14),
    ]

    rows, curves = [], {}
    for name, fit, X_test, y_test in variants:
        started = time.perf_counter()
        p = fit().predict_proba(X_test)[:, 1]
        rows.append({"variant": name[:1], **scores(y_test, p)})
        curves[name] = reliability_table(y_test, p)
        print(f"\n## {name} ({round(time.perf_counter() - started)} s)")
        print("reliability (10 quantile bins):")
        print(curves[name].round(4).to_string(index=False))

    pd.set_option("display.width", 250)
    table = pd.DataFrame(rows).set_index("variant")
    print("\n## summary (gap = observed bad rate minus mean predicted; cost = expected cost per applicant at the threshold)")
    print(table.round(4).T.to_string())

    fig, ax = plt.subplots(figsize=(6, 5))
    for name, curve in curves.items():
        ax.plot(curve["mean_predicted"], curve["observed_bad_rate"], marker="o", ms=3, lw=1, label=name)
    ax.plot([0, 0.35], [0, 0.35], color="grey", ls=":", lw=1)
    ax.set_xlabel("mean predicted P(bad), by decile")
    ax.set_ylabel("observed bad rate")
    ax.set_title("LC-10 calibration on a later year")
    ax.legend(fontsize=7)
    fig.tight_layout()
    fig.savefig(FIG_DIR / "lc10_temporal_reliability.png", dpi=150)
    plt.close(fig)


if __name__ == "__main__":
    main()
