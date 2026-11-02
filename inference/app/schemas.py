from typing import Annotated, Literal, get_args, get_origin

from pydantic import BaseModel, ConfigDict, Field

AtLeastOne = Annotated[int, Field(ge=1)]
Bucket4 = Annotated[int, Field(ge=1, le=4)]
Count = Annotated[int, Field(ge=0)]
NonNegative = Annotated[float, Field(ge=0)]


class PredictRequest(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)

    checking_account_status: Literal["A11", "A12", "A13", "A14"]
    duration_months: AtLeastOne
    credit_history: Literal["A30", "A31", "A32", "A33", "A34"]
    purpose: Literal["A40", "A41", "A42", "A43", "A44", "A45", "A46", "A48", "A49", "A410"]
    credit_amount: AtLeastOne
    savings_account: Literal["A61", "A62", "A63", "A64", "A65"]
    employment_duration: Literal["A71", "A72", "A73", "A74", "A75"]
    installment_rate: Bucket4
    personal_status_sex: Literal["A91", "A92", "A93", "A94"]
    other_debtors_guarantors: Literal["A101", "A102", "A103"]
    present_residence_since: Bucket4
    property: Literal["A121", "A122", "A123", "A124"]
    age: Annotated[int, Field(ge=18)]
    other_installment_plans: Literal["A141", "A142", "A143"]
    housing: Literal["A151", "A152", "A153"]
    existing_credits_count: Bucket4
    job: Literal["A171", "A172", "A173", "A174"]
    num_people_liable: Annotated[int, Field(ge=1, le=2)]
    telephone: Literal["A191", "A192"]
    foreign_worker: Literal["A201", "A202"]


class LendingClubRequest(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)

    loan_amnt: AtLeastOne
    purpose: Literal["car", "credit_card", "debt_consolidation", "home_improvement", "house", "major_purchase",
                     "medical", "moving", "other", "renewable_energy", "small_business", "vacation", "wedding"]
    emp_length_years: Annotated[int, Field(ge=0, le=10)] | None
    home_ownership: Literal["RENT", "OWN", "MORTGAGE", "OTHER"]
    annual_inc: NonNegative
    verification_status: Literal["Not Verified", "Source Verified", "Verified"]
    dti: NonNegative
    fico: Annotated[float, Field(ge=300, le=850)]
    credit_history_years: NonNegative
    inq_last_6mths: Count
    delinq_2yrs: Count
    pub_rec: Count
    pub_rec_bankruptcies: Count
    revol_bal: NonNegative
    revol_util: NonNegative | None
    open_acc: Count
    total_acc: Count
    mort_acc: Count | None


def category_codes(request_model):
    return {
        name: list(get_args(field.annotation))
        for name, field in request_model.model_fields.items()
        if get_origin(field.annotation) is Literal
    }


FEATURES = list(PredictRequest.model_fields)
CATEGORY_CODES = category_codes(PredictRequest)

LC_FEATURES = list(LendingClubRequest.model_fields)
LC_CATEGORY_CODES = category_codes(LendingClubRequest)


class PredictResponse(BaseModel):
    model_id: str
    p_bad: float


class TopFactor(BaseModel):
    feature: str
    value: int | float | str | None
    contribution: float


class LendingClubPredictResponse(PredictResponse):
    top_factors: list[TopFactor] | None


class Threshold(BaseModel):
    value: float
    rule: str
    derivation: str


class ModelInfo(BaseModel):
    model_id: str
    output: str
    recommended_threshold: Threshold
    features: list[str]
    library_versions: dict[str, str]


class ModelSummary(BaseModel):
    model_id: str
    model_name: str
    dataset: str
    recommended_threshold: Threshold
    created_at: str
    library_versions: dict[str, str]


class ModelList(BaseModel):
    models: list[ModelSummary]


class ReviewBand(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    from_: float = Field(alias="from", serialization_alias="from")
    to: float
    rule: str


class LendingClubModelSummary(ModelSummary):
    recommended_review_band: ReviewBand | None


class LendingClubModelList(BaseModel):
    models: list[LendingClubModelSummary]


class Health(BaseModel):
    status: Literal["ok", "degraded"]
    model_loaded: bool
    model_id: str | None
    models_loaded: list[str]
