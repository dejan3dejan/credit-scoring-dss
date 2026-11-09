import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import pandas as pd

EXPERIMENTS = Path(__file__).resolve().parents[1] / "experiments"
PANELS = (("log_loss", "log loss (lower is better)"), ("roc_auc", "ROC-AUC"), ("expected_cost", "expected cost at 0.3286"))


def main():
    folds = pd.read_csv(EXPERIMENTS / "lc09_search_folds.csv")
    default = folds[folds["config"] == "default"].set_index("fold")
    diffs = folds.assign(**{m: folds[m] - folds["fold"].map(default[m]) for m, _ in PANELS})
    grouped = diffs.groupby("config")[[m for m, _ in PANELS]]
    means, stds = grouped.mean(), grouped.std(ddof=0)
    order = means["log_loss"].sort_values().index

    fig, axes = plt.subplots(1, 3, figsize=(13, 6), sharey=True)
    for ax, (metric, label) in zip(axes, PANELS):
        mean = means.loc[order, metric]
        std = stds.loc[order, metric]
        colors = ["tab:red" if c == "default" else "black" for c in order]
        ax.errorbar(mean, range(len(order)), xerr=std, fmt="none", ecolor="grey", lw=1)
        ax.scatter(mean, range(len(order)), c=colors, s=14, zorder=3)
        ax.axvline(0, color="tab:red", ls="--", lw=0.8)
        ax.set_xlabel(f"{label}: config minus default")
        ax.locator_params(axis="x", nbins=5)
    axes[0].set_yticks(range(len(order)), order)
    axes[0].invert_yaxis()
    fig.suptitle("LC-09 random search, paired per-fold differences against the default (mean +- std over 5 folds)")
    fig.tight_layout()
    fig.savefig(EXPERIMENTS / "figures" / "lc09_search.png", dpi=150)
    plt.close(fig)


if __name__ == "__main__":
    main()
