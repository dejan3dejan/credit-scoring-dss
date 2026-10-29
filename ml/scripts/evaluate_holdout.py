import argparse
import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

from src.data.load import load_dataset
from src.evaluation.calibration import calibrated
from src.evaluation.cv_evaluate import METRIC_KEYS, evaluate_holdout
from src.evaluation.splits import get_cv_and_holdout
from src.models.pipelines import build_candidates


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True, choices=["LogReg", "RandomForest", "XGBoost"])
    parser.add_argument("--threshold", type=float, default=0.5,
                        help="reject iff P(bad) >= threshold; chosen on OOF predictions beforehand")
    parser.add_argument("--calibration", choices=["none", "sigmoid", "isotonic"], default="none")
    args = parser.parse_args()

    df = load_dataset()
    X_train, X_test, y_train, y_test, _ = get_cv_and_holdout(df)
    pipeline = build_candidates(y_train)[args.model]
    if args.calibration != "none":
        pipeline = calibrated(pipeline, args.calibration)

    results = evaluate_holdout(pipeline, X_train, y_train, X_test, y_test, threshold=args.threshold)

    print(f"=== {args.model} (calibration={args.calibration}, threshold={args.threshold}): "
          f"holdout (n={len(y_test)}) ===")
    for metric in METRIC_KEYS:
        print(f"  {metric:14s} {results[metric]:.3f}")
    print("Confusion matrix [[TN, FP], [FN, TP]]:")
    print(" ", results["confusion_matrix"].tolist())


if __name__ == "__main__":
    main()
