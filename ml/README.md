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

Two datasets are used:

- **German Credit** (proof of concept): download `german.data` from the UCI
  Machine Learning Repository (Statlog German Credit Data) into `data/raw/`.
- **Lending Club** (the model the application uses): download "All Lending
  Club loan data" from Kaggle (`wordsforthewise/lending-club`) and put
  `accepted_2007_to_2018Q4.csv` and `rejected_2007_to_2018Q4.csv` in
  `data/raw/lending_club/`.

## Layout

```
data/raw/        source data, not committed
notebooks/       01 exploratory analysis, 03 thresholds and calibration, 04 robustness and review band
src/data/        loading and target definition (`load.py` German Credit, `lending_club.py`)
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

Lending Club, in order:

```bash
python scripts/profile_lending_club.py   # data profile, cost ratio from training loans
python scripts/run_lc_baseline.py        # 5-fold CV of the four candidates on 2012-2014
python scripts/run_lc_calibration.py     # calibrated CV and review bands
python scripts/train_lc_model.py         # fits and saves credit_lc_xgb_isotonic_v1
python scripts/evaluate_lc_holdout.py    # scores the 2015 loans once with the saved model
```

The Lending Club model is trained on 36-month loans issued 2012 to 2014 and
tested on loans issued in 2015, so it is always judged on a later year than
it learned from.
