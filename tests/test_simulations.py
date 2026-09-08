# SPDX-License-Identifier: Apache-2.0
"""Hardware-free regression tests. All samples are synthetic."""
import math
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "simulations"))
from simulated_ccd_reader import FRAME_SIZE, analyze_boundary, generate_line
from uncertainty_budget import Measurement, Uncertainty, relative_uncertainty, youngs_modulus


class BoundaryTests(unittest.TestCase):
    def test_dark_valley(self):
        result = analyze_boundary(generate_line())
        self.assertEqual((result.left, result.right), (1406, 2194))
        self.assertAlmostEqual(result.width_mm, 6.304)

    def test_translation(self):
        values = generate_line()
        a, b = analyze_boundary(values), analyze_boundary([200] * 31 + values[:-31])
        self.assertEqual((b.left - a.left, b.right - a.right), (31, 31))

    def test_flat_signal_preserves_rule_not_measurement_validity(self):
        for value in (0, 40, 255):
            result = analyze_boundary([value] * FRAME_SIZE)
            self.assertEqual((result.left, result.right), (1, FRAME_SIZE - 2))

    def test_reject_invalid_frame(self):
        for values in ([], [0] * 64, [0] * (FRAME_SIZE + 1), [-1] * FRAME_SIZE,
                       [256] * FRAME_SIZE, [math.nan] * FRAME_SIZE, [True] * FRAME_SIZE):
            with self.assertRaises(ValueError):
                analyze_boundary(values)

    def test_extreme_signal(self):
        values = [255] * FRAME_SIZE
        values[0] = 0
        result = analyze_boundary(values)
        self.assertTrue(0 <= result.left < result.right < FRAME_SIZE)


class UncertaintyTests(unittest.TestCase):
    def test_known_formula(self):
        m = Measurement(1, 1, 1, 1)
        self.assertAlmostEqual(youngs_modulus(m), 4 / math.pi)
        self.assertAlmostEqual(relative_uncertainty(m, Uncertainty(.1, .1, .1, .1)), math.sqrt(.07))

    def test_invalid_measurement(self):
        for v in (0, -1, math.nan, math.inf):
            with self.assertRaises(ValueError):
                youngs_modulus(Measurement(1, 1, v, 1))

    def test_invalid_uncertainty(self):
        m = Measurement(1, 1, 1, 1)
        for v in (-1, math.nan, math.inf):
            with self.assertRaises(ValueError):
                relative_uncertainty(m, Uncertainty(v, 0, 0, 0))
        self.assertEqual(relative_uncertainty(m, Uncertainty(0, 0, 0, 0)), 0)


if __name__ == "__main__":
    unittest.main()
