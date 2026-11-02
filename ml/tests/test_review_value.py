import numpy as np
import pytest

from src.evaluation.review_value import band_money, break_even_catch, max_cost_per_review, reviewer_gain

RESULT = [300.0, 200.0, -1000.0, 100.0, -500.0, 400.0, -2000.0]
TARGET = [0, 0, 1, 0, 1, 0, 1]
P_BAD = [0.05, 0.10, 0.22, 0.25, 0.30, 0.31, 0.40]


@pytest.fixture
def money():
    return band_money(RESULT, TARGET, P_BAD, low=0.20, high=0.3286)


def test_band_money_splits_the_band_by_outcome(money):
    assert money["loans"] == 7
    assert money["band_loans"] == 4
    assert money["band_bad"] == 2
    assert money["band_good"] == 2
    assert money["band_bad_result"] == -1500.0
    assert money["band_good_result"] == 500.0


def test_band_money_totals_each_policy(money):
    assert money["approve_everyone"] == -2500.0
    assert money["threshold_only"] == -500.0
    assert money["reject_band"] == 500.0


def test_a_reviewer_who_rejects_nothing_changes_nothing(money):
    assert reviewer_gain(money, catch=0, false_reject=0) == 0


def test_reviewer_gain_counts_saved_losses_minus_lost_profit(money):
    assert reviewer_gain(money, catch=0.5, false_reject=0.2) == pytest.approx(0.5 * 1500 - 0.2 * 500)


def test_rejecting_the_whole_band_equals_the_reject_band_policy(money):
    assert money["threshold_only"] + reviewer_gain(money, catch=1, false_reject=1) == money["reject_band"]


def test_break_even_catch_makes_the_gain_pay_for_the_reviews(money):
    catch = break_even_catch(money, false_reject=0.2, cost_per_review=50)
    assert catch == pytest.approx((0.2 * 500 + 50 * 4) / 1500)
    assert reviewer_gain(money, catch, 0.2) == pytest.approx(50 * 4)


def test_break_even_above_one_means_no_reviewer_can_pay_for_it(money):
    assert break_even_catch(money, false_reject=1, cost_per_review=1000) > 1


def test_max_cost_per_review_is_the_gain_shared_over_reviewed_loans(money):
    assert max_cost_per_review(money, catch=0.6, false_reject=0.1) == pytest.approx((0.6 * 1500 - 0.1 * 500) / 4)


def test_outcomes_are_used_as_recorded_not_by_sign():
    money = band_money([150.0, -100.0], [1, 0], [0.25, 0.25], low=0.20, high=0.3286)
    assert money["band_bad_result"] == 150.0
    assert money["band_good_result"] == -100.0


@pytest.mark.parametrize("catch, false_reject", [(-0.1, 0), (1.1, 0), (0, -0.1), (0, 1.1), (np.nan, 0)])
def test_shares_outside_zero_to_one_are_refused(money, catch, false_reject):
    with pytest.raises(ValueError):
        reviewer_gain(money, catch, false_reject)


def test_a_band_without_losses_has_no_break_even():
    money = band_money([100.0, 200.0], [0, 0], [0.25, 0.25], low=0.20, high=0.3286)
    with pytest.raises(ValueError, match="no losses"):
        break_even_catch(money, false_reject=0)


def test_an_empty_band_has_no_cost_per_review():
    money = band_money([100.0], [0], [0.05], low=0.20, high=0.3286)
    with pytest.raises(ValueError, match="no loans"):
        max_cost_per_review(money, catch=0.5, false_reject=0)


def test_inputs_of_different_length_are_refused():
    with pytest.raises(ValueError, match="length"):
        band_money([1.0, 2.0], [0], [0.1, 0.2], low=0.20, high=0.3286)


def test_a_target_other_than_zero_or_one_is_refused():
    with pytest.raises(ValueError, match="0 or 1"):
        band_money([1.0], [2], [0.25], low=0.20, high=0.3286)


def test_a_band_with_its_edges_reversed_is_refused():
    with pytest.raises(ValueError, match="low"):
        band_money([1.0], [0], [0.25], low=0.3286, high=0.20)
