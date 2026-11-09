import sys
import time
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

from src.data.lending_club import FEATURES, TEST_YEAR, TRAIN_FROM, load_dataset
from src.models.artifact import load_artifact
from src.models.explain import contributions, effect_by_group, global_importance

MODEL_PATH = Path(__file__).resolve().parents[1] / "models" / "credit_lc_xgb_isotonic_v1.joblib"
FIG_DIR = Path(__file__).resolve().parents[1] / "experiments" / "figures"
CATEGORICAL = ("purpose", "home_ownership", "verification_status")
FICO_BANDS = [0, 690, 720, 750, 900]
FICO_LABELS = ["< 690", "690-719", "720-749", ">= 750"]
TOP_SHOWN = 8


def groups_of(values):
    if values.name in CATEGORICAL:
        return values.fillna("missing")
    if values.nunique() <= 6:
        return values.fillna(-1).astype(int).astype(str).replace("-1", "missing")
    quintiles = pd.qcut(values, 5, duplicates="drop")
    labels = {iv: f"Q{i} {iv}" for i, iv in enumerate(quintiles.cat.categories, start=1)}
    return quintiles.map(labels).astype(str).where(values.notna(), "missing")


def delinquency_groups(values):
    return values.clip(upper=3).astype(int).astype(str).replace("3", "3+")


def main():
    started = time.perf_counter()
    model, metadata = load_artifact(MODEL_PATH)
    df = load_dataset()
    train = df[(df["issue_year"] >= TRAIN_FROM) & (df["issue_year"] < TEST_YEAR)]
    X, y = train[FEATURES], train["target"]
    print(f"model {metadata['model_id']}; {len(X)} training loans {TRAIN_FROM} to {TEST_YEAR - 1}; {TEST_YEAR} not used")
    print("contributions: XGBoost TreeSHAP in log-odds before isotonic calibration, mean over the 5 fold models")
    print("the model saw these loans in training: this describes the model, it does not test it")

    contribs = contributions(model, X, FEATURES)
    print(f"computed in {round(time.perf_counter() - started)} s")

    importance = global_importance(contribs)
    table = pd.DataFrame({"mean_abs_contribution": importance, "share": importance / importance.sum()})
    print("\n## global importance (mean |contribution| over all training loans)")
    print(table.round(4).to_string())

    print("\n## effect by value for the most important inputs (quintiles for numbers)")
    for feature in importance.index[:TOP_SHOWN]:
        print(f"\n{feature}:")
        print(effect_by_group(groups_of(X[feature]), contribs[feature], y).round(4).to_string())

    delinq = delinquency_groups(X["delinq_2yrs"])
    fico_band = pd.cut(X["fico"], FICO_BANDS, labels=FICO_LABELS, right=False).astype(str)
    print("\n## delinquencies in the last 2 years (G36)")
    by_delinq = effect_by_group(delinq, contribs["delinq_2yrs"], y)
    by_delinq["mean_fico"] = X.groupby(delinq)["fico"].mean()
    by_delinq["mean_p_bad"] = pd.Series(model.predict_proba(X)[:, 1], index=X.index).groupby(delinq).mean()
    print(by_delinq.round(4).to_string())
    print("\nobserved bad rate by FICO band (rows) and delinquencies (columns):")
    print(pd.crosstab(fico_band, delinq, values=y, aggfunc="mean").reindex(FICO_LABELS).round(4).to_string())
    print("\nloans per cell:")
    print(pd.crosstab(fico_band, delinq).reindex(FICO_LABELS).to_string())
    print("\nmean delinq_2yrs contribution by FICO band and delinquencies:")
    print(pd.crosstab(fico_band, delinq, values=contribs["delinq_2yrs"], aggfunc="mean")
          .reindex(FICO_LABELS).round(4).to_string())

    fig, axes = plt.subplots(1, 2, figsize=(12, 4.5))
    shown = table["mean_abs_contribution"].iloc[::-1]
    axes[0].barh(shown.index, shown.values, color="grey")
    axes[0].set_xlabel("mean |contribution| (log-odds)")
    axes[0].set_title("which inputs move the score")
    x = np.arange(len(by_delinq))
    axes[1].bar(x - 0.2, by_delinq["bad_rate"], width=0.4, color="black", label="observed bad rate")
    axes[1].set_xticks(x, by_delinq.index)
    axes[1].set_xlabel("delinquencies in the last 2 years")
    axes[1].set_ylabel("observed bad rate")
    twin = axes[1].twinx()
    twin.bar(x + 0.2, by_delinq["mean_contribution"], width=0.4, color="tab:blue", label="mean contribution")
    twin.axhline(0, color="tab:blue", lw=0.6)
    twin.set_ylabel("mean delinq_2yrs contribution (log-odds)")
    axes[1].set_title("delinquencies: observed risk and the model's use of them")
    fig.legend(loc="lower right", fontsize=8)
    fig.suptitle(f"LC-11 global explanations, training years {TRAIN_FROM} to {TEST_YEAR - 1}")
    fig.tight_layout()
    fig.savefig(FIG_DIR / "lc11_explanations.png", dpi=150)
    plt.close(fig)
    print(f"\ntotal {round(time.perf_counter() - started)} s")


if __name__ == "__main__":
    main()
