import numpy as np
import pandas as pd
import pytest

from src.data.lending_club import FEATURES
from src.features.lending_club_preprocessing import Winsorizer, WoeEncoder, build_lc_preprocessing
from src.models.lending_club_pipelines import build_lc_candidates


@pytest.fixture
def synthetic_loans():
    rng = np.random.default_rng(0)
    n = 2000
    df = pd.DataFrame({
        "loan_amnt": rng.integers(1000, 35001, n).astype(float),
        "purpose": rng.choice(["debt_consolidation", "credit_card", "other", "car"], n, p=[0.6, 0.3, 0.09, 0.01]),
        "emp_length_years": rng.choice([0.0, 2.0, 5.0, 10.0, np.nan], n),
        "home_ownership": rng.choice(["MORTGAGE", "RENT", "OWN", "NONE"], n, p=[0.5, 0.4, 0.099, 0.001]),
        "annual_inc": rng.lognormal(11, 0.5, n),
        "verification_status": rng.choice(["Verified", "Not Verified", "Source Verified"], n),
        "dti": rng.uniform(0, 40, n),
        "fico": rng.uniform(660, 850, n),
        "credit_history_years": rng.uniform(1, 40, n),
        "inq_last_6mths": rng.integers(0, 6, n).astype(float),
        "delinq_2yrs": rng.integers(0, 3, n).astype(float),
        "pub_rec": rng.integers(0, 2, n).astype(float),
        "pub_rec_bankruptcies": rng.integers(0, 2, n).astype(float),
        "revol_bal": rng.lognormal(9, 1, n),
        "revol_util": rng.uniform(0, 100, n),
        "open_acc": rng.integers(1, 30, n).astype(float),
        "total_acc": rng.integers(2, 60, n).astype(float),
        "mort_acc": rng.integers(0, 5, n).astype(float),
    })[FEATURES]
    risk = -3 + 0.08 * df["dti"] - 0.01 * (df["fico"] - 700)
    y = pd.Series((rng.random(n) < 1 / (1 + np.exp(-risk))).astype(int))
    return df, y


def test_winsorizer_clips_to_the_quantiles_of_the_data_it_was_fitted_on():
    fit = pd.DataFrame({"x": np.arange(1, 1001, dtype=float)})
    w = Winsorizer(lower=0.01, upper=0.99).fit(fit)
    out = w.transform(pd.DataFrame({"x": [-5.0, 500.0, 10_000.0]}))
    low, high = np.quantile(fit["x"], [0.01, 0.99])
    assert out["x"].tolist() == [low, 500.0, high]


def test_winsorizer_keeps_missing_values_missing():
    w = Winsorizer().fit(pd.DataFrame({"x": [1.0, 2.0, np.nan, 4.0]}))
    assert w.transform(pd.DataFrame({"x": [np.nan]}))["x"].isna().all()


def test_winsorizer_does_not_modify_its_input():
    df = pd.DataFrame({"x": [1.0, 100.0]})
    Winsorizer(lower=0.0, upper=0.5).fit(df).transform(df)
    assert df["x"].tolist() == [1.0, 100.0]


def test_woe_gives_the_riskier_bin_the_lower_value():
    X = pd.DataFrame({"x": [1.0] * 100 + [9.0] * 100})
    y = pd.Series([0] * 90 + [1] * 10 + [0] * 50 + [1] * 50)
    out = WoeEncoder(n_bins=2).fit(X, y).transform(pd.DataFrame({"x": [1.0, 9.0]}))
    assert out["x"].iloc[0] > 0 > out["x"].iloc[1]


def test_woe_treats_missing_as_its_own_bin():
    X = pd.DataFrame({"x": [1.0] * 100 + [np.nan] * 100})
    y = pd.Series([0] * 90 + [1] * 10 + [0] * 40 + [1] * 60)
    out = WoeEncoder(n_bins=2).fit(X, y).transform(pd.DataFrame({"x": [np.nan, 1.0]}))
    assert out["x"].iloc[0] < 0 < out["x"].iloc[1]


def test_woe_gives_an_unseen_category_a_neutral_value():
    X = pd.DataFrame({"c": ["a"] * 50 + ["b"] * 50})
    y = pd.Series([0] * 45 + [1] * 5 + [0] * 25 + [1] * 25)
    out = WoeEncoder().fit(X, y).transform(pd.DataFrame({"c": ["zzz"]}))
    assert out["c"].iloc[0] == 0.0


def test_preprocessing_output_is_complete_and_finite(synthetic_loans):
    X, _ = synthetic_loans
    out = build_lc_preprocessing().fit_transform(X)
    assert np.isfinite(out).all()


def test_preprocessing_caps_are_learned_from_training_data_only(synthetic_loans):
    X, _ = synthetic_loans
    pre = build_lc_preprocessing().fit(X)
    extreme = X.iloc[[0]].assign(annual_inc=1e9)
    capped = X.iloc[[0]].assign(annual_inc=X["annual_inc"].quantile(0.995))
    assert np.allclose(pre.transform(extreme), pre.transform(capped))


def test_preprocessing_accepts_an_unseen_category(synthetic_loans):
    X, _ = synthetic_loans
    pre = build_lc_preprocessing().fit(X)
    assert np.isfinite(pre.transform(X.iloc[[0]].assign(purpose="space_travel"))).all()


@pytest.mark.parametrize("name", ["Scorecard", "LogReg", "RandomForest", "XGBoost"])
def test_every_candidate_fits_and_returns_probabilities(synthetic_loans, name):
    X, y = synthetic_loans
    model = build_lc_candidates()[name].fit(X, y)
    p = model.predict_proba(X.iloc[:50])[:, 1]
    assert ((p >= 0) & (p <= 1)).all()
