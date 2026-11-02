import sys
import time
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import numpy as np
import pandas as pd
from sklearn.metrics import brier_score_loss, confusion_matrix, log_loss, roc_auc_score
from sklearn.model_selection import StratifiedKFold

from src.config import RANDOM_STATE
from src.data.lending_club import COST_FN, COST_FP, TEST_YEAR, TRAIN_FROM, load_dataset, split_by_time
from src.evaluation.cv_evaluate import expected_cost, oof_predict
from src.models.lending_club_pipelines import build_lc_candidates

THRESHOLD = COST_FP / (COST_FP + COST_FN)


def main():
    df = load_dataset()
    X_train, _, y_train, _ = split_by_time(df, TEST_YEAR, TRAIN_FROM)
    skf = StratifiedKFold(n_splits=5, shuffle=True, random_state=RANDOM_STATE)

    print(f"train {TRAIN_FROM} to {TEST_YEAR - 1}: {X_train.shape}, bad rate {y_train.mean():.4f}")
    print(f"cost FN {COST_FN}, FP {COST_FP}, threshold {THRESHOLD:.4f}; test year {TEST_YEAR} not loaded into any metric")
    print(f"reject everyone: {(1 - y_train.mean()) * COST_FP:.4f}  approve everyone: {y_train.mean() * COST_FN:.4f}")

    rows = []
    for name, pipeline in build_lc_candidates().items():
        started = time.perf_counter()
        oof = oof_predict(pipeline, X_train, y_train, skf)
        fold_auc = [roc_auc_score(y_train.iloc[v], oof[v]) for _, v in skf.split(X_train, y_train)]
        cm = confusion_matrix(y_train, (oof >= THRESHOLD).astype(int), labels=[0, 1])
        rows.append({
            "model": name,
            "roc_auc": np.mean(fold_auc),
            "roc_auc_std": np.std(fold_auc),
            "brier": brier_score_loss(y_train, oof),
            "log_loss": log_loss(y_train, oof),
            "mean_p_bad": oof.mean(),
            "expected_cost": expected_cost(cm, COST_FN, COST_FP),
            "approved_share": (oof < THRESHOLD).mean(),
            "p50": np.quantile(oof, 0.5),
            "p90": np.quantile(oof, 0.9),
            "p99": np.quantile(oof, 0.99),
            "p_max": oof.max(),
            "share_ge_0.20": (oof >= 0.20).mean(),
            "share_ge_0.25": (oof >= 0.25).mean(),
            "confusion": cm.tolist(),
            "seconds": round(time.perf_counter() - started),
        })
        print(f"done {name} in {rows[-1]['seconds']} s", flush=True)

    print()
    table = pd.DataFrame(rows).set_index("model").round(4)
    print(table.drop(columns=["confusion"]).T.to_string())
    print()
    print(table["confusion"].to_string())


if __name__ == "__main__":
    main()
