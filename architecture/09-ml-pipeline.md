# 9. ML pipeline

## 9.1 From raw data to the served model

Run by hand, once per model version. The 2015 test year is kept apart and scored only after the model and its edges were fixed.

```mermaid
flowchart TD
    raw[("accepted_2007_to_2018Q4.csv<br/>2,260,668 loans")] --> target["add_target<br/>bad = Charged Off, Default<br/>good = Fully Paid<br/>drop loans still open"]
    target --> matured["matured<br/>term ended by 2018-12"]
    matured --> term["keep_term<br/>36-month loans only"]
    term --> prepare["prepare<br/>18 application-time inputs<br/>(FICO midpoint, years of credit history,<br/>employment years)"]
    prepare --> split{"split_by_time"}
    split -- "2012-2014<br/>306,462 loans" --> train["Training set"]
    split -- "2015<br/>283,026 loans" --> test["Test set<br/>(scored once)"]

    train --> cv["LC-02, LC-03<br/>5-fold cross-validation:<br/>4 candidates compared,<br/>calibration and review edge chosen"]
    cv --> fit["train_lc_model.py<br/>fit on all training loans"]
    fit --> art[("credit_lc_xgb_isotonic_v1<br/>.joblib + .json metadata")]
    art --> holdout["LC-04 test-year result<br/>LC-05 dollar value<br/>LC-07 benchmarks"]
    test --> holdout
    art --> serve["Inference service<br/>loads at start"]
```

## 9.2 What is inside the saved model

One scikit-learn object. Preprocessing is part of it, so the service applies exactly the transforms learned on the training data.

```mermaid
flowchart LR
    X["18 inputs<br/>(one row)"] --> ct

    subgraph pipe["One fold, of 5"]
        direction LR
        subgraph ct["ColumnTransformer"]
            direction TB
            lg["annual_inc, revol_bal:<br/>cap 0.5-99.5%, log1p,<br/>median impute + missing flag, scale"]
            nm["13 numeric inputs:<br/>cap 0.5-99.5%,<br/>median impute + missing flag, scale"]
            ct3["purpose, home_ownership,<br/>verification_status:<br/>one-hot, rare under 0.1% merged"]
        end
        xgb["XGBClassifier<br/>default settings, seed 42"]
        ct --> xgb
    end

    xgb --> iso["Isotonic calibration<br/>per fold"]
    iso --> avg["Average of 5 folds"]
    avg --> p["P(bad)"]
```

## 9.3 Inside one prediction (inference service)

```mermaid
flowchart TD
    req["POST /v3/models/{id}/predict<br/>JSON body"] --> pyd{"Pydantic validation<br/>types, ranges, codes"}
    pyd -- invalid --> e422["422 validation_error"]
    pyd -- valid --> find{"model id loaded?"}
    find -- no --> e404["404 model_not_found"]
    find -- yes --> frame["one-row DataFrame<br/>in training column order"]
    frame --> proba["predict_proba"]
    proba --> chk{"0 <= P(bad) <= 1<br/>and finite?"}
    chk -- no --> e500["500 internal_error, no score"]
    chk -- yes --> expl{"calibrated XGBoost?"}
    expl -- no --> nul["top_factors = null"]
    expl -- yes --> shap["TreeSHAP per fold<br/>(XGBoost pred_contribs),<br/>averaged, columns summed<br/>back into the 18 inputs"]
    shap --> top["top 5 by size,<br/>with the entered value"]
    nul --> resp["200: model_id, p_bad, top_factors"]
    top --> resp
```

The factors are in log-odds of the model's score before calibration: they show direction and relative size, not percentage points of P(bad).

## 9.4 Experiments and what each one decided

```mermaid
flowchart LR
    lc1["LC-01<br/>profile, cost ratio 2.0431,<br/>36-month only, split 2012-14 / 2015"] --> lc2["LC-02<br/>4 candidates, AUC about 0.65,<br/>threshold alone approves 97-100%"]
    lc2 --> lc3["LC-03<br/>calibration, review band,<br/>XGBoost slightly ahead"]
    lc3 --> lc4["LC-04<br/>test year once:<br/>AUC 0.672, 16% to review,<br/>30% of bad loans flagged"]
    lc4 --> lc5["LC-05<br/>dollars: threshold +$44k,<br/>reviewer must catch 1.27x<br/>more bad than good"]
    lc4 --> lc7["LC-07<br/>vs FICO 0.602,<br/>vs LendingClub rate 0.678"]
```
