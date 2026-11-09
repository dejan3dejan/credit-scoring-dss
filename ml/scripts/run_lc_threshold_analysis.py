import sys
import time
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from sklearn.metrics import log_loss, roc_auc_score
from sklearn.model_selection import StratifiedKFold

from src.config import RANDOM_STATE
from src.data.lending_club import COST_FN, COST_FP, FEATURES, TEST_YEAR, TRAIN_FROM, load_dataset
from src.evaluation.calibration import calibrated
from src.evaluation.cost_analysis import (
    cost_curve,
    cost_ratios,
    dollar_curve,
    edge_for_capacity,
    expected_cost_at,
    near_optimal_range,
    review_band_policy,
    theoretical_threshold,
)
from src.evaluation.cv_evaluate import oof_predict
from src.evaluation.review_value import band_money, break_even_catch, max_cost_per_review
from src.evaluation.robustness import bootstrap_best_threshold
from src.models.lending_club_pipelines import xgboost_pipeline

FIG_DIR = Path(__file__).resolve().parents[1] / "experiments" / "figures"
THRESHOLD = theoretical_threshold(COST_FN, COST_FP)
CURRENT_EDGE = 0.20
FINE_GRID = np.round(np.arange(0.001, 1.0, 0.001), 3)
LOWER_EDGES = np.round(np.arange(0.10, 0.301, 0.01), 2)
CAPACITY_GRID = np.round(np.arange(0.05, THRESHOLD, 0.001), 3)
CAPACITIES = (0.05, 0.10, 0.15, 0.20)
SKILLS = ((0.4, 0.1), (0.6, 0.1), (0.6, 0.2), (1.0, 0.0))
N_BOOT = 1000


def training_years():
    df = load_dataset(with_result=True)
    return df[(df["issue_year"] >= TRAIN_FROM) & (df["issue_year"] < TEST_YEAR)].reset_index(drop=True)


def cost_curve_part(y, p, result):
    unit = cost_curve(y, p, COST_FN, COST_FP)
    unit_fine = cost_curve(y, p, COST_FN, COST_FP, FINE_GRID)
    money_fine = dollar_curve(result, p, FINE_GRID)
    best_unit = unit_fine.loc[unit_fine["expected_cost"].idxmin()]
    best_money = money_fine.loc[money_fine["result_per_applicant"].idxmax()]
    at_threshold = expected_cost_at(y, p, THRESHOLD, COST_FN, COST_FP)
    money_at_threshold = dollar_curve(result, p, [THRESHOLD])["result_per_applicant"].iloc[0]

    print("\n## part 1: empirical cost curve against the formula")
    print(f"formula threshold 1 / (1 + {COST_FN}) = {THRESHOLD:.4f}")
    print(f"approve everyone: expected cost {COST_FN * y.mean():.5f}, result per applicant {result.mean():.2f}")
    print(f"unit cost (FN {COST_FN}, FP {COST_FP}) per applicant:")
    print(f"  at 0.3286: {at_threshold:.5f}")
    print(f"  empirical minimum on 0.001 grid: threshold {best_unit['threshold']:.3f}, cost {best_unit['expected_cost']:.5f}")
    print(f"  empirical minimum on 0.01 grid: threshold {unit.loc[unit['expected_cost'].idxmin(), 'threshold']:.2f}")
    for tol in (0.01, 0.001):
        low, high = near_optimal_range(FINE_GRID, unit_fine["expected_cost"], tol)
        print(f"  thresholds within {tol:.1%} of the minimum: {low:.3f} to {high:.3f}")
    print("dollar result per applicant (recorded total_pymnt - funded_amnt of approved loans, divided by all applicants):")
    print(f"  at 0.3286: {money_at_threshold:.2f}")
    print(f"  empirical maximum on 0.001 grid: threshold {best_money['threshold']:.3f}, "
          f"result {best_money['result_per_applicant']:.2f}, approved {best_money['share_approved']:.4f}")
    for tol in (0.01, 0.001):
        low, high = near_optimal_range(FINE_GRID, -money_fine["result_per_applicant"], tol)
        print(f"  thresholds within {tol:.1%} of the maximum: {low:.3f} to {high:.3f}")

    started = time.perf_counter()
    boot_unit = bootstrap_best_threshold(p, COST_FN * (y == 1), COST_FP * (y == 0), FINE_GRID, N_BOOT)
    boot_money = bootstrap_best_threshold(p, -result, np.zeros(len(result)), FINE_GRID, N_BOOT)
    print(f"bootstrap of the optimal threshold ({N_BOOT} resamples of the OOF predictions, seed {RANDOM_STATE}, "
          f"model held fixed; {round(time.perf_counter() - started)} s):")
    for name, boot in (("unit cost", boot_unit), ("dollars", boot_money)):
        q = np.percentile(boot, [2.5, 25, 50, 75, 97.5])
        print(f"  {name:9s} 2.5% {q[0]:.3f}  25% {q[1]:.3f}  median {q[2]:.3f}  75% {q[3]:.3f}  97.5% {q[4]:.3f}  "
              f"share >= 0.3286 {np.mean(boot >= THRESHOLD):.3f}")

    print("curve on the 0.01 grid (threshold, unit cost, dollars per applicant, share approved):")
    money = dollar_curve(result, p)
    table = unit.assign(result_per_applicant=money["result_per_applicant"], share_approved=money["share_approved"])
    print(table[table["threshold"] <= 0.80].round(5).to_string(index=False))

    fig, axes = plt.subplots(1, 2, figsize=(11, 4))
    for ax, curve, column, boot, best, label in (
        (axes[0], unit_fine, "expected_cost", boot_unit, best_unit["threshold"], "expected cost per applicant (unit costs)"),
        (axes[1], money_fine, "result_per_applicant", boot_money, best_money["threshold"], "result per applicant (USD)"),
    ):
        shown = curve[curve["threshold"] <= 0.8]
        ax.plot(shown["threshold"], shown[column], color="black", lw=1)
        ax.axvspan(*np.percentile(boot, [2.5, 97.5]), color="grey", alpha=0.25, label="bootstrap 95% of optimum")
        ax.axvline(THRESHOLD, color="tab:red", ls="--", lw=1, label=f"formula {THRESHOLD:.4f}")
        ax.axvline(best, color="tab:blue", ls=":", lw=1, label=f"empirical optimum {best:.3f}")
        ax.set_xlabel("reject if P(bad) >= threshold")
        ax.set_ylabel(label)
        ax.legend(fontsize=8)
    fig.suptitle(f"LC-08 cost curve, training years {TRAIN_FROM} to {TEST_YEAR - 1}, out-of-fold")
    fig.tight_layout()
    fig.savefig(FIG_DIR / "lc08_cost_curve.png", dpi=150)
    plt.close(fig)


def cost_ratio_part(y, p, result, funded):
    good, bad = y == 0, y == 1
    ratios = cost_ratios(result, y, funded)
    money_at_threshold = dollar_curve(result, p, [THRESHOLD])["result_per_applicant"].iloc[0]

    print("\n## part 2: cost-ratio sensitivity")
    print(f"good loans {good.sum()}, bad loans {bad.sum()}")
    print(f"  mean:   loss {-result[bad].mean():.2f} / gain {result[good].mean():.2f}")
    print(f"  median: loss {-np.median(result[bad]):.2f} / gain {np.median(result[good]):.2f}")
    print(f"  per dollar lent: loss {-result[bad].sum():,.0f} / {funded[bad].sum():,.0f} = "
          f"{-result[bad].sum() / funded[bad].sum():.5f}; gain {result[good].sum():,.0f} / {funded[good].sum():,.0f} = "
          f"{result[good].sum() / funded[good].sum():.5f}")
    print(f"  mean funded amount: bad {funded[bad].mean():.2f}, good {funded[good].mean():.2f}")

    rows = []
    for name, ratio in ratios.items():
        t = theoretical_threshold(ratio, 1)
        rows.append({
            "basis": name,
            "ratio": ratio,
            "threshold": t,
            "share_approved": float(np.mean(p < t)),
            "share_rejected": float(np.mean(p >= t)),
            "own_cost_at_t": expected_cost_at(y, p, t, ratio, 1),
            "own_cost_diff_vs_3286": expected_cost_at(y, p, t, ratio, 1) - expected_cost_at(y, p, THRESHOLD, ratio, 1),
            "mean_cost_diff_vs_3286": expected_cost_at(y, p, t, COST_FN, COST_FP)
                                      - expected_cost_at(y, p, THRESHOLD, COST_FN, COST_FP),
            "usd_per_applicant_diff_vs_3286": dollar_curve(result, p, [t])["result_per_applicant"].iloc[0]
                                              - money_at_threshold,
        })
    table = pd.DataFrame(rows)
    print("own_cost: unit expected cost using that basis's ratio; mean_cost: using the current 2.0431; "
          "usd: recorded dollars, positive = more profit than at 0.3286")
    print(table.round(5).to_string(index=False))
    print(f"usd difference over all {len(y)} training loans: "
          + ", ".join(f"{r['basis']} {r['usd_per_applicant_diff_vs_3286'] * len(y):,.0f}" for r in rows))

    curve = dollar_curve(result, p, FINE_GRID)
    shown = curve[(curve["threshold"] >= 0.15) & (curve["threshold"] <= 0.6)]
    fig, ax = plt.subplots(figsize=(7, 4))
    ax.plot(shown["threshold"], shown["result_per_applicant"], color="black", lw=1)
    for (name, row), color in zip(table.set_index("basis").iterrows(), ("tab:red", "tab:green", "tab:purple")):
        ax.axvline(row["threshold"], color=color, ls="--", lw=1, label=f"{name}: ratio {row['ratio']:.3f}, t {row['threshold']:.4f}")
    ax.set_xlabel("reject if P(bad) >= threshold")
    ax.set_ylabel("result per applicant (USD)")
    ax.set_title("LC-08 thresholds from three cost-ratio definitions")
    ax.legend(fontsize=8)
    fig.tight_layout()
    fig.savefig(FIG_DIR / "lc08_cost_ratio.png", dpi=150)
    plt.close(fig)


def band_row(y, p, result, low):
    policy = review_band_policy(y, p, low, THRESHOLD, COST_FN, COST_FP)
    money = band_money(result, y, p, low, THRESHOLD)
    row = {
        "low": low,
        "share_review": policy["share_review"],
        "bad_rate_band": policy["bad_rate_review"],
        "share_of_bad_in_band": money["band_bad"] / int(np.sum(y == 1)),
        "bad_usd": money["band_bad_result"],
        "good_usd": money["band_good_result"],
        "break_even_ratio": break_even_catch(money, 1.0),
    }
    for catch, false_reject in SKILLS:
        row[f"max_cost_c{catch:.1f}_f{false_reject:.1f}"] = max_cost_per_review(money, catch, false_reject)
    return row


def review_edge_part(y, p, result):
    print("\n## part 3: review-edge trade-off (upper edge fixed at 0.3286)")
    print("break_even_ratio: the band pays (review cost 0) only if catch rate / false-reject rate exceeds it; "
          "= good loans' total result / bad loans' total loss in the band")
    print("max_cost_cX_fY: most a review may cost per reviewed loan (USD) for a reviewer who rejects X of the band's "
          "bad loans and Y of its good ones")
    table = pd.DataFrame([band_row(y, p, result, low) for low in LOWER_EDGES])
    pd.set_option("display.width", 250)
    print(table.round(4).to_string(index=False))

    print("\ncapacity: widest band (lowest edge on a 0.001 grid) whose review share does not exceed the capacity")
    picked = pd.DataFrame([{"capacity": c, **band_row(y, p, result, edge_for_capacity(p, THRESHOLD, c, CAPACITY_GRID))}
                           for c in CAPACITIES])
    print(picked.round(4).to_string(index=False))
    current = band_row(y, p, result, CURRENT_EDGE)
    print(f"current edge {CURRENT_EDGE}: review share {current['share_review']:.4f}, "
          f"bad loans in band {current['share_of_bad_in_band']:.4f}, break-even ratio {current['break_even_ratio']:.4f}")

    fig, axes = plt.subplots(1, 2, figsize=(11, 4))
    axes[0].plot(table["share_review"], table["share_of_bad_in_band"], marker="o", ms=3, color="black", lw=1)
    axes[1].plot(table["share_review"], table["break_even_ratio"], marker="o", ms=3, color="black", lw=1)
    for _, row in table[np.isin(table["low"], (0.10, 0.15, 0.20, 0.25, 0.30))].iterrows():
        axes[0].annotate(f"{row['low']:.2f}", (row["share_review"], row["share_of_bad_in_band"]), fontsize=8,
                         xytext=(4, -10), textcoords="offset points")
        axes[1].annotate(f"{row['low']:.2f}", (row["share_review"], row["break_even_ratio"]), fontsize=8,
                         xytext=(4, 4), textcoords="offset points")
    for ax in axes:
        for c in CAPACITIES:
            ax.axvline(c, color="grey", ls=":", lw=0.8)
        ax.set_xlabel("share of applications sent to review")
    axes[0].set_ylabel("share of all bad loans in the band")
    axes[1].set_ylabel("break-even catch / false-reject ratio")
    fig.suptitle("LC-08 review edge against review capacity (labels: lower edge)")
    fig.tight_layout()
    fig.savefig(FIG_DIR / "lc08_review_capacity.png", dpi=150)
    plt.close(fig)


def main():
    started = time.perf_counter()
    train = training_years()
    X, y = train[FEATURES], train["target"]
    skf = StratifiedKFold(n_splits=5, shuffle=True, random_state=RANDOM_STATE)

    print(f"train {TRAIN_FROM} to {TEST_YEAR - 1}: {X.shape}, bad rate {y.mean():.4f}; {TEST_YEAR} not used")
    print(f"served configuration: XGBoost defaults, isotonic inner 5-fold, outer stratified 5-fold OOF, seed {RANDOM_STATE}")
    oof = oof_predict(calibrated(xgboost_pipeline(), "isotonic"), X, y, skf)
    fold_auc = [roc_auc_score(y.iloc[v], oof[v]) for _, v in skf.split(X, y)]
    print(f"OOF check against LC-03: ROC-AUC {np.mean(fold_auc):.4f} +- {np.std(fold_auc):.4f}, "
          f"log loss {log_loss(y, oof):.4f}, max P(bad) {oof.max():.4f} ({round(time.perf_counter() - started)} s)")

    y_arr, result, funded = y.to_numpy(), train["result"].to_numpy(), train["funded_amnt"].to_numpy()
    cost_curve_part(y_arr, oof, result)
    cost_ratio_part(y_arr, oof, result, funded)
    review_edge_part(y_arr, oof, result)
    print(f"\ntotal {round(time.perf_counter() - started)} s")


if __name__ == "__main__":
    main()
