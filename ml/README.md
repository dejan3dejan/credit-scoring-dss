# credit-scoring-ml

Data and model work for a credit scoring decision support system, built on
the Statlog German Credit dataset. The output is a calibrated model that
returns the probability of default for one applicant.

## Setup

```bash
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
```

Download `german.data` from the UCI Machine Learning Repository (Statlog
German Credit Data) and place it in `data/raw/`.

## Layout

```
data/raw/        source data, not committed
notebooks/       01 exploratory analysis, 03 thresholds and calibration, 04 robustness and review band
src/data/        loading and target definition
src/features/    preprocessing pipeline
src/models/      model pipelines, GLM inference, saved-model helpers
src/evaluation/  split, cross-validation, cost analysis, calibration
scripts/         baseline CV, final holdout evaluation, model training
models/          saved model and metadata, not committed
tests/
```

## Usage

```bash
python -m pytest -q
python scripts/run_baseline_experiments.py
python scripts/train_final_model.py
```

`scripts/evaluate_holdout.py` scores the held-out test set and is run once,
after model selection.
