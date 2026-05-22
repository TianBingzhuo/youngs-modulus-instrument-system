# Measurement Principle And Instrument Design

This document expands the measurement principle from the original project report. The complete extracted report is available in `docs/project-report-extract.md`, and the original Word report is preserved in `docs/source-materials/youngs-modulus-instrument-report.docx`.

## Problem Background

Young's modulus measurement is a classic college physics experiment. It connects Hooke's law, material mechanics, length measurement, optical amplification, and error analysis. In traditional teaching instruments, the measurement is meaningful but the operation often has several pain points:

- **Insufficient stability:** the traditional frame can shake during loading and adjustment, causing visible data fluctuation.
- **Difficult operation:** students often spend too much time adjusting the telescope, mirror angle, and reading device before they can obtain usable data.
- **Invisible optical path:** the optical-lever principle is abstract, and students may know the formula without forming a clear physical image.
- **Manual reading error:** subjective reading and repeated manual calculation introduce avoidable uncertainty.

The project responds to these problems by improving the mechanical frame, integrating two optical measurement routes, adding CCD-based reading, and designing a remote pre-lab workflow.

## Overall Measurement Chain

```text
loading force
  -> wire deformation
  -> optical amplification
  -> folded optical path / projection path
  -> microscope reading or CCD feature extraction
  -> displacement estimation
  -> Young's modulus calculation
  -> uncertainty and fitting-quality analysis
```

## Mechanical Structure

The instrument uses an aluminum-profile triangular frame as the main support. The wire is fixed by customized clamps, and the loading process is stabilized by a pulley-based mechanism instead of direct weight loading. This structure is intended to reduce wire shaking and improve repeatability.

The instrument body includes:

- metal wire and wire-locking fixtures;
- fixed pulley groups and loading components;
- movable L-shaped platforms coupled to wire elongation;
- leveling knobs and plumb-line observation for horizontal adjustment;
- optical modules for folded optical path and projection measurement.

## Folded Optical Path Method

The folded optical path method is derived from the traditional stretching method and optical-lever principle. Wire elongation drives the movable platform, changing the angle of the moving mirror. The optical path is then redirected through moving and fixed mirrors, finally forming a readable displacement on the receiving plane or microscope.

This method keeps the optical amplification principle visible:

- the optical path can be traced directly;
- the operation is more compact than moving a separate telescope and scale;
- the result can be used as an independent measurement route;
- it can cross-check the result from the projection method.

## Projection Method With Linear CCD

The projection method uses a laser, slit, and linear CCD. When the wire elongates, the slit width changes. The projected optical feature on the CCD changes accordingly. The software reads CCD intensity data through the serial port and estimates the optical feature size.

The project report describes the geometric relationship through similar triangles. In the software operation notes, the CCD scale factor is recorded as approximately `0.343`, corresponding to the ratio between optical distances in the instrument setup.

The projection route contributes two important improvements:

- it replaces subjective manual reading with software-assisted feature extraction;
- it turns the deformation process into a visible, recordable intensity curve.

## Dual-Method Verification

The main design value is not only "automation", but **one instrument with two measurement methods**:

- folded optical path method: suitable for principle visibility and manual verification;
- projection method: suitable for CCD acquisition, automated reading, and data processing.

When the two methods produce close Young's modulus values and both show lower uncertainty than the traditional method, the result becomes more convincing for teaching and experiment demonstration.

## Teaching Workflow

The project also adds a remote pre-lab concept. Students can use an online platform to learn the principle and interact with the experiment before entering the offline class. The goal is to make offline time focus on measurement, comparison, and error analysis instead of basic principle familiarization.
