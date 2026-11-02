import numpy as np
import pandas as pd
import pytest
from sklearn.calibration import CalibratedClassifierCV

from src.data.lending_club import FEATURES
from src.models.explain import contributions, explainable, fold_contributions, input_of, top_factors
from src.models.lending_club_pipelines import logistic_regression_pipeline, xgboost_pipeline


@pytest.fixture(scope="module")
def loans():
    rng = np.random.default_rng(0)
    n = 1500
    df = pd.DataFrame({
        "loan_amnt": rng.integers(1000, 35001, n).astype(float),
        "purpose": rng.choice(["debt_consolidation", "credit_card", "small_business", "car"], n, p=[0.5, 0.3, 0.15, 0.05]),
        "emp_length_years": rng.choice([0.0, 2.0, 5.0, 10.0, np.nan], n),
        "home_ownership": rng.choice(["MORTGAGE", "RENT", "OWN"], n),
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
        "total_acc": rng.integers(30, 60, n).astype(float),
        "mort_acc": rng.choice([0.0, 1.0, 3.0, np.nan], n),
    })[FEATURES]
    risk = -2 + 0.06 * df["dti"] - 0.02 * (df["fico"] - 700) + 1.0 * (df["purpose"] == "small_business")
    y = pd.Series((rng.random(n) < 1 / (1 + np.exp(-risk))).astype(int))
    return df, y


@pytest.fixture(scope="module")
def calibrated_xgb(loans):
    X, y = loans
    return CalibratedClassifierCV(xgboost_pipeline(), method="isotonic", cv=3).fit(X, y)


@pytest.mark.parametrize("column, expected", [
    ("log__annual_inc", "annual_inc"),
    ("numeric__fico", "fico"),
    ("numeric__missingindicator_emp_length_years", "emp_length_years"),
    ("categorical__purpose_small_business", "purpose"),
    ("categorical__home_ownership_infrequent_sklearn", "home_ownership"),
    ("categorical__verification_status_Not Verified", "verification_status"),
    ("numeric__pub_rec", "pub_rec"),
    ("numeric__pub_rec_bankruptcies", "pub_rec_bankruptcies"),
])
def test_every_model_column_belongs_to_one_input(column, expected):
    assert input_of(column, FEATURES) == expected


def test_a_column_of_no_known_input_raises():
    with pytest.raises(ValueError, match="int_rate"):
        input_of("numeric__int_rate", FEATURES)


def test_each_fold_adds_up_to_its_raw_score(calibrated_xgb, loans):
    X = loans[0].iloc[:50]
    for fold in calibrated_xgb.calibrated_classifiers_:
        per_input, bias = fold_contributions(fold.estimator, X, FEATURES)
        margin = fold.estimator[-1].predict(fold.estimator[:-1].transform(X), output_margin=True)

        assert list(per_input.columns) == FEATURES
        np.testing.assert_allclose(per_input.sum(axis=1) + bias, margin, rtol=1e-5, atol=1e-5)


def test_contributions_are_the_mean_over_folds(calibrated_xgb, loans):
    X = loans[0].iloc[:20]
    folds = [fold_contributions(f.estimator, X, FEATURES)[0] for f in calibrated_xgb.calibrated_classifiers_]

    pd.testing.assert_frame_equal(contributions(calibrated_xgb, X, FEATURES), sum(folds) / len(folds))


def test_the_input_that_drives_risk_is_found(calibrated_xgb, loans):
    X = loans[0]
    mean_size = contributions(calibrated_xgb, X, FEATURES).abs().mean()

    assert set(mean_size.nlargest(3).index) >= {"fico", "dti"}


def test_top_factors_are_ordered_by_size_and_carry_the_entered_value(calibrated_xgb, loans):
    X = loans[0].iloc[[0]].copy()
    X["emp_length_years"] = np.nan

    factors = top_factors(calibrated_xgb, X, FEATURES, n=5)
    row = contributions(calibrated_xgb, X, FEATURES).iloc[0]

    assert len(factors) == 5
    assert [abs(f["contribution"]) for f in factors] == sorted((abs(f["contribution"]) for f in factors), reverse=True)
    assert [f["feature"] for f in factors] == list(row.abs().nlargest(5).index)
    for f in factors:
        assert f["contribution"] == pytest.approx(row[f["feature"]])
        expected = X.iloc[0][f["feature"]]
        assert f["value"] == (None if pd.isna(expected) else expected)
    assert all(isinstance(f["contribution"], float) for f in factors)


def test_missing_value_is_reported_as_none(calibrated_xgb, loans):
    X = loans[0].iloc[[0]].copy()
    X["emp_length_years"] = np.nan

    factors = top_factors(calibrated_xgb, X, FEATURES, n=len(FEATURES))

    assert next(f for f in factors if f["feature"] == "emp_length_years")["value"] is None


def test_only_calibrated_xgboost_pipelines_are_explainable(calibrated_xgb, loans):
    X, y = loans
    logistic = CalibratedClassifierCV(logistic_regression_pipeline(), method="isotonic", cv=3).fit(X, y)

    assert explainable(calibrated_xgb)
    assert not explainable(logistic)
    assert not explainable(xgboost_pipeline())
    with pytest.raises(TypeError):
        top_factors(logistic, X.iloc[[0]], FEATURES)


def test_top_factors_take_a_single_row(calibrated_xgb, loans):
    with pytest.raises(ValueError, match="one row"):
        top_factors(calibrated_xgb, loans[0].iloc[:2], FEATURES)
