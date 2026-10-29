import numpy as np
from sklearn.base import clone
from sklearn.metrics import (
    confusion_matrix,
    f1_score,
    precision_score,
    recall_score,
    roc_auc_score,
)

METRIC_KEYS = ["roc_auc", "precision", "recall", "f1", "expected_cost"]

COST_FN = 5
COST_FP = 1


def expected_cost(cm, cost_fn=COST_FN, cost_fp=COST_FP):
    (_, fp), (fn, _) = cm
    return (cost_fn * fn + cost_fp * fp) / cm.sum()


def oof_predict(pipeline, X_train, y_train, skf):
    oof = np.full(len(y_train), np.nan)
    for train_idx, val_idx in skf.split(X_train, y_train):
        model = clone(pipeline)
        model.fit(X_train.iloc[train_idx], y_train.iloc[train_idx])
        oof[val_idx] = model.predict_proba(X_train.iloc[val_idx])[:, 1]
    return oof


def cv_evaluate(pipeline, X_train, y_train, skf, threshold=0.5):
    fold_metrics = []
    agg_cm = np.zeros((2, 2), dtype=int)

    for fold_idx, (train_idx, val_idx) in enumerate(skf.split(X_train, y_train), start=1):
        X_tr, X_val = X_train.iloc[train_idx], X_train.iloc[val_idx]
        y_tr, y_val = y_train.iloc[train_idx], y_train.iloc[val_idx]

        model = clone(pipeline)
        model.fit(X_tr, y_tr)

        y_proba = model.predict_proba(X_val)[:, 1]
        y_pred = (y_proba >= threshold).astype(int)

        cm = confusion_matrix(y_val, y_pred, labels=[0, 1])
        fold_metrics.append({
            "fold": fold_idx,
            "roc_auc": roc_auc_score(y_val, y_proba),
            "precision": precision_score(y_val, y_pred, zero_division=0),
            "recall": recall_score(y_val, y_pred, zero_division=0),
            "f1": f1_score(y_val, y_pred, zero_division=0),
            "expected_cost": expected_cost(cm),
        })
        agg_cm += cm

    summary = {}
    for key in METRIC_KEYS:
        vals = [m[key] for m in fold_metrics]
        summary[f"{key}_mean"] = float(np.mean(vals))
        summary[f"{key}_std"] = float(np.std(vals))

    return {
        "fold_metrics": fold_metrics,
        "summary": summary,
        "confusion_matrix": agg_cm,
    }


def evaluate_holdout(pipeline, X_train, y_train, X_test, y_test, threshold=0.5):
    model = clone(pipeline)
    model.fit(X_train, y_train)

    y_proba = model.predict_proba(X_test)[:, 1]
    y_pred = (y_proba >= threshold).astype(int)
    cm = confusion_matrix(y_test, y_pred, labels=[0, 1])

    return {
        "roc_auc": roc_auc_score(y_test, y_proba),
        "precision": precision_score(y_test, y_pred, zero_division=0),
        "recall": recall_score(y_test, y_pred, zero_division=0),
        "f1": f1_score(y_test, y_pred, zero_division=0),
        "expected_cost": expected_cost(cm),
        "confusion_matrix": cm,
    }
