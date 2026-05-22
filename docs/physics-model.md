# Measurement Principle

The project improves the classic Young's modulus experiment by combining two optical measurement methods in one instrument: folded optical path measurement and projection measurement. The goal is to make the deformation process more stable, more visible, and easier to measure repeatedly.

## Measurement Chain

```text
force loading
  -> wire deformation
  -> optical amplification
  -> sensor acquisition
  -> displacement estimate
  -> Young's modulus calculation
  -> uncertainty-aware record
```

## Instrument Design

- The main support structure uses an aluminum-profile triangular frame.
- The wire is fixed with custom fixtures to reduce sliding error.
- A pulley-based loading mechanism applies force more smoothly than directly adding weights to the wire.
- A folded optical path keeps the optical lever principle visible and compact.
- A projection method uses a slit and linear CCD to convert small deformation into a measurable optical-feature change.

## Calculation

The software records force and CCD-derived deformation. After two or more valid records are collected, the application estimates Young's modulus through least-squares fitting and reports the fitting quality. This reduces the dependence on manual reading and repeated hand calculation.

## Teaching Design

The project also includes a remote pre-lab idea: students can learn the principle and observe/operate the experiment before the offline class, so the classroom session can focus more on measurement, comparison, and error analysis.
