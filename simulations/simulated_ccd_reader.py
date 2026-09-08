# SPDX-FileCopyrightText: 2026 Tian Bingzhuo and project contributors
# SPDX-License-Identifier: Apache-2.0
"""Synthetic dark-valley example following ExperimentPage.AnalyzeBoundaryPoints.

No serial port is opened. This is a later explanation of the C# rule, not
competition firmware, a signal-quality test, or a physical calibration.
"""
from __future__ import annotations

import argparse
import csv
from dataclasses import dataclass
from pathlib import Path

FRAME_SIZE = 3648
PIXEL_SIZE_UM = 8.0


@dataclass(frozen=True)
class Boundary:
    left: int
    right: int
    threshold: float

    @property
    def width_mm(self) -> float:
        return abs(self.right - self.left) * PIXEL_SIZE_UM / 1000.0


def generate_line() -> list[int]:
    """Return one synthetic 8-bit frame with a rectangular dark region."""
    return [40 if 1400 <= i <= 2200 else 200 for i in range(FRAME_SIZE)]


def analyze_boundary(values: list[int]) -> Boundary:
    """Follow the original centered smoothing, threshold and fallback rules."""
    if len(values) != FRAME_SIZE or any(type(v) is not int or not 0 <= v <= 255 for v in values):
        raise ValueError("Expected exactly 3648 integer intensities in 0..255.")
    # C# SmoothData(data, 10) includes both ends: up to 11 samples, not 10.
    smoothed = []
    for i in range(len(values)):
        window = values[max(0, i - 5):min(len(values), i + 6)]
        smoothed.append(sum(window) / len(window))
    first_average = sum(smoothed) / len(smoothed)
    below_average = [v for v in smoothed if v <= first_average]
    threshold = sum(below_average) / len(below_average) + 5
    low = [i for i, value in enumerate(smoothed) if value <= threshold]
    if len(low) >= 4:
        left, right = low[1], low[-2]
    elif len(low) >= 2:
        left, right = low[0], low[-1]
    else:
        minimum = smoothed.index(min(smoothed))
        left, right = max(0, minimum - 100), min(len(values) - 1, minimum + 100)
    if left >= right:
        center = len(values) // 2
        left, right = max(0, center - 50), min(len(values) - 1, center + 50)
    return Boundary(left, right, threshold)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--csv", type=Path, help="Write a new synthetic CCD CSV; never overwrite existing files.")
    args = parser.parse_args()
    values = generate_line()
    boundary = analyze_boundary(values)
    print("SYNTHETIC DEMO ONLY - no instrument measurement")
    print(f"left={boundary.left}, right={boundary.right}, width_mm={boundary.width_mm:.3f}")
    if args.csv:
        with args.csv.open("x", encoding="utf-8", newline="") as stream:
            writer = csv.writer(stream)
            writer.writerow(["pixelIndex", "intensity"])
            writer.writerows(enumerate(values))
        print(f"Synthetic CSV: {args.csv}")


if __name__ == "__main__":
    main()
