from pathlib import Path

import pandas as pd

RAW_PATH = Path(__file__).resolve().parents[2] / "data" / "raw" / "german.data"

COLUMN_NAMES = [
    "checking_account_status",
    "duration_months",
    "credit_history",
    "purpose",
    "credit_amount",
    "savings_account",
    "employment_duration",
    "installment_rate",
    "personal_status_sex",
    "other_debtors_guarantors",
    "present_residence_since",
    "property",
    "age",
    "other_installment_plans",
    "housing",
    "existing_credits_count",
    "job",
    "num_people_liable",
    "telephone",
    "foreign_worker",
    "raw_class",
]


def load_raw(path: Path = RAW_PATH) -> pd.DataFrame:
    return pd.read_csv(path, sep=r"\s+", header=None, names=COLUMN_NAMES)


def add_target(df: pd.DataFrame) -> pd.DataFrame:
    df = df.copy()
    df["target"] = (df["raw_class"] == 2).astype(int)
    return df.drop(columns=["raw_class"])


def load_dataset(path: Path = RAW_PATH) -> pd.DataFrame:
    return add_target(load_raw(path))
