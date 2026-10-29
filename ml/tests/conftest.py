import numpy as np
import pandas as pd
import pytest

CATEGORY_CODES = {
    "checking_account_status": ["A11", "A12", "A13", "A14"],
    "credit_history": ["A30", "A31", "A32", "A33", "A34"],
    "purpose": ["A40", "A41", "A42", "A43", "A44", "A45", "A46", "A48", "A49", "A410"],
    "savings_account": ["A61", "A62", "A63", "A64", "A65"],
    "employment_duration": ["A71", "A72", "A73", "A74", "A75"],
    "personal_status_sex": ["A91", "A92", "A93", "A94"],
    "other_debtors_guarantors": ["A101", "A102", "A103"],
    "property": ["A121", "A122", "A123", "A124"],
    "other_installment_plans": ["A141", "A142", "A143"],
    "housing": ["A151", "A152", "A153"],
    "job": ["A171", "A172", "A173", "A174"],
    "telephone": ["A191", "A192"],
    "foreign_worker": ["A201", "A202"],
}


@pytest.fixture
def synthetic_applicants():
    rng = np.random.default_rng(0)
    n = 300
    df = pd.DataFrame({col: rng.choice(codes, size=n) for col, codes in CATEGORY_CODES.items()})
    df["duration_months"] = rng.integers(4, 61, size=n)
    df["credit_amount"] = rng.integers(250, 18425, size=n)
    df["installment_rate"] = rng.integers(1, 5, size=n)
    df["present_residence_since"] = rng.integers(1, 5, size=n)
    df["age"] = rng.integers(19, 76, size=n)
    df["existing_credits_count"] = rng.integers(1, 5, size=n)
    df["num_people_liable"] = rng.integers(1, 3, size=n)
    y = pd.Series((rng.random(n) < 0.3).astype(int))
    return df, y
