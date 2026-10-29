import numpy as np

from src.evaluation.cv_evaluate import expected_cost


def test_missed_default_costs_five_times_a_false_alarm():
    one_fn = np.array([[5, 0], [1, 4]])
    one_fp = np.array([[4, 1], [0, 5]])
    assert expected_cost(one_fn) == 5 / 10
    assert expected_cost(one_fp) == 1 / 10


def test_perfect_classifier_costs_nothing():
    assert expected_cost(np.array([[7, 0], [0, 3]])) == 0
