FROM python:3.13-slim

WORKDIR /app
COPY inference/requirements.txt inference/requirements.txt
RUN pip install --no-cache-dir -r inference/requirements.txt

COPY ml/src ml/src
COPY inference/app inference/app

ENV PYTHONPATH=/app/ml \
    MODELS_DIR=/models \
    MODEL_PATH=/models/credit_rf_isotonic_v1.joblib

WORKDIR /app/inference
EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
