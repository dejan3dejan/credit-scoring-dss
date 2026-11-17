import numpy as np
import pandas as pd
import pytest

from src.evaluation.segments import expected_calibration_error, segment_table, standardised_mean_difference


def test_ece_is_zero_when_every_bin_matches_its_observed_rate():
    target = [0, 0, 0, 1, 0, 1, 1, 1]
    p_bad = [0.25, 0.25, 0.25, 0.25, 0.75, 0.75, 0.75, 0.75]
    assert expected_calibration_error(target, p_bad, bins=2) == pytest.approx(0.0)


def test_ece_weights_each_bin_gap_by_its_size():
    target = [0, 0, 0, 0, 1, 1, 1, 1]
    p_bad = [0.1, 0.1, 0.1, 0.1, 0.6, 0.6, 0.6, 0.6]
    assert expected_calibration_error(target, p_bad, bins=2) == pytest.approx(0.5 * 0.1 + 0.5 * 0.4)


def test_ece_refuses_mismatched_lengths():
    with pytest.raises(ValueError):
        expected_calibration_error([0, 1], [0.5], bins=2)


def test_segment_table_reports_size_rates_gap_and_auc():
    target = [0, 1, 0, 1, 0, 0]
    p_bad = [0.1, 0.9, 0.2, 0.8, 0.3, 0.4]
    groups = ["a", "a", "a", "a", "b", "b"]

    table = segment_table(target, p_bad, groups, min_rows=1)

    assert table.loc["a", "n"] == 4
    assert table.loc["a", "bad_rate"] == pytest.approx(0.5)
    assert table.loc["a", "mean_p_bad"] == pytest.approx(0.5)
    assert table.loc["a", "gap"] == pytest.approx(0.0)
    assert table.loc["a", "auc"] == pytest.approx(1.0)
    assert np.isnan(table.loc["b", "auc"])


def test_segment_table_leaves_auc_out_for_small_groups():
    table = segment_table([0, 1, 0, 1], [0.1, 0.9, 0.2, 0.8], ["a"] * 4, min_rows=5)
    assert np.isnan(table.loc["a", "auc"])


def test_standardised_mean_difference_uses_the_reference_spread():
    before = pd.DataFrame({"x": [0.0, 2.0, 4.0], "y": [1.0, 1.0, 1.0]})
    after = pd.DataFrame({"x": [2.0, 4.0, 6.0], "y": [1.0, 1.0, 1.0]})

    smd = standardised_mean_difference(before, after)

    assert smd["x"] == pytest.approx(1.0)
    assert smd["y"] == 0.0
