from sklearn.model_selection import StratifiedKFold, train_test_split

from src.config import RANDOM_STATE


def get_cv_and_holdout(df, target_col="target", holdout_size=0.2, n_splits=5):
    X = df.drop(columns=[target_col])
    y = df[target_col]

    X_train, X_test, y_train, y_test = train_test_split(
        X, y,
        test_size=holdout_size,
        stratify=y,
        random_state=RANDOM_STATE,
    )

    skf = StratifiedKFold(n_splits=n_splits, shuffle=True, random_state=RANDOM_STATE)

    return X_train, X_test, y_train, y_test, skf
