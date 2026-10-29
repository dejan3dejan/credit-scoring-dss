import pandas as pd

from src.data.load import add_target


def test_bad_maps_to_one_and_raw_class_is_dropped():
    raw = pd.DataFrame({"age": [30, 40, 50], "raw_class": [1, 2, 1]})
    out = add_target(raw)
    assert out["target"].tolist() == [0, 1, 0]
    assert "raw_class" not in out.columns


def test_input_frame_is_not_modified():
    raw = pd.DataFrame({"age": [30, 40], "raw_class": [1, 2]})
    add_target(raw)
    assert list(raw.columns) == ["age", "raw_class"]
