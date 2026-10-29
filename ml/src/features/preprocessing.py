import numpy as np
from sklearn.compose import ColumnTransformer
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler, OrdinalEncoder, OneHotEncoder, FunctionTransformer

LOG_SCALE_COLUMNS = [
    "credit_amount",
    "duration_months",
]

SCALE_COLUMNS = [
    "age",
    "installment_rate",
    "present_residence_since",
    "existing_credits_count",
    "num_people_liable",
]

ORDINAL_COLUMNS = [
    "job",
    "employment_duration",
]

NOMINAL_COLUMNS = [
    "checking_account_status",
    "credit_history",
    "purpose",
    "savings_account",
    "personal_status_sex",
    "other_debtors_guarantors",
    "property",
    "other_installment_plans",
    "housing",
    "telephone",
    "foreign_worker",
]


def build_preprocessing_pipeline(drop_first=False):
    log_scale_pipe = Pipeline([
        ("log1p", FunctionTransformer(np.log1p, feature_names_out="one-to-one")),
        ("scale", StandardScaler()),
    ])

    ordinal_categories = [
        ["A171", "A172", "A173", "A174"],
        ["A71", "A72", "A73", "A74", "A75"],
    ]

    return ColumnTransformer([
        ("log_scale", log_scale_pipe, LOG_SCALE_COLUMNS),
        ("scale", StandardScaler(), SCALE_COLUMNS),
        ("ordinal", OrdinalEncoder(categories=ordinal_categories), ORDINAL_COLUMNS),
        ("nominal", OneHotEncoder(
            drop="first" if drop_first else None,
            handle_unknown="ignore",
            sparse_output=False,
        ), NOMINAL_COLUMNS),
    ])
