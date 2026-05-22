# Young's Modulus Instrument System

This project page connects applied-physics measurement with system engineering: optical measurement, sensor acquisition, data processing, and desktop software workflow.

## One-Sentence Summary

The Young's modulus experiment can be framed as a measurement-to-software pipeline: physical deformation, optical amplification, CCD/camera acquisition, serial data, software processing, experiment records, and remote pre-lab support.

## System Chain

```text
wire deformation and optical amplification
  -> CCD / camera / serial data
  -> data parsing and feature extraction
  -> Young's modulus calculation and uncertainty-aware records
  -> WinUI/.NET desktop workflow
  -> remote pre-lab and teaching workflow
```

## Public Technical Framing

- Physical measurement and uncertainty-aware experimental workflow.
- Optical amplification and projection/folded optical-path methods at a high level.
- CCD/camera/serial acquisition as a sensor-data pipeline.
- Desktop software as an experiment-record and visualization workflow.

## Public Source Code

- `app/YoungsModuleTest/`: cleaned WinUI/.NET desktop application source snapshot.
- `src/simulated_ccd_reader.py`: a public-safe simulated data reader.
- `src/uncertainty_budget.py`: a minimal uncertainty-budget calculation for measurement reporting.
- `assets/README.md`: guidance for adding public screenshots or setup images.

Run:

```bash
python src/simulated_ccd_reader.py
python src/uncertainty_budget.py
```

## Application Snapshot

The application snapshot keeps source files, XAML views, project metadata, and app assets. Local build outputs, `.vs/`, `bin/`, `obj/`, signing certificates, user-specific publish files, and packaged executables are excluded.

Main UI source locations:

- `app/YoungsModuleTest/MainWindow.xaml`
- `app/YoungsModuleTest/Views/HomePage.xaml`
- `app/YoungsModuleTest/Views/ExperimentPage.xaml`
- `app/YoungsModuleTest/Views/SettingsPage.xaml`

## Public Artifacts

- `docs/physics-model.md`: high-level measurement model.
- `docs/software-architecture.md`: desktop software and data-flow architecture.
- `docs/data-pipeline.md`: synthetic CCD data and experiment-record pipeline.

## Public Boundary

This page does not publish raw reports, team information, private screenshots, instrument photos, executable programs, drivers, or signed packages. Personal contribution wording should remain conservative until role details are confirmed.

For images, use only public-safe screenshots or equipment photos: no faces, no student names, no private lab records, no unpublished instrument schematics unless you are sure they can be public. A few cropped interface screenshots are more useful than dumping every design image.
