import pandas as pd
import pytest

from src.data.lending_club import (FEATURES, RAW_COLUMNS, RESULT_COLUMNS, add_target, emp_length_years, keep_term,
                                   load_dataset, matured, prepare, split_by_time, term_months)


def loans(**columns):
    base = {
        "issue_d": ["Dec-2015"],
        "term": [" 36 months"],
        "loan_status": ["Fully Paid"],
        "loan_amnt": [10000.0],
        "purpose": ["credit_card"],
        "emp_length": ["3 years"],
        "home_ownership": ["RENT"],
        "annual_inc": [55000.0],
        "verification_status": ["Verified"],
        "dti": [18.5],
        "fico_range_low": [700.0],
        "fico_range_high": [704.0],
        "earliest_cr_line": ["Dec-2005"],
        "inq_last_6mths": [1.0],
        "delinq_2yrs": [0.0],
        "pub_rec": [0.0],
        "pub_rec_bankruptcies": [0.0],
        "revol_bal": [8000.0],
        "revol_util": [40.0],
        "open_acc": [9.0],
        "total_acc": [20.0],
        "mort_acc": [1.0],
    }
    n = max(len(v) for v in columns.values()) if columns else 1
    data = {k: v * n if len(v) == 1 else v for k, v in base.items()}
    data.update(columns)
    return pd.DataFrame(data)


@pytest.mark.parametrize("status, target", [
    ("Fully Paid", 0),
    ("Does not meet the credit policy. Status:Fully Paid", 0),
    ("Charged Off", 1),
    ("Default", 1),
    ("Does not meet the credit policy. Status:Charged Off", 1),
])
def test_final_outcomes_map_to_the_target(status, target):
    out = add_target(loans(loan_status=[status]))
    assert out["target"].tolist() == [target]
    assert "loan_status" not in out.columns


@pytest.mark.parametrize("status", ["Current", "In Grace Period", "Late (16-30 days)", "Late (31-120 days)"])
def test_loans_without_a_final_outcome_are_dropped(status):
    assert add_target(loans(loan_status=[status, "Fully Paid"]))["target"].tolist() == [0]


def test_an_unknown_status_fails_instead_of_being_dropped():
    with pytest.raises(ValueError, match="Paid Early"):
        add_target(loans(loan_status=["Paid Early"]))


def test_term_is_read_as_months():
    assert term_months(pd.Series([" 36 months", "60 months"])).tolist() == [36, 60]


def test_only_loans_whose_term_ended_by_the_cutoff_are_kept():
    df = loans(
        issue_d=["Dec-2015", "Jan-2016", "Dec-2013", "Jan-2014"],
        term=[" 36 months", " 36 months", " 60 months", " 60 months"],
    )
    kept = matured(df, cutoff="2018-12")
    assert kept["issue_d"].tolist() == ["Dec-2015", "Dec-2013"]


@pytest.mark.parametrize("text, years", [
    ("< 1 year", 0.0), ("1 year", 1.0), ("6 years", 6.0), ("10+ years", 10.0),
])
def test_employment_length_becomes_years(text, years):
    assert emp_length_years(pd.Series([text])).tolist() == [years]


def test_missing_employment_length_stays_missing():
    assert emp_length_years(pd.Series([None])).isna().all()


def test_an_unknown_employment_length_fails():
    with pytest.raises(ValueError, match="about 5 years"):
        emp_length_years(pd.Series(["about 5 years"]))


def test_keep_term_keeps_only_loans_of_that_term():
    df = loans(term=[" 36 months", " 60 months", "36 months"])
    assert keep_term(df, 36)["term"].str.strip().tolist() == ["36 months", "36 months"]


def test_term_is_not_a_model_input():
    assert "term_months" not in FEATURES and "term" not in FEATURES


def test_prepare_derives_the_model_inputs():
    out = prepare(add_target(loans()))
    row = out.iloc[0]
    assert list(out.columns) == FEATURES + ["issue_year", "target"]
    assert row["emp_length_years"] == 3.0
    assert row["fico"] == 702.0
    assert row["credit_history_years"] == pytest.approx(10.0, abs=0.01)
    assert row["issue_year"] == 2015


def test_prepare_leaves_out_post_issue_and_lendingclub_fields():
    df = add_target(loans()).assign(grade="B", int_rate=11.5, total_pymnt=12000.0, addr_state="CA", emp_title="Teacher")
    out = prepare(df)
    assert not {"grade", "int_rate", "total_pymnt", "addr_state", "emp_title"} & set(out.columns)


def test_input_frame_is_not_modified():
    df = add_target(loans())
    before = df.copy()
    prepare(df)
    pd.testing.assert_frame_equal(df, before)


def test_split_by_time_tests_on_the_later_year_only():
    df = prepare(add_target(loans(issue_d=["Mar-2013", "Jun-2014", "Feb-2015", "Nov-2015"])))
    X_train, X_test, y_train, y_test = split_by_time(df, test_year=2015)
    assert sorted(df.loc[X_train.index, "issue_year"]) == [2013, 2014]
    assert sorted(df.loc[X_test.index, "issue_year"]) == [2015, 2015]
    assert list(X_train.columns) == FEATURES
    assert len(y_train) == 2 and len(y_test) == 2


def test_split_by_time_can_start_training_at_a_later_year():
    df = prepare(add_target(loans(issue_d=["Mar-2011", "Jun-2012", "Jan-2014", "Feb-2015"])))
    X_train, X_test, _, _ = split_by_time(df, test_year=2015, train_from=2012)
    assert sorted(df.loc[X_train.index, "issue_year"]) == [2012, 2014]
    assert sorted(df.loc[X_test.index, "issue_year"]) == [2015]


def test_split_by_time_refuses_a_year_with_no_loans():
    df = prepare(add_target(loans(issue_d=["Mar-2013"])))
    with pytest.raises(ValueError, match="2015"):
        split_by_time(df, test_year=2015)


def write_loans(path, total_pymnt=11200.0, **columns):
    loans(**columns).assign(funded_amnt=10000.0, total_pymnt=total_pymnt).to_csv(path, index=False)
    return path


def test_load_dataset_can_bring_each_loans_result(tmp_path):
    path = write_loans(tmp_path / "accepted.csv", total_pymnt=[11200.0, 3800.0],
                       loan_status=["Fully Paid", "Charged Off"], issue_d=["Mar-2014", "Feb-2015"])

    df = load_dataset(path, with_result=True)

    assert list(df["result"]) == [1200.0, -6200.0]
    assert list(df["target"]) == [0, 1]


def test_load_dataset_leaves_the_result_out_by_default(tmp_path):
    df = load_dataset(write_loans(tmp_path / "accepted.csv"))
    assert "result" not in df.columns


def test_the_result_never_reaches_the_model_inputs(tmp_path):
    df = load_dataset(write_loans(tmp_path / "accepted.csv", issue_d=["Mar-2014", "Feb-2015"]), with_result=True)
    X_train, X_test, _, _ = split_by_time(df, test_year=2015)
    assert "result" not in FEATURES and not set(RESULT_COLUMNS) & set(RAW_COLUMNS)
    assert list(X_train.columns) == FEATURES and list(X_test.columns) == FEATURES
