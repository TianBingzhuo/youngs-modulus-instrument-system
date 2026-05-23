"""Synthetic CCD intensity reader for a public portfolio demo."""

import math


def generate_line(length: int = 64, center: float = 31.5, width: float = 6.0) -> list[float]:
    values: list[float] = []
    for i in range(length):
        signal = math.exp(-((i - center) ** 2) / (2 * width**2))
        background = 0.05
        values.append(background + signal)
    return values


def estimate_peak(values: list[float]) -> int:
    return max(range(len(values)), key=lambda i: values[i])


def main() -> None:
    values = generate_line()
    peak = estimate_peak(values)
    print(f"synthetic_peak_index={peak}")
    print("first_10_samples=" + ", ".join(f"{v:.3f}" for v in values[:10]))


if __name__ == "__main__":
    main()

