import logging
import math
import os
from contextlib import asynccontextmanager
from pathlib import Path

import pandas as pd
from fastapi import FastAPI, Request, Response

from app.errors import ModelNotFound, ModelUnavailable, install_handlers
from app.schemas import (CATEGORY_CODES, FEATURES, LC_CATEGORY_CODES, LC_FEATURES, Health, LendingClubModelList,
                         LendingClubModelSummary, LendingClubPredictResponse, LendingClubRequest, ModelInfo, ModelList, ModelSummary,
                         PredictRequest, PredictResponse)
from src.models.artifact import load_artifact
from src.models.explain import explainable, top_factors

logger = logging.getLogger("uvicorn.error.inference")

DEFAULT_MODEL_PATH = (
    Path(__file__).resolve().parents[2] / "ml" / "models" / "credit_rf_isotonic_v1.joblib"
)
DATASET = "statlog_german_credit"
LC_DATASET = "lending_club"


def dataset_of(metadata):
    return metadata.get("dataset", DATASET)


def check_contract_matches(metadata):
    features = metadata["features"]
    if features["order"] != FEATURES:
        raise RuntimeError(f"artifact feature order {features['order']} differs from the v1 contract")
    seen = features["categorical_levels_seen_in_training"]
    if seen.keys() != CATEGORY_CODES.keys():
        raise RuntimeError(f"artifact categorical fields {sorted(seen)} differ from the v1 contract")
    for field, codes in CATEGORY_CODES.items():
        if set(seen[field]) != set(codes):
            raise RuntimeError(
                f"{field}: artifact codes {sorted(seen[field])} differ from contract codes {sorted(codes)}"
            )


def check_lc_contract_matches(metadata):
    features = metadata["features"]
    if features["order"] != LC_FEATURES:
        raise RuntimeError(f"artifact feature order {features['order']} differs from the v3 contract")
    seen = features["categorical_levels_seen_in_training"]
    for field, codes in LC_CATEGORY_CODES.items():
        unseen = set(codes) - set(seen.get(field, []))
        if unseen:
            raise RuntimeError(f"{field}: contract codes {sorted(unseen)} were never seen in training")


CONTRACT_CHECKS = {DATASET: check_contract_matches, LC_DATASET: check_lc_contract_matches}


def load_model(model_path):
    model, metadata = load_artifact(model_path)
    check_contract_matches(metadata)
    return model, metadata


def load_models_dir(models_dir):
    models = {DATASET: {}, LC_DATASET: {}}
    for path in sorted(Path(models_dir).glob("*.joblib")):
        try:
            model, metadata = load_artifact(path)
            dataset = dataset_of(metadata)
            if dataset not in CONTRACT_CHECKS:
                raise RuntimeError(f"unknown dataset {dataset!r}")
            CONTRACT_CHECKS[dataset](metadata)
        except Exception:
            logger.exception("skipping %s: it failed to load or does not match its contract", path)
            continue
        models[dataset][metadata["model_id"]] = (model, metadata)
        logger.info("loaded %s (%s)", metadata["model_id"], dataset)
    return models


def lc_frame(body):
    X = pd.DataFrame([body.model_dump()], columns=LC_FEATURES)
    return X.astype({name: float for name in LC_FEATURES if name not in LC_CATEGORY_CODES})


def score(model, metadata, body, frame=None):
    X = pd.DataFrame([body.model_dump()], columns=FEATURES) if frame is None else frame
    p_bad = float(model.predict_proba(X)[0, 1])
    if not (math.isfinite(p_bad) and 0.0 <= p_bad <= 1.0):
        raise ValueError(f"model returned p_bad={p_bad}, outside [0, 1]")
    return PredictResponse(model_id=metadata["model_id"], p_bad=p_bad)


def explain(model, body, frame):
    if not explainable(model):
        return None
    sent = body.model_dump()
    factors = top_factors(model, frame, LC_FEATURES)
    if not all(math.isfinite(f["contribution"]) for f in factors):
        raise ValueError(f"model returned a non-finite contribution: {factors}")
    return [{**f, "value": sent[f["feature"]]} for f in factors]


def create_app(model_path=None, models_dir=None) -> FastAPI:
    model_path = Path(model_path or os.environ.get("MODEL_PATH", DEFAULT_MODEL_PATH))
    models_dir = Path(models_dir or os.environ.get("MODELS_DIR", model_path.parent))

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        try:
            app.state.model, app.state.metadata = load_model(model_path)
            logger.info("loaded %s", app.state.metadata["model_id"])
        except Exception:
            logger.exception("model load failed for %s; scoring endpoints return 503", model_path)
            app.state.model, app.state.metadata = None, None
        loaded_models = load_models_dir(models_dir)
        app.state.models, app.state.lc_models = loaded_models[DATASET], loaded_models[LC_DATASET]
        yield

    app = FastAPI(title="Credit scoring inference", version="1", lifespan=lifespan)
    install_handlers(app)

    def loaded(request: Request):
        if request.app.state.model is None:
            raise ModelUnavailable()
        return request.app.state.model, request.app.state.metadata

    @app.post("/v1/predict", response_model=PredictResponse)
    def predict(body: PredictRequest, request: Request):
        model, metadata = loaded(request)
        return score(model, metadata, body)

    @app.get("/v2/models", response_model=ModelList)
    def list_models(request: Request):
        return ModelList(models=[
            ModelSummary(
                model_id=model_id,
                model_name=metadata["model_name"],
                dataset=DATASET,
                recommended_threshold=metadata["recommended_threshold"],
                created_at=metadata["created_at"],
                library_versions=metadata["library_versions"],
            )
            for model_id, (_, metadata) in sorted(request.app.state.models.items())
        ])

    @app.post("/v2/models/{model_id}/predict", response_model=PredictResponse)
    def predict_with(model_id: str, body: PredictRequest, request: Request):
        if model_id not in request.app.state.models:
            raise ModelNotFound(model_id)
        model, metadata = request.app.state.models[model_id]
        return score(model, metadata, body)

    @app.get("/v3/models", response_model=LendingClubModelList, response_model_by_alias=True)
    def list_lc_models(request: Request):
        return LendingClubModelList(models=[
            LendingClubModelSummary(
                model_id=model_id,
                model_name=metadata["model_name"],
                dataset=LC_DATASET,
                recommended_threshold=metadata["recommended_threshold"],
                recommended_review_band=metadata.get("recommended_review_band"),
                created_at=metadata["created_at"],
                library_versions=metadata["library_versions"],
            )
            for model_id, (_, metadata) in sorted(request.app.state.lc_models.items())
        ])

    @app.post("/v3/models/{model_id}/predict", response_model=LendingClubPredictResponse)
    def predict_lc(model_id: str, body: LendingClubRequest, request: Request):
        if model_id not in request.app.state.lc_models:
            raise ModelNotFound(model_id)
        model, metadata = request.app.state.lc_models[model_id]
        frame = lc_frame(body)
        scored = score(model, metadata, body, frame)
        return LendingClubPredictResponse(**scored.model_dump(), top_factors=explain(model, body, frame))

    @app.get("/v1/model", response_model=ModelInfo)
    def model_info(request: Request):
        _, metadata = loaded(request)
        return ModelInfo(
            model_id=metadata["model_id"],
            output=metadata["output"],
            recommended_threshold=metadata["recommended_threshold"],
            features=metadata["features"]["order"],
            library_versions=metadata["library_versions"],
        )

    @app.get("/health", response_model=Health)
    def health(request: Request, response: Response):
        metadata = request.app.state.metadata
        loaded_ids = sorted([*request.app.state.models, *request.app.state.lc_models])
        default_loaded = request.app.state.model is not None
        if not default_loaded and not loaded_ids:
            response.status_code = 503
            return Health(status="degraded", model_loaded=False, model_id=None, models_loaded=loaded_ids)
        return Health(status="ok", model_loaded=default_loaded,
                      model_id=metadata["model_id"] if default_loaded else None, models_loaded=loaded_ids)

    return app


app = create_app()
