import hashlib
import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import pandas as pd

from src.data.lending_club import (ACCEPTED_PATH, DATA_CUTOFF, FEATURES, RAW_COLUMNS, REJECTED_PATH, TERM_MONTHS,
                                   TEST_YEAR, TRAIN_FROM, add_target, keep_term, load_raw, matured, prepare,
                                   split_by_time)

PROFILE_COLUMNS = RAW_COLUMNS + ["funded_amnt", "total_pymnt", "application_type"]


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def section(title):
    print(f"\n## {title}")


def cost_ratio(title, loans):
    net = loans["total_pymnt"] - loans["funded_amnt"]
    good, bad = net[loans["target"] == 0], net[loans["target"] == 1]
    section(f"result per loan, total paid back minus amount funded (USD): {title}")
    print(f"fully paid:  mean {good.mean():.2f}  median {good.median():.2f}  n {len(good)}")
    print(f"charged off: mean {bad.mean():.2f}  median {bad.median():.2f}  n {len(bad)}")
    ratio = -bad.mean() / good.mean()
    print(f"cost ratio (mean loss / mean gain): {ratio:.4f}  threshold 1 / (1 + ratio): {1 / (1 + ratio):.4f}")


def main():
    print(f"accepted file sha256: {sha256(ACCEPTED_PATH)}")

    raw = load_raw(ACCEPTED_PATH, PROFILE_COLUMNS)
    section("all loans")
    print(f"loans: {len(raw)}")
    issued = pd.to_datetime(raw["issue_d"], format="%b-%Y")
    print(f"issued: {issued.min():%Y-%m} to {issued.max():%Y-%m}")
    print(raw["loan_status"].value_counts().to_string())

    labelled = matured(add_target(raw), DATA_CUTOFF)
    section(f"final outcome, term ended by {DATA_CUTOFF}")
    print(f"loans: {len(labelled)}  bad rate: {labelled['target'].mean():.4f}")
    print(labelled["term"].str.strip().value_counts().to_string())
    print(labelled["application_type"].value_counts().to_string())

    years = pd.to_datetime(labelled["issue_d"], format="%b-%Y").dt.year
    cost_ratio("all finished loans, all terms and years (context only)", labelled)
    in_train = keep_term(labelled, TERM_MONTHS)
    in_train = in_train[years.loc[in_train.index].between(TRAIN_FROM, TEST_YEAR - 1)]
    cost_ratio(f"training loans only: {TERM_MONTHS}-month, {TRAIN_FROM} to {TEST_YEAR - 1} (sets the threshold)", in_train)

    section("by issue year, all terms")
    by_year = labelled.groupby(years)["target"].agg(loans="count", bad_rate="mean")
    by_year["share_60_months"] = labelled["term"].str.contains("60").groupby(years).mean()
    print(by_year.round(4).to_string())

    df = prepare(keep_term(labelled, TERM_MONTHS))
    section(f"{TERM_MONTHS}-month loans only")
    print(f"loans: {len(df)}  bad rate: {df['target'].mean():.4f}")
    by_year = df.groupby("issue_year")["target"].agg(loans="count", bad_rate="mean")
    by_year["missing_mort_acc"] = df["mort_acc"].isna().groupby(df["issue_year"]).mean()
    by_year["missing_pub_rec_bankruptcies"] = df["pub_rec_bankruptcies"].isna().groupby(df["issue_year"]).mean()
    print(by_year.round(4).to_string())

    X_train, X_test, y_train, y_test = split_by_time(df, TEST_YEAR, TRAIN_FROM)
    section(f"time split: train {TRAIN_FROM} to {TEST_YEAR - 1}, test {TEST_YEAR}")
    print(f"train: {len(y_train)} loans, bad rate {y_train.mean():.4f}")
    print(f"test:  {len(y_test)} loans, bad rate {y_test.mean():.4f}")

    section("model inputs: missing share and range (train)")
    numeric = X_train.select_dtypes("number")
    summary = pd.DataFrame({
        "missing": X_train[FEATURES].isna().mean(),
        "min": numeric.min(),
        "p0.5": numeric.quantile(0.005),
        "median": numeric.median(),
        "p99.5": numeric.quantile(0.995),
        "max": numeric.max(),
    }).loc[FEATURES]
    print(summary.round(3).to_string())
    for column in X_train.select_dtypes(exclude="number").columns:
        print(f"\n{column}:")
        print(X_train[column].value_counts(dropna=False).to_string())

    section("rejected applications")
    rejected = pd.read_csv(REJECTED_PATH, usecols=["Application Date"])
    years = pd.to_datetime(rejected["Application Date"]).dt.year
    print(f"rejected: {len(rejected)}")
    print(years.value_counts().sort_index().to_string())


if __name__ == "__main__":
    main()
