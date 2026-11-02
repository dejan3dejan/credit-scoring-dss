import numpy as np


def band_money(result, target, p_bad, low, high):
    result, target, p_bad = (np.asarray(a, dtype=float) for a in (result, target, p_bad))
    if not len(result) == len(target) == len(p_bad):
        raise ValueError("result, target and p_bad must have the same length")
    if not np.isin(target, (0, 1)).all():
        raise ValueError("target must be 0 or 1")
    if not low < high:
        raise ValueError(f"low {low} must be below high {high}")

    reject = p_bad >= high
    band = (p_bad >= low) & ~reject
    bad, good = band & (target == 1), band & (target == 0)
    return {
        "loans": len(result),
        "band_loans": int(band.sum()),
        "band_bad": int(bad.sum()),
        "band_good": int(good.sum()),
        "band_bad_result": float(result[bad].sum()),
        "band_good_result": float(result[good].sum()),
        "approve_everyone": float(result.sum()),
        "threshold_only": float(result[~reject].sum()),
        "reject_band": float(result[~reject & ~band].sum()),
    }


def _share(name, value):
    if not 0 <= value <= 1:
        raise ValueError(f"{name} must be between 0 and 1, got {value}")


def reviewer_gain(money, catch, false_reject):
    _share("catch", catch)
    _share("false_reject", false_reject)
    return -catch * money["band_bad_result"] - false_reject * money["band_good_result"]


def break_even_catch(money, false_reject, cost_per_review=0.0):
    _share("false_reject", false_reject)
    loss = -money["band_bad_result"]
    if loss <= 0:
        raise ValueError("the band has no losses to avoid")
    return (false_reject * money["band_good_result"] + cost_per_review * money["band_loans"]) / loss


def max_cost_per_review(money, catch, false_reject):
    if money["band_loans"] == 0:
        raise ValueError("the band has no loans")
    return reviewer_gain(money, catch, false_reject) / money["band_loans"]
