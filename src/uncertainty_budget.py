# SPDX-License-Identifier: Apache-2.0
"""Uncertainty-budget demo for a Young's modulus measurement."""

from __future__ import annotations

import math
from dataclasses import dataclass


@dataclass(frozen=True)
class Measurement:
    force_n: float
    length_m: float
    diameter_m: float
    elongation_m: float


@dataclass(frozen=True)
class Uncertainty:
    force_n: float
    length_m: float
    diameter_m: float
    elongation_m: float


def youngs_modulus(measurement: Measurement) -> float:
    area = math.pi * (measurement.diameter_m / 2.0) ** 2
    return measurement.force_n * measurement.length_m / (area * measurement.elongation_m)


def relative_uncertainty(measurement: Measurement, uncertainty: Uncertainty) -> float:
    terms = [
        uncertainty.force_n / measurement.force_n,
        uncertainty.length_m / measurement.length_m,
        2.0 * uncertainty.diameter_m / measurement.diameter_m,
        uncertainty.elongation_m / measurement.elongation_m,
    ]
    return math.sqrt(sum(term * term for term in terms))


def main() -> int:
    measurement = Measurement(force_n=9.8, length_m=0.800, diameter_m=0.00052, elongation_m=0.000180)
    uncertainty = Uncertainty(force_n=0.02, length_m=0.001, diameter_m=0.000005, elongation_m=0.000003)
    modulus = youngs_modulus(measurement)
    rel = relative_uncertainty(measurement, uncertainty)
    print(f"E={modulus / 1e9:.3f} GPa")
    print(f"relative_uncertainty={rel:.2%}")
    print(f"expanded_interval=+/- {2 * rel * modulus / 1e9:.3f} GPa (k=2)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
