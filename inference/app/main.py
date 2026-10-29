import logging
import math
import os
from contextlib import asynccontextmanager
from pathlib import Path

import pandas as pd
from fastapi import FastAPI, Request, Response

from app.errors import ModelUnavailable, install_handlers
from app.schemas import CATEGORY_CODES, FEATURES, Health, ModelInfo, PredictRequest, PredictResponse
from src.models.artifact import load_artifact

logger = logging.getLogger("inference")

DEFAULT_MODEL_PATH = (
    Path(__file__).resolve().parents[2] / "ml" / "models" / "credit_rf_isotonic_v1.joblib"
)


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


def load_model(model_path):
    model, metadata = load_artifact(model_path)
    check_contract_matches(metadata)
    return model, metadata


def create_app(model_path=None) -> FastAPI:
    model_path = Path(model_path or os.environ.get("MODEL_PATH", DEFAULT_MODEL_PATH))

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        try:
            app.state.model, app.state.metadata = load_model(model_path)
            logger.info("loaded %s", app.state.metadata["model_id"])
        except Exception:
            logger.exception("model load failed for %s; scoring endpoints return 503", model_path)
            app.state.model, app.state.metadata = None, None
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
        X = pd.DataFrame([body.model_dump()], columns=FEATURES)
        p_bad = float(model.predict_proba(X)[0, 1])
        if not (math.isfinite(p_bad) and 0.0 <= p_bad <= 1.0):
            raise ValueError(f"model returned p_bad={p_bad}, outside [0, 1]")
        return PredictResponse(model_id=metadata["model_id"], p_bad=p_bad)

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
        if request.app.state.model is None:
            response.status_code = 503
            return Health(status="degraded", model_loaded=False, model_id=None)
        return Health(status="ok", model_loaded=True, model_id=metadata["model_id"])

    return app


app = create_app()
