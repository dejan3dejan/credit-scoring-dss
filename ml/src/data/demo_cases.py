import math

import numpy as np
import pandas as pd

PURPOSES = {
    "debt_consolidation": "DebtConsolidation",
    "credit_card": "CreditCard",
    "home_improvement": "HomeImprovement",
    "major_purchase": "MajorPurchase",
    "car": "Car",
    "medical": "Medical",
    "moving": "Moving",
    "house": "House",
    "vacation": "Vacation",
    "wedding": "Wedding",
    "small_business": "SmallBusiness",
    "renewable_energy": "RenewableEnergy",
    "other": "Other",
}
HOME_OWNERSHIP = {"RENT": "Rent", "OWN": "Own", "MORTGAGE": "Mortgage", "OTHER": "Other"}
VERIFICATION = {"Not Verified": "NotVerified", "Source Verified": "SourceVerified", "Verified": "Verified"}
EMPLOYMENT = ["LessThanOneYear", "OneYear", "TwoYears", "ThreeYears", "FourYears", "FiveYears",
              "SixYears", "SevenYears", "EightYears", "NineYears", "TenOrMoreYears"]

MIN_LOAN, MAX_LOAN = 1_000, 35_000
MIN_SCORE, MAX_SCORE = 300, 850
MAX_DTI, MAX_UTILISATION = 100, 200
MAX_RECENT, MAX_OPEN, MAX_TOTAL = 50, 100, 200
MAX_HISTORY_YEARS = 80
MONTHS_PER_YEAR = 12


def _lookup(table: dict, value, field: str) -> str:
    if value not in table:
        raise ValueError(f"{field} {value!r} has no value in the application form")
    return table[value]


def to_answers(row: pd.Series) -> dict:
    years = row["emp_length_years"]
    return {
        "loanAmount": int(row["loan_amnt"]),
        "purpose": _lookup(PURPOSES, row["purpose"], "purpose"),
        "employmentLength": "NotStated" if pd.isna(years) else EMPLOYMENT[int(years)],
        "homeOwnership": _lookup(HOME_OWNERSHIP, row["home_ownership"], "home_ownership"),
        "annualIncome": float(row["annual_inc"]),
        "incomeVerification": _lookup(VERIFICATION, row["verification_status"], "verification_status"),
        "debtToIncomePercent": float(row["dti"]),
        "creditScore": int(row["fico"]),
        "creditHistoryMonths": int(round(row["credit_history_years"] * MONTHS_PER_YEAR)),
        "inquiriesLast6Months": int(row["inq_last_6mths"]),
        "delinquenciesLast2Years": int(row["delinq_2yrs"]),
        "publicRecords": int(row["pub_rec"]),
        "bankruptcies": int(row["pub_rec_bankruptcies"]),
        "revolvingBalance": float(row["revol_bal"]),
        "revolvingUtilizationPercent": float(row["revol_util"]),
        "openAccounts": int(row["open_acc"]),
        "totalAccounts": int(row["total_acc"]),
        "mortgageAccounts": int(row["mort_acc"]),
    }


def fits_application(row: pd.Series) -> bool:
    required = row.drop(labels=["emp_length_years"])
    if required.isna().any():
        return False
    counts = [row["inq_last_6mths"], row["delinq_2yrs"], row["pub_rec"], row["pub_rec_bankruptcies"], row["mort_acc"]]
    return (
        row["purpose"] in PURPOSES
        and row["home_ownership"] in HOME_OWNERSHIP
        and row["verification_status"] in VERIFICATION
        and MIN_LOAN <= row["loan_amnt"] <= MAX_LOAN
        and float(row["loan_amnt"]).is_integer()
        and MIN_SCORE <= row["fico"] <= MAX_SCORE
        and float(row["fico"]).is_integer()
        and 0 <= row["dti"] <= MAX_DTI
        and 0 <= row["revol_util"] <= MAX_UTILISATION
        and (row["revol_bal"] > 0 or row["revol_util"] == 0)
        and 0 < row["credit_history_years"] <= MAX_HISTORY_YEARS - 1
        and all(0 <= c <= MAX_RECENT for c in counts)
        and row["pub_rec_bankruptcies"] <= row["pub_rec"]
        and 0 <= row["open_acc"] <= MAX_OPEN
        and 0 <= row["total_acc"] <= MAX_TOTAL
        and row["open_acc"] <= row["total_acc"]
        and row["mort_acc"] <= row["total_acc"]
        and not math.isnan(row["annual_inc"])
    )


def band_of(p_bad: float, threshold: float, review_from: float) -> str:
    if p_bad >= threshold:
        return "Reject"
    return "Review" if p_bad >= review_from else "Approve"


def pick_cases(loans: pd.DataFrame, p_bad: pd.Series, threshold: float, review_from: float,
               size: int, seed: int) -> pd.DataFrame:
    usable = loans[loans.apply(fits_application, axis=1)].copy()
    usable["p_bad"] = p_bad.loc[usable.index]
    usable["band"] = [band_of(p, threshold, review_from) for p in usable["p_bad"]]

    rng = np.random.default_rng(seed)
    picked = usable.loc[rng.choice(usable.index.to_numpy(), size=size, replace=False)]
    if not (picked["band"] == "Reject").any():
        rejects = usable.index[usable["band"] == "Reject"].to_numpy()
        if len(rejects) == 0:
            raise ValueError("no loan reaches the reject threshold, so the demo cannot show one")
        picked = pd.concat([picked.iloc[:-1], usable.loc[[rng.choice(rejects)]]])
    return picked
