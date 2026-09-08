# SPDX-License-Identifier: Apache-2.0
"""Synthetic single-point uncertainty example, independent of the C# fit."""

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


def validate_measurement(measurement: Measurement) -> None:
    if any(not math.isfinite(v) or v <= 0 for v in (
        measurement.force_n, measurement.length_m, measurement.diameter_m, measurement.elongation_m
    )):
        raise ValueError("Measurement values must be finite and positive in SI units.")


def youngs_modulus(measurement: Measurement) -> float:
    validate_measurement(measurement)
    area = math.pi * (measurement.diameter_m / 2.0) ** 2
    return measurement.force_n * measurement.length_m / (area * measurement.elongation_m)


def relative_uncertainty(measurement: Measurement, uncertainty: Uncertainty) -> float:
    validate_measurement(measurement)
    if any(not math.isfinite(v) or v < 0 for v in (
        uncertainty.force_n, uncertainty.length_m, uncertainty.diameter_m, uncertainty.elongation_m
    )):
        raise ValueError("Standard uncertainties must be finite and nonnegative.")
    terms = [
        uncertainty.force_n / measurement.force_n,
        uncertainty.length_m / measurement.length_m,
        2.0 * uncertainty.diameter_m / measurement.diameter_m,
        uncertainty.elongation_m / measurement.elongation_m,
    ]
    return math.sqrt(sum(term * term for term in terms))


def main() -> int:
    print("SYNTHETIC DEMO ONLY - independent-input first-order propagation")
    measurement = Measurement(force_n=9.8, length_m=0.800, diameter_m=0.00052, elongation_m=0.000180)
    uncertainty = Uncertainty(force_n=0.02, length_m=0.001, diameter_m=0.000005, elongation_m=0.000003)
    modulus = youngs_modulus(measurement)
    rel = relative_uncertainty(measurement, uncertainty)
    print(f"E={modulus / 1e9:.3f} GPa")
    print(f"relative_uncertainty={rel:.2%}")
    print(f"expanded_uncertainty=+/- {2 * rel * modulus / 1e9:.3f} GPa (k=2)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
