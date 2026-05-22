# Measurement Data Flow

## CCD Acquisition

1. The CCD module sends optical intensity data through the selected serial port.
2. The application parses the byte stream into numeric samples.
3. The chart is refreshed in real time so the operator can judge exposure and contrast.
4. The software identifies the optical feature boundary or characteristic size from the sampled curve.
5. The CCD-derived value is converted into wire deformation through the calibrated ratio.

## Experiment Record

For each recorded point, the application stores the applied force and the measured deformation. Once enough records are collected, the software estimates Young's modulus and fitting quality. CCD images, raw CCD data, and experiment records can be exported for later review.

## Demo Scripts

The Python scripts in `src/` provide small, hardware-independent examples for understanding CCD-like feature extraction and uncertainty calculation.
