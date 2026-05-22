# Data Pipeline Draft

## Data Flow

1. Sensor source produces a line or frame of intensity data.
2. The parser converts raw values into numeric samples.
3. The feature extractor finds a peak, edge, or fitted position.
4. The calculation layer updates displacement and material parameters.
5. The record layer stores parameters, result, and notes.

## Future Demo

Use synthetic CCD-like intensity data to demonstrate peak detection and record generation without real hardware or private project data.

