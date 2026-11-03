# 10. Data flow

How one application's data changes shape on its way through the system and back. Each box names the type or table that holds it.

```mermaid
flowchart TB
    form["HTML form<br/>New.cshtml"] -- "form post" --> req["CreateApplicationRequest<br/>(DTO, validated)"]
    req -- "ToDomain + client<br/>+ user from cookie" --> app["CreditApplication<br/>(domain, USD, months)"]
    app -- "LendingClubMapper" --> mlreq["MlPredictRequest<br/>18 contract fields<br/>(loan_amnt, fico, ...)"]
    mlreq -- "JSON over HTTP" --> pyreq["LendingClubRequest<br/>(Pydantic)"]
    pyreq --> df["pandas DataFrame<br/>1 row"]
    df -- "saved pipeline" --> p["P(bad) + contributions<br/>per model column"]
    p -- "summed per input,<br/>top 5" --> pyresp["LendingClubPredictResponse<br/>p_bad, top_factors"]
    pyresp -- "JSON" --> mlscore["MlScore + MlFactor list<br/>(checked: finite, known fields)"]
    mlscore -- "InputOf: model name<br/>to domain name" --> dec["CreditDecision<br/>+ ScoreFactor ranks 1-5<br/>+ Recommendation"]
    dec --> tables[("credit_applications<br/>credit_decisions<br/>score_factors<br/>shadow_scores")]
    tables --> resp["ApplicationResponse<br/>(DTO)"]
    resp --> page["Details page:<br/>gauge, factors, checklist"]
    page -- "officer decides" --> od[("officer_decisions")]
    tables --> hist["History and dashboard<br/>(queries, nothing copied)"]
    od --> hist
```

## What crosses each boundary

| Boundary | Carries | Does not carry |
|---|---|---|
| Browser to web app | form fields, sign-in cookie | the user's name or role (taken from the cookie) |
| Web app to inference | 18 numeric and coded inputs | client name, client number, any id, the user |
| Inference to web app | `p_bad`, `model_id`, up to 5 `{feature, value, contribution}` | a recommendation (the web app decides that) |
| Web app to database | everything, normalised | computed totals (always queried) |

The inference service never sees who the applicant is: the name and client number stay in the web application.

## Training data flow (offline)

```mermaid
flowchart LR
    kaggle[("Kaggle download<br/>by the author")] --> raw[("ml/data/raw<br/>not in git")]
    raw --> scripts["ml/scripts<br/>profile, compare, train, evaluate"]
    scripts --> log["ml/experiments<br/>outputs, experiment log"]
    scripts --> model[("ml/models<br/>artifact + metadata")]
    model --> inf["inference service"]
```
