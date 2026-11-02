import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import pandas as pd

from src.data.lending_club import FEATURES, TEST_YEAR, load_dataset
from src.evaluation.review_value import band_money, break_even_catch, max_cost_per_review
from src.models.artifact import load_artifact

MODEL_PATH = Path(__file__).resolve().parents[1] / "models" / "credit_lc_xgb_isotonic_v1.joblib"
FALSE_REJECTS = (0.0, 0.05, 0.10, 0.20, 0.30)
CATCHES = (0.2, 0.4, 0.6, 0.8, 1.0)


def main():
    model, metadata = load_artifact(MODEL_PATH)
    threshold = metadata["recommended_threshold"]["value"]
    review_from = metadata["recommended_review_band"]["from"]

    df = load_dataset(with_result=True)
    test = df[df["issue_year"] == TEST_YEAR]
    p_bad = model.predict_proba(test[FEATURES])[:, 1]
    money = band_money(test["result"], test["target"], p_bad, review_from, threshold)
    n = money["loans"]

    print(f"model {metadata['model_id']}; test year {TEST_YEAR}; threshold {threshold:.4f}, review from {review_from}")
    print(f"{n} loans")
    print("result per loan = total paid back minus amount funded (USD), as recorded; no funding cost or discounting")

    print("\npolicy totals (USD):")
    for name in ("approve_everyone", "threshold_only", "reject_band"):
        print(f"  {name:17s} total {money[name]:15,.0f}  per applicant {money[name] / n:9.2f}")

    print(f"\nreview band: {money['band_loans']} loans ({money['band_loans'] / n:.4f}), "
          f"{money['band_bad']} bad, {money['band_good']} good")
    print(f"  bad loans' result  {money['band_bad_result']:15,.0f}  mean {money['band_bad_result'] / money['band_bad']:9.2f}")
    print(f"  good loans' result {money['band_good_result']:15,.0f}  mean {money['band_good_result'] / money['band_good']:9.2f}")
    print(f"  whole band         {money['band_bad_result'] + money['band_good_result']:15,.0f}")

    print("\nbreak-even catch rate (share of the band's bad loans a reviewer must reject), review cost 0:")
    for f in FALSE_REJECTS:
        print(f"  false rejects {f:.2f}: {break_even_catch(money, f):.4f}")

    print("\nmost a review may cost per reviewed loan (USD) before the band loses money:")
    grid = pd.DataFrame(
        [[max_cost_per_review(money, c, f) for f in FALSE_REJECTS] for c in CATCHES],
        index=[f"catch {c:.1f}" for c in CATCHES],
        columns=[f"false rej {f:.2f}" for f in FALSE_REJECTS],
    )
    print(grid.round(2).to_string())

    print("\nresult per loan by P(bad) decile:")
    deciles = pd.qcut(p_bad, 10, labels=False)
    table = test.assign(p_bad=p_bad, decile=deciles).groupby("decile").agg(
        mean_p_bad=("p_bad", "mean"), bad_rate=("target", "mean"),
        mean_result=("result", "mean"), total_result=("result", "sum"))
    print(table.round(4).to_string())


if __name__ == "__main__":
    main()
