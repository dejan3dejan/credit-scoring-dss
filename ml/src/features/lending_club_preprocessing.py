import numpy as np
import pandas as pd
from sklearn.base import BaseEstimator, OneToOneFeatureMixin, TransformerMixin
from sklearn.compose import ColumnTransformer
from sklearn.impute import SimpleImputer
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import FunctionTransformer, OneHotEncoder, StandardScaler

LOG_COLUMNS = [
    "annual_inc",
    "revol_bal",
]

NUMERIC_COLUMNS = [
    "loan_amnt",
    "emp_length_years",
    "dti",
    "fico",
    "credit_history_years",
    "inq_last_6mths",
    "delinq_2yrs",
    "pub_rec",
    "pub_rec_bankruptcies",
    "revol_util",
    "open_acc",
    "total_acc",
    "mort_acc",
]

CATEGORICAL_COLUMNS = [
    "purpose",
    "home_ownership",
    "verification_status",
]

CAP_LOWER = 0.005
CAP_UPPER = 0.995
MIN_CATEGORY_SHARE = 0.001


class Winsorizer(OneToOneFeatureMixin, TransformerMixin, BaseEstimator):
    def __init__(self, lower=CAP_LOWER, upper=CAP_UPPER):
        self.lower = lower
        self.upper = upper

    def fit(self, X, y=None):
        values = np.asarray(X, dtype=float)
        self.n_features_in_ = values.shape[1]
        if isinstance(X, pd.DataFrame):
            self.feature_names_in_ = np.asarray(X.columns, dtype=object)
        self.low_, self.high_ = np.nanquantile(values, [self.lower, self.upper], axis=0)
        return self

    def transform(self, X):
        clipped = np.clip(np.asarray(X, dtype=float), self.low_, self.high_)
        if isinstance(X, pd.DataFrame):
            return pd.DataFrame(clipped, columns=X.columns, index=X.index)
        return clipped


class WoeEncoder(OneToOneFeatureMixin, TransformerMixin, BaseEstimator):
    def __init__(self, n_bins=10, smoothing=0.5):
        self.n_bins = n_bins
        self.smoothing = smoothing

    def fit(self, X, y):
        X = pd.DataFrame(X)
        y = np.asarray(y)
        self.feature_names_in_ = np.asarray(X.columns, dtype=object)
        self.n_features_in_ = X.shape[1]
        self.edges_, self.woe_ = {}, {}
        for column in X.columns:
            if pd.api.types.is_numeric_dtype(X[column]):
                quantiles = np.linspace(0, 1, self.n_bins + 1)[1:-1]
                self.edges_[column] = np.unique(np.nanquantile(X[column].to_numpy(dtype=float), quantiles))
            bins = self._bins(X[column], column)
            self.woe_[column] = self._woe(bins, y)
        return self

    def transform(self, X):
        X = pd.DataFrame(X)
        return pd.DataFrame(
            {c: self._bins(X[c], c).map(self.woe_[c]).fillna(0.0).astype(float) for c in X.columns},
            index=X.index,
        )

    def _bins(self, values, column):
        if column not in self.edges_:
            return values.astype(object).where(values.notna(), "<missing>")
        numbers = values.to_numpy(dtype=float)
        bins = np.searchsorted(self.edges_[column], numbers, side="right").astype(object)
        bins[np.isnan(numbers)] = "<missing>"
        return pd.Series(bins, index=values.index)

    def _woe(self, bins, y):
        counts = pd.crosstab(bins.to_numpy(), y)
        good = counts.get(0, 0) + self.smoothing
        bad = counts.get(1, 0) + self.smoothing
        return dict(np.log((good / good.sum()) / (bad / bad.sum())))


def build_lc_preprocessing():
    log_pipe = Pipeline([
        ("cap", Winsorizer()),
        ("log1p", FunctionTransformer(np.log1p, feature_names_out="one-to-one")),
        ("impute", SimpleImputer(strategy="median", add_indicator=True)),
        ("scale", StandardScaler()),
    ])
    numeric_pipe = Pipeline([
        ("cap", Winsorizer()),
        ("impute", SimpleImputer(strategy="median", add_indicator=True)),
        ("scale", StandardScaler()),
    ])
    return ColumnTransformer([
        ("log", log_pipe, LOG_COLUMNS),
        ("numeric", numeric_pipe, NUMERIC_COLUMNS),
        ("categorical", OneHotEncoder(
            handle_unknown="infrequent_if_exist",
            min_frequency=MIN_CATEGORY_SHARE,
            sparse_output=False,
        ), CATEGORICAL_COLUMNS),
    ])


def build_woe_preprocessing():
    return WoeEncoder()
