"""Tests for the region check of POST /download in app.py. No server is asked and the layer list is not loaded.

    python -m pytest Voice/search
"""

import app


def test_a_region_with_nan_or_infinity_is_refused_in_a_sentence():
    nan = float("nan")
    infinity = float("inf")
    assert "finite" in app.region_problem([nan, 3137432.0, 3965925.0, 3291676.0])
    assert "finite" in app.region_problem([3797132.0, 3137432.0, infinity, 3291676.0])


def test_a_good_region_has_no_problem_and_a_backwards_one_does():
    assert app.region_problem([3797132.0, 3137432.0, 3965925.0, 3291676.0]) == ""
    assert "min smaller than its max" in app.region_problem([3965925.0, 3137432.0, 3797132.0, 3291676.0])
    assert "4 numbers" in app.region_problem([1.0, 2.0, 3.0])
