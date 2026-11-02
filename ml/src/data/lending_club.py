from pathlib import Path

import pandas as pd

RAW_DIR = Path(__file__).resolve().parents[2] / "data" / "raw" / "lending_club"
ACCEPTED_PATH = RAW_DIR / "accepted_2007_to_2018Q4.csv"
REJECTED_PATH = RAW_DIR / "rejected_2007_to_2018Q4.csv"

DATA_CUTOFF = "2018-12"
TERM_MONTHS = 36
TRAIN_FROM = 2012
TEST_YEAR = 2015
COST_FN = 2.0431
COST_FP = 1
DATE_FORMAT = "%b-%Y"

BAD_STATUSES = {
    "Charged Off",
    "Default",
    "Does not meet the credit policy. Status:Charged Off",
}
GOOD_STATUSES = {
    "Fully Paid",
    "Does not meet the credit policy. Status:Fully Paid",
}
OPEN_STATUSES = {
    "Current",
    "In Grace Period",
    "Late (16-30 days)",
    "Late (31-120 days)",
}

RAW_INPUTS = [
    "loan_amnt",
    "term",
    "purpose",
    "emp_length",
    "home_ownership",
    "annual_inc",
    "verification_status",
    "dti",
    "fico_range_low",
    "fico_range_high",
    "earliest_cr_line",
    "inq_last_6mths",
    "delinq_2yrs",
    "pub_rec",
    "pub_rec_bankruptcies",
    "revol_bal",
    "revol_util",
    "open_acc",
    "total_acc",
    "mort_acc",
]
RAW_COLUMNS = ["issue_d", "loan_status"] + RAW_INPUTS

FEATURES = [
    "loan_amnt",
    "purpose",
    "emp_length_years",
    "home_ownership",
    "annual_inc",
    "verification_status",
    "dti",
    "fico",
    "credit_history_years",
    "inq_last_6mths",
    "delinq_2yrs",
    "pub_rec",
    "pub_rec_bankruptcies",
    "revol_bal",
    "revol_util",
    "open_acc",
    "total_acc",
    "mort_acc",
]


def load_raw(path: Path = ACCEPTED_PATH, columns=RAW_COLUMNS) -> pd.DataFrame:
    df = pd.read_csv(path, usecols=columns, low_memory=False)
    return df[df["issue_d"].notna()].reset_index(drop=True)


def add_target(df: pd.DataFrame) -> pd.DataFrame:
    unknown = set(df["loan_status"].dropna()) - BAD_STATUSES - GOOD_STATUSES - OPEN_STATUSES
    if unknown or df["loan_status"].isna().any():
        raise ValueError(f"unmapped loan_status values: {sorted(unknown) or ['<missing>']}")

    final = df[~df["loan_status"].isin(OPEN_STATUSES)].copy()
    final["target"] = final["loan_status"].isin(BAD_STATUSES).astype(int)
    return final.drop(columns=["loan_status"])


def term_months(term: pd.Series) -> pd.Series:
    return term.str.extract(r"(\d+)", expand=False).astype(int)


def matured(df: pd.DataFrame, cutoff: str = DATA_CUTOFF) -> pd.DataFrame:
    issued = pd.to_datetime(df["issue_d"], format=DATE_FORMAT).dt.to_period("M")
    last_due = issued + term_months(df["term"])
    return df[last_due <= pd.Period(cutoff, freq="M")]


def keep_term(df: pd.DataFrame, months: int = TERM_MONTHS) -> pd.DataFrame:
    return df[term_months(df["term"]) == months]


def emp_length_years(emp_length: pd.Series) -> pd.Series:
    known = emp_length.dropna()
    years = known.replace({"< 1 year": "0", "10+ years": "10"}).str.extract(r"^(\d+) years?$|^(\d+)$")
    parsed = years[0].fillna(years[1])
    if parsed.isna().any():
        raise ValueError(f"unknown emp_length values: {sorted(known[parsed.isna()].unique())}")
    return parsed.astype(float).reindex(emp_length.index)


def prepare(df: pd.DataFrame) -> pd.DataFrame:
    issued = pd.to_datetime(df["issue_d"], format=DATE_FORMAT)
    first_credit = pd.to_datetime(df["earliest_cr_line"], format=DATE_FORMAT)
    out = df.assign(
        emp_length_years=emp_length_years(df["emp_length"]),
        fico=(df["fico_range_low"] + df["fico_range_high"]) / 2,
        credit_history_years=(issued - first_credit).dt.days / 365.25,
        issue_year=issued.dt.year,
    )
    return out[FEATURES + ["issue_year", "target"]]


def load_dataset(path: Path = ACCEPTED_PATH) -> pd.DataFrame:
    return prepare(keep_term(matured(add_target(load_raw(path)))))


def split_by_time(df: pd.DataFrame, test_year: int = TEST_YEAR, train_from: int | None = None,
                  target_col: str = "target"):
    is_test = df["issue_year"] == test_year
    if not is_test.any():
        raise ValueError(f"no loans issued in {test_year}")
    is_train = df["issue_year"] < test_year
    if train_from is not None:
        is_train &= df["issue_year"] >= train_from
    train, test = df[is_train], df[is_test]
    return train[FEATURES], test[FEATURES], train[target_col], test[target_col]
