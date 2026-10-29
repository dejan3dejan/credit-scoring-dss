import json
from pathlib import Path

import joblib
import sklearn

FEATURE_ORDER_NOTE = "raw German Credit columns, names as in src/data/load.py"


def build_metadata(*, model_id, model_name, calibration, threshold, cost_fn, cost_fp,
                   seed, X_train, y_train, data_sha256, created_at, library_versions,
                   evaluation_ref):
    categorical = X_train.select_dtypes(exclude="number").columns
    return {
        "model_id": model_id,
        "model_name": model_name,
        "calibration": calibration,
        "output": "P(bad), probability of default, class 1",
        "recommended_threshold": {
            "value": threshold,
            "rule": "reject iff P(bad) >= value",
            "applied_by": "backend ICreditDecisionService, not the inference service",
            "derivation": f"cost_fp / (cost_fp + cost_fn), cost_fn={cost_fn}, cost_fp={cost_fp}",
        },
        "training": {
            "seed": seed,
            "rows": int(len(y_train)),
            "bad_rate": float(y_train.mean()),
            "split": "stratified 80% training portion; the 20% holdout was not used for fitting",
            "data_sha256": data_sha256,
        },
        "features": {
            "order": list(X_train.columns),
            "note": FEATURE_ORDER_NOTE,
            "categorical_levels_seen_in_training": {
                c: sorted(X_train[c].unique().tolist()) for c in categorical
            },
        },
        "library_versions": library_versions,
        "evaluation_ref": evaluation_ref,
        "created_at": created_at,
    }


def save_artifact(model, metadata, out_dir):
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    model_path = out_dir / f"{metadata['model_id']}.joblib"
    meta_path = out_dir / f"{metadata['model_id']}.json"
    joblib.dump(model, model_path)
    meta_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    return model_path, meta_path


def load_artifact(model_path):
    model_path = Path(model_path)
    metadata = json.loads(model_path.with_suffix(".json").read_text(encoding="utf-8"))
    saved = metadata["library_versions"]["scikit-learn"]
    if saved != sklearn.__version__:
        raise RuntimeError(
            f"{model_path.name} was saved with scikit-learn {saved}, "
            f"running {sklearn.__version__}; refusing to load"
        )
    return joblib.load(model_path), metadata
