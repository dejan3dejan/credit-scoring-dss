import hashlib
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import sklearn
import xgboost

from src.config import RANDOM_STATE
from src.data.load import RAW_PATH, load_dataset
from src.evaluation.calibration import calibrated
from src.evaluation.cost_analysis import theoretical_threshold
from src.evaluation.cv_evaluate import COST_FN, COST_FP
from src.evaluation.splits import get_cv_and_holdout
from src.models.artifact import build_metadata, save_artifact
from src.models.pipelines import random_forest_pipeline

MODEL_ID = "credit_rf_isotonic_v1"
OUT_DIR = Path(__file__).resolve().parents[1] / "models"


def main():
    df = load_dataset()
    X_train, _, y_train, _, _ = get_cv_and_holdout(df)

    model = calibrated(random_forest_pipeline(), "isotonic")
    model.fit(X_train, y_train)

    metadata = build_metadata(
        model_id=MODEL_ID,
        model_name="RandomForest",
        calibration="isotonic (CalibratedClassifierCV, inner 5-fold)",
        threshold=theoretical_threshold(COST_FN, COST_FP),
        cost_fn=COST_FN,
        cost_fp=COST_FP,
        seed=RANDOM_STATE,
        X_train=X_train,
        y_train=y_train,
        data_sha256=hashlib.sha256(RAW_PATH.read_bytes()).hexdigest(),
        created_at=datetime.now(timezone.utc).isoformat(timespec="seconds"),
        library_versions={"scikit-learn": sklearn.__version__, "xgboost": xgboost.__version__,
                          "python": sys.version.split()[0]},
        evaluation_ref="experiments/experiment_log.md, Experiment 04, final test-set result",
    )
    model_path, meta_path = save_artifact(model, metadata, OUT_DIR)
    print(f"saved {model_path}")
    print(f"saved {meta_path}")


if __name__ == "__main__":
    main()
