import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import pandas as pd
from sklearn.metrics import average_precision_score, brier_score_loss, roc_auc_score

from src.data import lending_club as lc
from src.evaluation.segments import expected_calibration_error, segment_table, standardised_mean_difference
from src.models.artifact import load_artifact

MODEL_PATH = Path(__file__).resolve().parents[1] / "models" / "credit_lc_xgb_isotonic_v1.joblib"
CATEGORICAL = {"purpose", "home_ownership", "verification_status"}
FICO_BANDS = [0, 680, 700, 720, 760, 900]


def label_availability():
    raw = lc.load_raw(columns=lc.RAW_COLUMNS + ["last_pymnt_d"])
    issued = pd.to_datetime(raw["issue_d"], format=lc.DATE_FORMAT)
    year = issued.dt.year
    term = lc.term_months(raw["term"])
    last_due = issued.dt.to_period("M") + term
    scope = raw[(term == lc.TERM_MONTHS) & year.between(lc.TRAIN_FROM, lc.TEST_YEAR)
                & (last_due <= pd.Period(lc.DATA_CUTOFF, freq="M"))]
    open_ = scope["loan_status"].isin(lc.OPEN_STATUSES)
    scope_year = year[scope.index]

    print("matured 36-month loans still open at the data cut-off (dropped from the data):")
    print(pd.DataFrame({"open": open_, "year": scope_year}).groupby("year")["open"].agg(["sum", "mean", "size"]).to_string())

    train = scope[~open_ & (scope_year < lc.TEST_YEAR)]
    known = pd.to_datetime(train["last_pymnt_d"], format=lc.DATE_FORMAT) < pd.Timestamp(f"{lc.TEST_YEAR}-01-01")
    bad = train["loan_status"].isin(lc.BAD_STATUSES)
    print(f"\ntraining loans whose outcome was known before {lc.TEST_YEAR}-01: {known.mean():.4f} of {len(train)}")
    print(f"  bad rate among them {bad[known].mean():.4f}, among all training loans {bad.mean():.4f}")
    print(pd.DataFrame({"known": known, "year": scope_year[train.index]}).groupby("year")["known"].mean().round(4).to_string())


def main():
    model, metadata = load_artifact(MODEL_PATH)
    df = lc.load_dataset()
    X_train, X_test, y_train, y_test = lc.split_by_time(df, lc.TEST_YEAR, lc.TRAIN_FROM)
    p_train = model.predict_proba(X_train)[:, 1]
    p_test = model.predict_proba(X_test)[:, 1]

    print(f"model {metadata['model_id']}; training {lc.TRAIN_FROM} to {lc.TEST_YEAR - 1}, test {lc.TEST_YEAR}\n")
    label_availability()

    print(f"\n{lc.TEST_YEAR}: n {len(y_test)}, bad rate {y_test.mean():.4f}, mean P(bad) {p_test.mean():.4f}")
    print(f"  ROC-AUC {roc_auc_score(y_test, p_test):.4f}  PR-AUC {average_precision_score(y_test, p_test):.4f}"
          f"  Brier {brier_score_loss(y_test, p_test):.4f}  ECE (10 quantile bins) {expected_calibration_error(y_test, p_test):.4f}")

    print(f"\ndrift: bad rate {y_train.mean():.4f} -> {y_test.mean():.4f}; "
          f"mean P(bad) {p_train.mean():.4f} -> {p_test.mean():.4f} (same model, so this part comes from the inputs)")
    numeric = [c for c in lc.FEATURES if c not in CATEGORICAL]
    smd = standardised_mean_difference(X_train[numeric], X_test[numeric]).sort_values(key=abs, ascending=False)
    print("standardised mean difference of numeric inputs, training years -> test year:")
    print(smd.round(3).to_string())

    segments = {
        "home_ownership": X_test["home_ownership"],
        "verification_status": X_test["verification_status"],
        "fico_band": pd.cut(X_test["fico"], FICO_BANDS, right=False),
        "income_quartile": pd.qcut(X_test["annual_inc"], 4),
        "purpose": X_test["purpose"],
    }
    for name, groups in segments.items():
        print(f"\n{lc.TEST_YEAR} by {name} (gap = mean P(bad) - observed bad rate; AUC left out under 200 loans):")
        print(segment_table(y_test, p_test, groups).round(4).to_string())


if __name__ == "__main__":
    main()
