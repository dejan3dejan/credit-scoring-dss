import numpy as np
import pytest

from conftest import VALID_REQUEST


def with_changes(**changes):
    return {**VALID_REQUEST, **changes}


def without(field):
    return {k: v for k, v in VALID_REQUEST.items() if k != field}


def assert_validation_error(resp, expected_details):
    assert resp.status_code == 422
    body = resp.json()
    assert body["error"]["code"] == "validation_error"
    assert body["error"]["message"] == "Request failed validation"
    assert body["error"]["details"] == expected_details


@pytest.mark.parametrize("payload, field, issue", [
    (without("age"), "age", "field required"),
    (with_changes(duration_months="24"), "duration_months", "must be an integer"),
    (with_changes(credit_amount=3500.5), "credit_amount", "must be an integer"),
    (with_changes(credit_amount=3500.0), "credit_amount", "must be an integer"),
    (with_changes(num_people_liable=True), "num_people_liable", "must be an integer"),
    (with_changes(age=None), "age", "must be an integer"),
    (with_changes(purpose="A47"), "purpose",
     "unknown code 'A47'; allowed: A40, A41, A42, A43, A44, A45, A46, A48, A49, A410"),
    (with_changes(personal_status_sex="A95"), "personal_status_sex",
     "unknown code 'A95'; allowed: A91, A92, A93, A94"),
    (with_changes(telephone=191), "telephone", "unknown code 191; allowed: A191, A192"),
    (with_changes(duration_months=0), "duration_months", "must be >= 1"),
    (with_changes(credit_amount=-100), "credit_amount", "must be >= 1"),
    (with_changes(age=17), "age", "must be >= 18"),
    (with_changes(installment_rate=5), "installment_rate", "must be <= 4"),
    (with_changes(present_residence_since=0), "present_residence_since", "must be >= 1"),
    (with_changes(existing_credits_count=5), "existing_credits_count", "must be <= 4"),
    (with_changes(num_people_liable=3), "num_people_liable", "must be <= 2"),
    (with_changes(applicant_name="Jane"), "applicant_name", "unknown field"),
])
def test_single_field_error(client, payload, field, issue):
    assert_validation_error(client.post("/v1/predict", json=payload), [{"field": field, "issue": issue}])


def test_every_failing_field_is_listed(client):
    payload = {**without("housing"), "purpose": "A47", "age": 17, "installment_rate": "3", "extra": 1}
    resp = client.post("/v1/predict", json=payload)
    assert resp.status_code == 422
    details = resp.json()["error"]["details"]
    assert {d["field"] for d in details} == {"housing", "purpose", "age", "installment_rate", "extra"}


def test_malformed_json(client):
    resp = client.post("/v1/predict", content=b'{"age": 35,',
                       headers={"Content-Type": "application/json"})
    assert_validation_error(resp, [{"field": "body", "issue": "malformed JSON"}])


def test_body_must_be_an_object(client):
    resp = client.post("/v1/predict", json=[VALID_REQUEST])
    assert_validation_error(resp, [{"field": "body", "issue": "request body must be a JSON object"}])


class RaisingModel:
    def predict_proba(self, X):
        raise ValueError("boom")


class ConstantModel:
    def __init__(self, p):
        self.p = p

    def predict_proba(self, X):
        return np.array([[1 - self.p, self.p]])


@pytest.mark.parametrize("model", [RaisingModel(), ConstantModel(np.nan), ConstantModel(1.5)])
def test_scoring_failure_is_internal_error_without_score(client, model):
    client.app.state.model = model
    resp = client.post("/v1/predict", json=VALID_REQUEST)
    assert resp.status_code == 500
    assert resp.json() == {"error": {"code": "internal_error", "message": "Unexpected error during scoring"}}
