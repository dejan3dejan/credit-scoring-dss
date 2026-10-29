import numpy as np
import pytest

from src.evaluation.cost_analysis import (
    acceptance_rate_policy,
    expected_cost_at,
    optimal_threshold,
    theoretical_threshold,
)


def test_theoretical_threshold_is_one_sixth_for_statlog_matrix():
    assert theoretical_threshold(cost_fn=5, cost_fp=1) == pytest.approx(1 / 6)


def test_theoretical_threshold_is_half_for_symmetric_costs():
    assert theoretical_threshold(cost_fn=1, cost_fp=1) == 0.5


def test_expected_cost_hand_computed():
    y = np.array([0, 0, 0, 1, 1])
    p = np.array([0.1, 0.3, 0.6, 0.2, 0.9])
    assert expected_cost_at(y, p, 0.5) == pytest.approx((5 * 1 + 1 * 1) / 5)
    assert expected_cost_at(y, p, 0.15) == pytest.approx(2 / 5)


def test_optimal_threshold_finds_cost_minimum():
    y = np.array([0, 0, 0, 1, 1])
    p = np.array([0.1, 0.3, 0.6, 0.2, 0.9])
    t, cost = optimal_threshold(y, p)
    assert t == pytest.approx(0.11)
    assert cost == pytest.approx(2 / 5)


def test_acceptance_policy_accepts_lowest_risk_share():
    y = np.array([0, 0, 1, 0, 1, 1, 0, 0, 1, 0])
    p = np.linspace(0.05, 0.95, 10)
    out = acceptance_rate_policy(y, p, 0.6)
    assert out["realised_acceptance"] == pytest.approx(0.6)
    assert out["bad_rate_among_accepted"] == pytest.approx(3 / 6)
