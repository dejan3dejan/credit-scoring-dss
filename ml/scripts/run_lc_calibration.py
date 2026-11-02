import sys
import time
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import numpy as np
import pandas as pd
from sklearn.metrics import brier_score_loss, log_loss, roc_auc_score
from sklearn.model_selection import StratifiedKFold

from src.config import RANDOM_STATE
from src.data.lending_club import COST_FN, COST_FP, TEST_YEAR, TRAIN_FROM, load_dataset, split_by_time
from src.evaluation.calibration import calibrated, reliability_table
from src.evaluation.cost_analysis import review_band_table, theoretical_threshold
from src.evaluation.cv_evaluate import oof_predict
from src.models.lending_club_pipelines import build_lc_candidates

THRESHOLD = theoretical_threshold(COST_FN, COST_FP)
LOWER_EDGES = (THRESHOLD, 0.25, 0.20, 0.15)


def main():
    df = load_dataset()
    X_train, _, y_train, _ = split_by_time(df, TEST_YEAR, TRAIN_FROM)
    skf = StratifiedKFold(n_splits=5, shuffle=True, random_state=RANDOM_STATE)
    bands = [(low, THRESHOLD) for low in LOWER_EDGES]

    print(f"train {TRAIN_FROM} to {TEST_YEAR - 1}: {X_train.shape}, bad rate {y_train.mean():.4f}")
    print(f"isotonic calibration, inner 5-fold, inside each of 5 outer folds; threshold {THRESHOLD:.4f}")
    print(f"test year {TEST_YEAR} not loaded into any metric")

    summary = []
    for name, pipeline in build_lc_candidates().items():
        started = time.perf_counter()
        oof = oof_predict(calibrated(pipeline, "isotonic"), X_train, y_train, skf)
        fold_auc = [roc_auc_score(y_train.iloc[v], oof[v]) for _, v in skf.split(X_train, y_train)]
        summary.append({
            "model": name,
            "roc_auc": np.mean(fold_auc),
            "roc_auc_std": np.std(fold_auc),
            "brier": brier_score_loss(y_train, oof),
            "log_loss": log_loss(y_train, oof),
            "mean_p_bad": oof.mean(),
            "p99": np.quantile(oof, 0.99),
            "p_max": oof.max(),
            "seconds": round(time.perf_counter() - started),
        })
        print(f"\n## {name} ({summary[-1]['seconds']} s)")
        print("review bands [low, threshold):")
        print(review_band_table(y_train, oof, bands, COST_FN, COST_FP).round(4).to_string(index=False))
        print("reliability (10 quantile bins):")
        print(reliability_table(y_train, oof).round(4).to_string(index=False))

    print("\n## summary")
    print(pd.DataFrame(summary).set_index("model").round(4).to_string())


if __name__ == "__main__":
    main()
