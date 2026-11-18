import json
import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import numpy as np
import pandas as pd

from src.data import lending_club as lc
from src.data.demo_cases import pick_cases, to_answers
from src.models.artifact import load_artifact

ROOT = Path(__file__).resolve().parents[2]
MODEL_PATH = ROOT / "ml" / "models" / "credit_lc_xgb_isotonic_v1.joblib"
OUT_PATH = ROOT / "demo" / "cases.json"
SIZE = 40
SEED = 42

FIRST_NAMES = ["Ana", "Marko", "Jelena", "Nikola", "Milica", "Stefan", "Ivana", "Luka", "Jovana", "Miloš",
               "Tamara", "Nemanja", "Marija", "Aleksandar", "Teodora", "Vuk", "Sanja", "Filip", "Katarina", "Dušan"]
LAST_NAMES = ["Petrović", "Jovanović", "Nikolić", "Marković", "Đorđević", "Stojanović", "Ilić", "Pavlović",
              "Milošević", "Popović", "Kostić", "Lazić", "Tomić", "Radović", "Živković", "Savić", "Mitić", "Vasić"]


def main():
    model, metadata = load_artifact(MODEL_PATH)
    threshold = metadata["recommended_threshold"]["value"]
    review_from = metadata["recommended_review_band"]["from"]

    df = lc.load_dataset()
    _, X_test, _, y_test = lc.split_by_time(df, lc.TEST_YEAR, lc.TRAIN_FROM)
    p_bad = pd.Series(model.predict_proba(X_test)[:, 1], index=X_test.index)

    picked = pick_cases(X_test, p_bad, threshold, review_from, size=SIZE, seed=SEED)
    rng = np.random.default_rng(SEED)
    names = set()
    while len(names) < len(picked):
        names.add(f"{rng.choice(FIRST_NAMES)} {rng.choice(LAST_NAMES)}")

    cases = []
    for (index, row), name in zip(picked.iterrows(), sorted(names, key=lambda n: rng.random())):
        cases.append({
            "name": name,
            "yearsOldAtFirstCredit": int(rng.integers(19, 31)),
            "answers": to_answers(row),
            "expected": {"pBad": round(float(row["p_bad"]), 4), "band": row["band"]},
            "observedOutcome": "Defaulted" if y_test.loc[index] == 1 else "Repaid",
        })

    bands = picked["band"].value_counts()
    document = {
        "source": f"{len(cases)} loans issued in {lc.TEST_YEAR}, drawn at random (seed {SEED}) from the {lc.TEST_YEAR} test year "
                  f"after leaving out loans the application form cannot take; when the draw has no Reject, its last loan is "
                  f"swapped for a random Reject so the demo can show one. Names and birth dates are invented.",
        "modelId": metadata["model_id"],
        "threshold": threshold,
        "reviewFrom": review_from,
        "bands": {band: int(bands.get(band, 0)) for band in ["Approve", "Review", "Reject"]},
        "testYearBands": {band: round(float(share), 4) for band, share in
                          pd.cut(p_bad, [-1, review_from, threshold, 2], right=False,
                                 labels=["Approve", "Review", "Reject"]).value_counts(normalize=True).items()},
        "cases": cases,
    }
    OUT_PATH.parent.mkdir(exist_ok=True)
    OUT_PATH.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {OUT_PATH} with {len(cases)} cases; bands {document['bands']}; test year {document['testYearBands']}")
    print(f"observed defaults among the cases: {sum(c['observedOutcome'] == 'Defaulted' for c in cases)}")


if __name__ == "__main__":
    main()
