import hashlib
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import sklearn
import xgboost

from src.config import RANDOM_STATE
from src.data.lending_club import ACCEPTED_PATH, COST_FN, COST_FP, TEST_YEAR, TRAIN_FROM, load_dataset, split_by_time
from src.evaluation.calibration import calibrated
from src.evaluation.cost_analysis import theoretical_threshold
from src.models.artifact import build_metadata, save_artifact
from src.models.lending_club_pipelines import xgboost_pipeline

MODEL_ID = "credit_lc_xgb_isotonic_v1"
REVIEW_FROM = 0.20
OUT_DIR = Path(__file__).resolve().parents[1] / "models"


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def main():
    df = load_dataset()
    X_train, _, y_train, _ = split_by_time(df, TEST_YEAR, TRAIN_FROM)

    model = calibrated(xgboost_pipeline(), "isotonic")
    model.fit(X_train, y_train)

    metadata = build_metadata(
        model_id=MODEL_ID,
        model_name="XGBoost",
        calibration="isotonic (CalibratedClassifierCV, inner 5-fold)",
        threshold=theoretical_threshold(COST_FN, COST_FP),
        cost_fn=COST_FN,
        cost_fp=COST_FP,
        seed=RANDOM_STATE,
        X_train=X_train,
        y_train=y_train,
        data_sha256=sha256(ACCEPTED_PATH),
        created_at=datetime.now(timezone.utc).isoformat(timespec="seconds"),
        library_versions={"scikit-learn": sklearn.__version__, "xgboost": xgboost.__version__,
                          "python": sys.version.split()[0]},
        evaluation_ref=f"36-month loans; fitted on {TRAIN_FROM} to {TEST_YEAR - 1}, scored once on {TEST_YEAR}",
        dataset="lending_club",
        review_from=REVIEW_FROM,
    )
    metadata["training"]["split"] = f"36-month loans issued {TRAIN_FROM} to {TEST_YEAR - 1}; {TEST_YEAR} held out"
    model_path, meta_path = save_artifact(model, metadata, OUT_DIR)
    print(f"saved {model_path} ({model_path.stat().st_size} bytes)")
    print(f"saved {meta_path}")


if __name__ == "__main__":
    main()
