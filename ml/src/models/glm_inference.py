import numpy as np
import pandas as pd
import statsmodels.api as sm
from statsmodels.stats.outliers_influence import variance_inflation_factor

from src.features.preprocessing import NOMINAL_COLUMNS, ORDINAL_COLUMNS, build_preprocessing_pipeline


def design_matrix(X_train):
    pre = build_preprocessing_pipeline(drop_first=True)
    Z = pre.fit_transform(X_train)
    return pd.DataFrame(Z, columns=pre.get_feature_names_out(), index=X_train.index)


def separation_check(X_train, y_train):
    rows = []
    for col in NOMINAL_COLUMNS + ORDINAL_COLUMNS:
        stats = y_train.groupby(X_train[col]).agg(["size", "sum"])
        for level, (n, n_bad) in stats.iterrows():
            if n_bad == 0 or n_bad == n:
                rows.append({"column": col, "level": level, "n": int(n), "n_bad": int(n_bad)})
    return pd.DataFrame(rows, columns=["column", "level", "n", "n_bad"])


def vif_table(Z):
    Zc = sm.add_constant(Z)
    return pd.DataFrame({
        "feature": Z.columns,
        "vif": [variance_inflation_factor(Zc.values, i + 1) for i in range(Z.shape[1])],
    }).sort_values("vif", ascending=False, ignore_index=True)


def fit_glm(X_train, y_train):
    Z = design_matrix(X_train)
    model = sm.GLM(y_train.to_numpy(), sm.add_constant(Z), family=sm.families.Binomial())
    return model.fit(), Z


def coefficient_table(result, alpha=0.05):
    ci = result.conf_int(alpha=alpha)
    return pd.DataFrame({
        "coef": result.params,
        "std_err": result.bse,
        "odds_ratio": np.exp(result.params),
        "or_ci_low": np.exp(ci[0]),
        "or_ci_high": np.exp(ci[1]),
        "p_value": result.pvalues,
    })
