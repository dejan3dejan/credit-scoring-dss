import numpy as np
from sklearn.model_selection import StratifiedKFold

from src.config import RANDOM_STATE
from src.features.preprocessing import LOG_SCALE_COLUMNS, build_preprocessing_pipeline


def test_preprocessing_uses_train_fold_only(synthetic_applicants):
    X, y = synthetic_applicants
    skf = StratifiedKFold(n_splits=5, shuffle=True, random_state=RANDOM_STATE)
    train_idx, test_idx = next(skf.split(X, y))
    X_train, X_test = X.iloc[train_idx], X.iloc[test_idx]

    pipeline = build_preprocessing_pipeline()
    pipeline.fit(X_train)
    fitted_scaler = pipeline.named_transformers_["log_scale"].named_steps["scale"]

    train_log = np.log1p(X_train[LOG_SCALE_COLUMNS])
    assert np.allclose(fitted_scaler.mean_, train_log.mean().values)
    assert np.allclose(fitted_scaler.scale_, train_log.std(ddof=0).values)

    test_log_mean = np.log1p(X_test[LOG_SCALE_COLUMNS]).mean().values
    assert not np.allclose(fitted_scaler.mean_, test_log_mean)

    assert pipeline.transform(X_test).shape[0] == len(X_test)
