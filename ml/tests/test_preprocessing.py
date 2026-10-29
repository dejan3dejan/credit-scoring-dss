import numpy as np
import pandas as pd
import pytest

from src.features.preprocessing import build_preprocessing_pipeline


@pytest.fixture
def sample_df():
    return pd.DataFrame({
        "checking_account_status": ["A11", "A12", "A14", "A13"],
        "duration_months": [6, 12, 24, 36],
        "credit_history": ["A32", "A34", "A30", "A33"],
        "purpose": ["A40", "A43", "A41", "A42"],
        "credit_amount": [1000, 0, 5000, 2500],
        "savings_account": ["A61", "A65", "A62", "A63"],
        "employment_duration": ["A71", "A72", "A73", "A74"],
        "installment_rate": [1, 2, 3, 4],
        "personal_status_sex": ["A91", "A92", "A93", "A94"],
        "other_debtors_guarantors": ["A101", "A102", "A103", "A101"],
        "present_residence_since": [1, 2, 3, 4],
        "property": ["A121", "A122", "A123", "A124"],
        "age": [25, 40, 55, 30],
        "other_installment_plans": ["A141", "A142", "A143", "A141"],
        "housing": ["A151", "A152", "A153", "A151"],
        "existing_credits_count": [1, 2, 1, 3],
        "job": ["A171", "A172", "A173", "A174"],
        "num_people_liable": [1, 2, 1, 1],
        "telephone": ["A191", "A192", "A191", "A192"],
        "foreign_worker": ["A201", "A202", "A201", "A201"],
    })


def test_output_shape(sample_df):
    transformed = build_preprocessing_pipeline().fit_transform(sample_df)
    nominal_levels = 4 + 4 + 4 + 4 + 4 + 3 + 4 + 3 + 3 + 2 + 2
    assert transformed.shape == (len(sample_df), 2 + 5 + 2 + nominal_levels)


def test_drop_first_removes_one_level_per_nominal_column(sample_df):
    full = build_preprocessing_pipeline().fit_transform(sample_df)
    dropped = build_preprocessing_pipeline(drop_first=True).fit_transform(sample_df)
    assert full.shape[1] - dropped.shape[1] == 11


def test_unseen_category_does_not_crash(sample_df):
    pipeline = build_preprocessing_pipeline()
    pipeline.fit(sample_df)
    new_row = sample_df.iloc[[0]].copy()
    new_row["purpose"] = "A48"
    assert pipeline.transform(new_row).shape[0] == 1


def test_log_transform_handles_zero(sample_df):
    transformed = build_preprocessing_pipeline().fit_transform(sample_df)
    assert np.isfinite(transformed).all()


def test_feature_names_are_readable(sample_df):
    pipeline = build_preprocessing_pipeline()
    pipeline.fit(sample_df)
    feature_names = pipeline.get_feature_names_out()

    assert len(feature_names) == pipeline.transform(sample_df).shape[1]
    assert "nominal__checking_account_status_A12" in feature_names
    assert "log_scale__credit_amount" in feature_names
    assert "scale__age" in feature_names
    assert "ordinal__job" in feature_names
