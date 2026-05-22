# Measurement Data Flow

This document focuses on how the instrument software turns CCD and experiment inputs into usable Young's modulus records. It is based on the original operation guide and report.

## Inputs

The experiment page combines several data sources:

- applied load entered by the operator;
- CCD intensity data received through the selected serial port;
- camera preview for observing the instrument state;
- wire parameters: diameter, original length, and CCD-to-wire scale factor;
- experiment-state information such as current force, recorded points, and emergency-stop state.

## CCD Acquisition And Curve Quality

The operation guide describes a practical CCD adjustment loop:

1. Refresh the serial-port list.
2. Select the port used by the CCD module.
3. Start CCD acquisition and observe the real-time intensity curve.
4. If the curve bottom touches the chart boundary, reduce integration time.
5. If the upper/lower contrast is unclear, increase integration time.
6. Record data only after the optical feature is stable enough.

This gives the operator direct feedback before data is written into the measurement record.

## Feature Extraction

```text
CCD byte stream
  -> numeric intensity samples
  -> smoothing / boundary search
  -> optical feature width or boundary position
  -> deformation estimate through CCD scale factor
```

The project software keeps this process visible through the live curve. The goal is not only to compute a final number, but to let the user judge whether the acquisition state is reasonable.

## Measurement Record

For every recorded point, the application stores:

- applied force;
- CCD-derived length/deformation value;
- current wire and scale parameters;
- experiment record information for later recovery/export.

After at least two records are collected, the software estimates Young's modulus with least-squares fitting and reports fitting quality. This matches the teaching goal: students can see how additional measurement points affect the result.

## Export And Import

The software supports:

- exporting experiment images;
- exporting CCD raw data;
- exporting experiment records;
- importing historical data to recover previous results.

This makes the application useful not just for live measurement, but also for report writing, review, and teaching demonstration.

## Hardware-Independent Demos

The scripts in `src/` are small standalone demos:

- `simulated_ccd_reader.py`: generates CCD-like data and finds a synthetic peak.
- `uncertainty_budget.py`: computes a Young's modulus value and a simple uncertainty interval.

They do not replace the WinUI application or the physical instrument, but they make the data-processing idea easy to inspect without hardware.
