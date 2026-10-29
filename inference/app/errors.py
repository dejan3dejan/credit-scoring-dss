from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from app.schemas import CATEGORY_CODES


class ModelUnavailable(Exception):
    pass


def error_response(status, code, message, details=None):
    error = {"code": code, "message": message}
    if details is not None:
        error["details"] = details
    return JSONResponse(status_code=status, content={"error": error})


def _field(loc):
    if len(loc) > 1 and isinstance(loc[1], str):
        return loc[1]
    return "body"


def _issue(field, err):
    kind, ctx = err["type"], err.get("ctx", {})
    if kind == "missing":
        return "field required"
    if kind == "extra_forbidden":
        return "unknown field"
    if kind == "literal_error" and field in CATEGORY_CODES:
        return f"unknown code {err['input']!r}; allowed: {', '.join(CATEGORY_CODES[field])}"
    if kind == "int_type":
        return "must be an integer"
    if kind == "greater_than_equal":
        return f"must be >= {ctx['ge']}"
    if kind == "less_than_equal":
        return f"must be <= {ctx['le']}"
    if kind == "json_invalid":
        return "malformed JSON"
    if kind in ("model_type", "model_attributes_type", "dict_type"):
        return "request body must be a JSON object"
    return err["msg"]


def validation_details(errors):
    details = []
    for err in errors:
        field = _field(err["loc"])
        details.append({"field": field, "issue": _issue(field, err)})
    return details


def install_handlers(app: FastAPI):
    @app.exception_handler(RequestValidationError)
    async def on_validation_error(request: Request, exc: RequestValidationError):
        return error_response(422, "validation_error", "Request failed validation",
                              validation_details(exc.errors()))

    @app.exception_handler(ModelUnavailable)
    async def on_model_unavailable(request: Request, exc: ModelUnavailable):
        return error_response(503, "model_unavailable", "Model is not loaded; the service cannot score")

    @app.exception_handler(Exception)
    async def on_unexpected(request: Request, exc: Exception):
        return error_response(500, "internal_error", "Unexpected error during scoring")
