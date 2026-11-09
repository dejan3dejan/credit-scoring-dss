import sys
import time
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import numpy as np
import pandas as pd
from sklearn.metrics import log_loss
from sklearn.model_selection import StratifiedKFold

from src.config import RANDOM_STATE
from src.data.lending_club import COST_FN, COST_FP, TEST_YEAR, TRAIN_FROM, load_dataset, split_by_time
from src.evaluation.calibration import calibrated
from src.evaluation.cost_analysis import theoretical_threshold
from src.evaluation.cv_evaluate import fold_metrics, oof_predict
from src.models.lending_club_pipelines import XGB_SEARCH_SPACE, sample_xgb_params, xgboost_pipeline

N_CONFIGS = 20
THRESHOLD = theoretical_threshold(COST_FN, COST_FP)
METRICS = ("log_loss", "roc_auc", "brier", "expected_cost")
FOLDS_PATH = Path(__file__).resolve().parents[1] / "experiments" / "lc09_search_folds.csv"


def main():
    df = load_dataset()
    X, _, y, _ = split_by_time(df, TEST_YEAR, TRAIN_FROM)
    skf = StratifiedKFold(n_splits=5, shuffle=True, random_state=RANDOM_STATE)
    folds = list(skf.split(X, y))
    configs = [("default", {})] + [(f"c{i:02d}", params)
                                   for i, params in enumerate(sample_xgb_params(N_CONFIGS, RANDOM_STATE), start=1)]

    print(f"train {TRAIN_FROM} to {TEST_YEAR - 1}: {X.shape}, bad rate {y.mean():.4f}; {TEST_YEAR} not used")
    print(f"random search, {N_CONFIGS} configurations plus the default, seed {RANDOM_STATE}")
    print("space: " + "; ".join(f"{k} {kind} [{low}, {high}]" for k, (kind, low, high) in XGB_SEARCH_SPACE.items()))
    print(f"each configuration: isotonic inner 5-fold inside the same outer stratified 5 folds; "
          f"expected cost at {THRESHOLD:.4f} (FN {COST_FN}, FP {COST_FP})")
    print("default = XGBClassifier(eval_metric='logloss') with library defaults, the served configuration", flush=True)

    fold_rows = []
    for name, params in configs:
        started = time.perf_counter()
        pipeline = xgboost_pipeline().set_params(**{f"clf__{k}": v for k, v in params.items()})
        oof = oof_predict(calibrated(pipeline, "isotonic"), X, y, skf)
        table = fold_metrics(y, oof, folds, THRESHOLD, COST_FN, COST_FP)
        seconds = round(time.perf_counter() - started)
        fold_rows.append(table.assign(config=name, fold=range(1, len(folds) + 1)))
        shown = ", ".join(f"{k} {v:.4g}" for k, v in params.items()) or "library defaults"
        print(f"{name} ({seconds} s; {shown}): "
              + ", ".join(f"{m} {table[m].mean():.5f} +- {table[m].std(ddof=0):.5f}" for m in METRICS)
              + f", pooled log loss {log_loss(y, oof):.5f}", flush=True)

    folds_table = pd.concat(fold_rows, ignore_index=True)
    folds_table.to_csv(FOLDS_PATH, index=False)
    default = folds_table[folds_table["config"] == "default"].set_index("fold")

    rows = []
    for name, group in folds_table.groupby("config", sort=False):
        group = group.set_index("fold")
        row = {"config": name}
        for m in METRICS:
            diff = group[m] - default[m]
            row[m] = group[m].mean()
            row[f"{m}_std"] = group[m].std(ddof=0)
            row[f"d_{m}"] = diff.mean()
            row[f"d_{m}_std"] = diff.std(ddof=0)
        row["folds_better_log_loss"] = int((group["log_loss"] < default["log_loss"]).sum())
        rows.append(row)
    summary = pd.DataFrame(rows).sort_values("log_loss")

    pd.set_option("display.width", 250)
    print("\n## summary, sorted by mean fold log loss (d_ = config minus default, paired by fold; std over 5 folds)")
    print(summary[["config"] + [c for m in METRICS for c in (m, f"{m}_std")]].round(5).to_string(index=False))
    print("\n## paired differences against the default")
    print(summary[["config"] + [c for m in METRICS for c in (f"d_{m}", f"d_{m}_std")] + ["folds_better_log_loss"]]
          .round(5).to_string(index=False))
    best = summary.iloc[0]
    print(f"\nbest by log loss: {best['config']} {dict(configs)[best['config']]}")
    print(f"fold metrics saved to {FOLDS_PATH.name}")


if __name__ == "__main__":
    main()
