import numpy as np
import pandas as pd
import pytest

from src.data.demo_cases import band_of, fits_application, pick_cases, to_answers

THRESHOLD = 0.3286
REVIEW_FROM = 0.20


def loan(**changes):
    row = {
        "loan_amnt": 12000.0,
        "purpose": "debt_consolidation",
        "emp_length_years": 4.0,
        "home_ownership": "MORTGAGE",
        "annual_inc": 65000.0,
        "verification_status": "Source Verified",
        "dti": 17.25,
        "fico": 692.0,
        "credit_history_years": 12.0,
        "inq_last_6mths": 1.0,
        "delinq_2yrs": 0.0,
        "pub_rec": 0.0,
        "pub_rec_bankruptcies": 0.0,
        "revol_bal": 8400.0,
        "revol_util": 54.3,
        "open_acc": 9.0,
        "total_acc": 22.0,
        "mort_acc": 2.0,
    }
    row.update(changes)
    return pd.Series(row)


def test_answers_use_the_api_names_and_enum_values():
    answers = to_answers(loan())

    assert answers == {
        "loanAmount": 12000,
        "purpose": "DebtConsolidation",
        "employmentLength": "FourYears",
        "homeOwnership": "Mortgage",
        "annualIncome": 65000.0,
        "incomeVerification": "SourceVerified",
        "debtToIncomePercent": 17.25,
        "creditScore": 692,
        "creditHistoryMonths": 144,
        "inquiriesLast6Months": 1,
        "delinquenciesLast2Years": 0,
        "publicRecords": 0,
        "bankruptcies": 0,
        "revolvingBalance": 8400.0,
        "revolvingUtilizationPercent": 54.3,
        "openAccounts": 9,
        "totalAccounts": 22,
        "mortgageAccounts": 2,
    }


@pytest.mark.parametrize("years, expected", [(np.nan, "NotStated"), (0.0, "LessThanOneYear"), (1.0, "OneYear"), (10.0, "TenOrMoreYears")])
def test_employment_length_maps_every_end_of_the_scale(years, expected):
    assert to_answers(loan(emp_length_years=years))["employmentLength"] == expected


def test_an_unknown_category_fails_loudly():
    with pytest.raises(ValueError):
        to_answers(loan(purpose="educational"))


@pytest.mark.parametrize("changes", [
    {"purpose": "educational"},
    {"home_ownership": "ANY"},
    {"loan_amnt": 35025.0},
    {"dti": np.nan},
    {"dti": 120.0},
    {"revol_util": np.nan},
    {"pub_rec_bankruptcies": 2.0, "pub_rec": 1.0},
    {"open_acc": 30.0, "total_acc": 20.0},
])
def test_loans_the_application_form_cannot_take_are_left_out(changes):
    assert not fits_application(loan(**changes))


def test_a_typical_loan_fits_the_application_form():
    assert fits_application(loan())


@pytest.mark.parametrize("p_bad, expected", [(0.05, "Approve"), (0.20, "Review"), (0.3285, "Review"), (0.3286, "Reject")])
def test_band_follows_the_served_edges(p_bad, expected):
    assert band_of(p_bad, THRESHOLD, REVIEW_FROM) == expected


def test_pick_draws_a_random_sample_and_adds_one_reject_when_none_was_drawn():
    loans = pd.DataFrame([loan(loan_amnt=float(1000 + i)) for i in range(200)])
    p_bad = pd.Series([0.1] * 150 + [0.25] * 49 + [0.4], index=loans.index)

    picked = pick_cases(loans, p_bad, THRESHOLD, REVIEW_FROM, size=20, seed=1)

    assert len(picked) == 20
    assert (picked["band"] == "Reject").sum() == 1
    assert picked.index.is_unique
    assert picked.loc[199, "band"] == "Reject"


def test_pick_is_repeatable_for_a_seed():
    loans = pd.DataFrame([loan(loan_amnt=float(1000 + i)) for i in range(100)])
    p_bad = pd.Series(np.linspace(0.05, 0.4, 100), index=loans.index)

    first = pick_cases(loans, p_bad, THRESHOLD, REVIEW_FROM, size=10, seed=7)
    second = pick_cases(loans, p_bad, THRESHOLD, REVIEW_FROM, size=10, seed=7)

    assert first.index.tolist() == second.index.tolist()


def test_pick_skips_loans_the_form_cannot_take():
    loans = pd.DataFrame([loan(), loan(purpose="educational"), loan(), loan(dti=np.nan)])
    p_bad = pd.Series([0.1, 0.1, 0.4, 0.1], index=loans.index)

    picked = pick_cases(loans, p_bad, THRESHOLD, REVIEW_FROM, size=2, seed=0)

    assert set(picked.index) == {0, 2}
