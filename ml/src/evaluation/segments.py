import numpy as np
import pandas as pd
from sklearn.metrics import roc_auc_score


def _aligned(target, p_bad):
    target, p_bad = np.asarray(target, dtype=float), np.asarray(p_bad, dtype=float)
    if len(target) != len(p_bad):
        raise ValueError("target and p_bad must have the same length")
    return target, p_bad


def expected_calibration_error(target, p_bad, bins=10):
    target, p_bad = _aligned(target, p_bad)
    bin_of = pd.qcut(p_bad, bins, labels=False, duplicates="drop")
    frame = pd.DataFrame({"y": target, "p": p_bad, "bin": bin_of}).groupby("bin").agg(
        y=("y", "mean"), p=("p", "mean"), n=("y", "size"))
    return float((frame["n"] * (frame["p"] - frame["y"]).abs()).sum() / frame["n"].sum())


def segment_table(target, p_bad, groups, min_rows=200):
    target, p_bad = _aligned(target, p_bad)
    frame = pd.DataFrame({"y": target, "p": p_bad, "group": np.asarray(groups)})

    def summary(rows):
        rankable = len(rows) >= min_rows and rows["y"].nunique() == 2
        return pd.Series({
            "n": len(rows),
            "bad_rate": rows["y"].mean(),
            "mean_p_bad": rows["p"].mean(),
            "gap": rows["p"].mean() - rows["y"].mean(),
            "auc": roc_auc_score(rows["y"], rows["p"]) if rankable else np.nan,
        })

    return frame.groupby("group", observed=True)[["y", "p"]].apply(summary)


def standardised_mean_difference(reference: pd.DataFrame, other: pd.DataFrame) -> pd.Series:
    spread = reference.std()
    shift = other.mean() - reference.mean()
    return (shift / spread.where(spread > 0)).fillna(0.0)
