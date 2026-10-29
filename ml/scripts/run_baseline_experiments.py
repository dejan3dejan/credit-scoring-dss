import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

from src.data.load import load_dataset
from src.evaluation.cv_evaluate import METRIC_KEYS, cv_evaluate
from src.evaluation.splits import get_cv_and_holdout
from src.models.pipelines import build_candidates


def main():
    df = load_dataset()
    X_train, _, y_train, _, skf = get_cv_and_holdout(df)

    print(f"Train: {X_train.shape}, target rate: {y_train.mean():.3f}")
    print()

    for name, pipeline in build_candidates(y_train).items():
        cv_results = cv_evaluate(pipeline, X_train, y_train, skf)

        print(f"=== {name} ===")
        print("CV (5-fold, mean +/- std):")
        for metric in METRIC_KEYS:
            mean = cv_results["summary"][f"{metric}_mean"]
            std = cv_results["summary"][f"{metric}_std"]
            print(f"  {metric:14s} {mean:.3f} +/- {std:.3f}")
        print("CV aggregated confusion matrix [[TN, FP], [FN, TP]]:")
        print(" ", cv_results["confusion_matrix"].tolist())
        print()


if __name__ == "__main__":
    main()
