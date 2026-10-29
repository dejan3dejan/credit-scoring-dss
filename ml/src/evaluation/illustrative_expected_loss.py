import numpy as np
import pandas as pd

ASSUMED_LGD_SCENARIOS = {"low": 0.25, "mid": 0.45, "high": 0.65}


def illustrative_el_table(pd_by_source, ead, lgd_scenarios=ASSUMED_LGD_SCENARIOS):
    ead = np.asarray(ead, dtype=float)
    rows = []
    for source, pd_i in pd_by_source.items():
        row = {"pd_source": source}
        for scenario, lgd in lgd_scenarios.items():
            el = np.asarray(pd_i, dtype=float) * lgd * ead
            row[f"illustrative_total_EL_DM_assumed_LGD_{scenario}_{lgd}"] = el.sum()
            row[f"illustrative_mean_EL_DM_assumed_LGD_{scenario}_{lgd}"] = el.mean()
        rows.append(row)
    return pd.DataFrame(rows)
