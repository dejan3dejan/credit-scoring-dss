import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import numpy as np
from sklearn.metrics import roc_auc_score

from src.data.lending_club import FEATURES, RAW_COLUMNS, TEST_YEAR, add_target, keep_term, load_raw, matured, prepare
from src.models.artifact import load_artifact

MODEL_PATH = Path(__file__).resolve().parents[1] / "models" / "credit_lc_xgb_isotonic_v1.joblib"
LENDINGCLUB_COLUMNS = ["sub_grade", "int_rate"]
REVIEW_SHARE = 0.1593


def riskiest(score, target, share):
    flagged = score >= np.quantile(score, 1 - share)
    return flagged.mean(), target[flagged].sum() / target.sum()


def main():
    model, metadata = load_artifact(MODEL_PATH)
    loans = keep_term(matured(add_target(load_raw(columns=RAW_COLUMNS + LENDINGCLUB_COLUMNS))))
    df = prepare(loans).join(loans[LENDINGCLUB_COLUMNS])
    test = df[df["issue_year"] == TEST_YEAR]
    target = test["target"].to_numpy()

    grade_rank = test["sub_grade"].map({g: i for i, g in enumerate(sorted(test["sub_grade"].unique()))})
    scores = {
        f"our model ({metadata['model_id']})": model.predict_proba(test[FEATURES])[:, 1],
        "FICO alone (lower = riskier)": -test["fico"].to_numpy(),
        "LendingClub sub-grade": grade_rank.to_numpy(),
        "LendingClub interest rate": test["int_rate"].to_numpy(),
    }
    print(f"test year {TEST_YEAR}: {len(test)} loans, bad rate {target.mean():.4f}")
    print(f"share of bad loans in the riskiest {REVIEW_SHARE:.1%} by each score (our review share)\n")
    for name, score in scores.items():
        auc = roc_auc_score(target, score)
        flagged, caught = riskiest(score, target, REVIEW_SHARE)
        print(f"{name:45s} ROC-AUC {auc:.4f}  Gini {2 * auc - 1:.4f}  flagged {flagged:.4f}  bad caught {caught:.4f}")


if __name__ == "__main__":
    main()
