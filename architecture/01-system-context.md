# 1. System context

Three kinds of bank staff use the system through a browser. The model is trained offline on public Lending Club loan data; the running system only reads the saved model file.

```mermaid
flowchart LR
    officer["Loan officer<br/>(Officer)"]
    manager["Branch manager<br/>(Manager)"]
    risk["Risk analyst<br/>(Risk)"]

    dss["Credit Scoring DSS<br/>Scores applications, recommends,<br/>records the officer's decision,<br/>keeps a searchable history"]

    lc[("Lending Club loan data<br/>2.26 M loans, 2007-2018<br/>public, Kaggle CC0")]
    training["Offline training<br/>(Python, run by hand)"]

    officer -- "enters applications,<br/>decides" --> dss
    manager -- "decides, watches<br/>the dashboard" --> dss
    risk -- "manages models,<br/>reads the dashboard" --> dss
    lc --> training
    training -- "model file<br/>(.joblib + .json)" --> dss
```

The system never connects to a credit bureau, core banking system or the internet at run time. Applicant data is typed in by the officer.
