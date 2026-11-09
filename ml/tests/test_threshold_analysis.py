import numpy as np
import pytest

from src.evaluation.cost_analysis import (
    THRESHOLD_GRID,
    cost_curve,
    cost_ratios,
    dollar_curve,
    edge_for_capacity,
    expected_cost_at,
    near_optimal_range,
    policy_cost_curve,
)
from src.evaluation.robustness import bootstrap_best_threshold

Y = np.array([0, 0, 0, 1, 1, 0, 1, 0])
P = np.array([0.05, 0.10, 0.20, 0.20, 0.35, 0.40, 0.60, 0.30])
RESULT = np.array([300.0, 200.0, 100.0, -1000.0, -500.0, 50.0, -2000.0, 150.0])
FUNDED = np.array([10000.0, 5000.0, 2000.0, 8000.0, 4000.0, 1000.0, 10000.0, 3000.0])


def test_cost_curve_matches_counting_at_each_threshold():
    curve = cost_curve(Y, P, cost_fn=2.0, cost_fp=1.0)
    expected = [expected_cost_at(Y, P, t, 2.0, 1.0) for t in THRESHOLD_GRID]
    assert list(curve["threshold"]) == list(THRESHOLD_GRID)
    assert curve["expected_cost"].to_numpy() == pytest.approx(expected)


def test_a_score_equal_to_the_threshold_is_rejected():
    cost = policy_cost_curve(np.array([0.2]), np.array([5.0]), np.array([1.0]), np.array([0.2]))
    assert cost == pytest.approx([1.0])


def test_policy_cost_curve_adds_approved_and_rejected_costs():
    cost = policy_cost_curve(P, 2.0 * Y, 1.0 * (1 - Y), np.array([0.25]))
    assert cost == pytest.approx([(2.0 * 1 + 1.0 * 2) / 8])


def test_weights_count_an_applicant_that_many_times():
    p = np.array([0.1, 0.5])
    weighted = policy_cost_curve(p, np.array([0.0, 3.0]), np.array([1.0, 0.0]), np.array([0.3, 0.6]),
                                 weights=np.array([2, 1]))
    repeated = policy_cost_curve(np.array([0.1, 0.1, 0.5]), np.array([0.0, 0.0, 3.0]), np.array([1.0, 1.0, 0.0]),
                                 np.array([0.3, 0.6]))
    assert weighted == pytest.approx(repeated)


def test_dollar_curve_sums_the_results_of_approved_loans():
    curve = dollar_curve(RESULT, P, np.array([0.0, 0.25, 1.0]))
    assert curve["result_per_applicant"].to_numpy() == pytest.approx([0.0, (300 + 200 + 100 - 1000) / 8,
                                                                       RESULT.sum() / 8])
    assert curve["share_approved"].to_numpy() == pytest.approx([0.0, 4 / 8, 1.0])


def test_near_optimal_range_spans_every_threshold_within_tolerance():
    thresholds = np.array([0.1, 0.2, 0.3, 0.4, 0.5])
    costs = np.array([1.20, 1.009, 1.00, 1.011, 1.005])
    assert near_optimal_range(thresholds, costs, tolerance=0.01) == (0.2, 0.5)


def test_near_optimal_range_uses_the_size_of_a_negative_minimum():
    thresholds = np.array([0.1, 0.2, 0.3])
    costs = np.array([-100.0, -99.5, -98.0])
    assert near_optimal_range(thresholds, costs, tolerance=0.01) == (0.1, 0.2)


def test_cost_ratios_from_means_medians_and_dollars_lent():
    out = cost_ratios(RESULT, Y, FUNDED)
    good, bad = Y == 0, Y == 1
    assert out["mean"] == pytest.approx(-RESULT[bad].mean() / RESULT[good].mean())
    assert out["median"] == pytest.approx(-np.median(RESULT[bad]) / np.median(RESULT[good]))
    loss_per_dollar = -RESULT[bad].sum() / FUNDED[bad].sum()
    gain_per_dollar = RESULT[good].sum() / FUNDED[good].sum()
    assert out["per_dollar"] == pytest.approx(loss_per_dollar / gain_per_dollar)


def test_cost_ratios_need_both_outcomes():
    with pytest.raises(ValueError, match="both"):
        cost_ratios(RESULT[:3], Y[:3], FUNDED[:3])


def test_edge_for_capacity_picks_the_widest_band_that_fits():
    p = np.array([0.05, 0.12, 0.15, 0.18, 0.22, 0.26, 0.31, 0.40, 0.50, 0.60])
    grid = np.round(np.arange(0.10, 0.31, 0.01), 2)
    assert edge_for_capacity(p, high=0.3286, capacity=0.3, grid=grid) == pytest.approx(0.19)
    assert edge_for_capacity(p, high=0.3286, capacity=0.1, grid=grid) == pytest.approx(0.27)


def test_edge_for_capacity_refuses_when_no_edge_fits():
    p = np.array([0.25, 0.25, 0.25, 0.05])
    with pytest.raises(ValueError, match="capacity"):
        edge_for_capacity(p, high=0.3286, capacity=0.1, grid=np.array([0.20, 0.24]))


def test_bootstrap_best_threshold_is_reproducible_and_on_the_grid():
    rng = np.random.default_rng(0)
    p = rng.random(500)
    y = (rng.random(500) < p).astype(int)
    grid = np.round(np.arange(0.01, 1.0, 0.01), 2)
    a = bootstrap_best_threshold(p, 2.0 * y, 1.0 * (1 - y), grid, n_boot=50, seed=1)
    b = bootstrap_best_threshold(p, 2.0 * y, 1.0 * (1 - y), grid, n_boot=50, seed=1)
    assert a.shape == (50,)
    assert np.array_equal(a, b)
    assert np.isin(a, grid).all()


def test_bootstrap_of_a_clear_cut_finds_it_every_time():
    p = np.r_[np.full(200, 0.1), np.full(200, 0.9)]
    y = np.r_[np.zeros(200, dtype=int), np.ones(200, dtype=int)]
    grid = np.array([0.05, 0.5, 0.95])
    best = bootstrap_best_threshold(p, 2.0 * y, 1.0 * (1 - y), grid, n_boot=20, seed=0)
    assert (best == 0.5).all()
