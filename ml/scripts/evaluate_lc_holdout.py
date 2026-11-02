import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import numpy as np
from sklearn.metrics import brier_score_loss, log_loss, roc_auc_score

from src.data.lending_club import COST_FN, COST_FP, TEST_YEAR, TRAIN_FROM, load_dataset, split_by_time
from src.evaluation.calibration import reliability_table
from src.evaluation.cost_analysis import review_band_table
from src.models.artifact import load_artifact

MODEL_PATH = Path(__file__).resolve().parents[1] / "models" / "credit_lc_xgb_isotonic_v1.joblib"


def main():
    model, metadata = load_artifact(MODEL_PATH)
    threshold = metadata["recommended_threshold"]["value"]
    review_from = metadata["recommended_review_band"]["from"]

    df = load_dataset()
    _, X_test, _, y_test = split_by_time(df, TEST_YEAR, TRAIN_FROM)
    p_bad = model.predict_proba(X_test)[:, 1]

    print(f"model {metadata['model_id']}, created {metadata['created_at']}")
    print(f"test year {TEST_YEAR}: {len(y_test)} loans, bad rate {y_test.mean():.4f}")
    print(f"threshold {threshold:.4f}, review from {review_from}")
    print(f"ROC-AUC {roc_auc_score(y_test, p_bad):.4f}  Brier {brier_score_loss(y_test, p_bad):.4f}  "
          f"log loss {log_loss(y_test, p_bad):.4f}  mean P(bad) {p_bad.mean():.4f}")
    print(f"P(bad) p50 {np.quantile(p_bad, 0.5):.4f}  p90 {np.quantile(p_bad, 0.9):.4f}  "
          f"p99 {np.quantile(p_bad, 0.99):.4f}  max {p_bad.max():.4f}")
    print(f"approve everyone: {y_test.mean() * COST_FN:.4f}  reject everyone: {(1 - y_test.mean()) * COST_FP:.4f}")
    print("\nreview bands:")
    bands = [(threshold, threshold), (review_from, threshold)]
    print(review_band_table(y_test, p_bad, bands, COST_FN, COST_FP).round(4).to_string(index=False))
    print("\nreliability (10 quantile bins):")
    print(reliability_table(y_test, p_bad).round(4).to_string(index=False))


if __name__ == "__main__":
    main()
