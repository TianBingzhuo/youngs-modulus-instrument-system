# Measurement Data Flow

This page describes the data handled by the current C# application. It separates a CCD frame, a measurement point and a saved experiment record: they are not interchangeable files.

## From Bytes to Optical Width

```text
serial response
  -> 3648 unsigned intensity bytes
  -> centered moving average
  -> dark-region threshold and two boundaries
  -> pixel width * 8 micrometres / 1000
  -> optical width in millimetres
```

The receive buffer holds at most four frames (14,592 bytes). Overflow is treated as an acquisition fault, not silently trimmed into a new frame. This limit is a software memory bound, not a statement about the vendor's packet format.

The original rule uses the second and penultimate samples below a two-stage threshold. It does not fit a bright peak. The full rule, including fallbacks and its limitations, is in [C# pseudocode](csharp-implementation-pseudocode.md).

## Measurement Points

`MeasurementRecord` contains:

| Field | Meaning |
|---|---|
| `SerialNumber` | Position in the current measurement list. |
| `AppliedForce` | Operator-entered load in kg, despite the historical field name. |
| `IlluminatedLength` | Detected dark-region width in mm, despite the historical field name. |
| `Timestamp`, `MaterialType` | Capture time and material label. |
| `LeftBoundary`, `RightBoundary` | Pixel indices. |
| `PixelLength` | Absolute boundary separation. |
| `ActualLength` | Duplicate optical width in this record format; not automatically the wire elongation. |

Wire diameter, effective wire length and the CCD-to-wire ratio are kept in application settings. They are **not independently stored with every measurement point**. Re-analysis of an old record therefore requires checking the current settings against the experiment notes.

## Fitting

The code sorts retained points by load, subtracts the first point as a baseline, converts load differences to N and optical-width differences to estimated wire deformation in m, then calculates a least-squares slope.

The result is converted to GPa using the configured wire length and cross-sectional area. The original sign convention takes an absolute value, and the original IQR filtering is retained. These choices do not establish that a noisy or misaligned experiment is valid.

The calculation can update `ExperimentRecord.YoungsModulus` internally. Numerical result display remains disabled in this edition. A public repository without measurement datasets does not mean the running application never stores a user's calculated values.

## Saved Experiments

`experiment_records.json` is a UTF-8 JSON array in the application's local data folder. Measurement lists are serialized inside the `measurementData` string, preserving the historical schema. Records are still matched by name; new names include a timestamp and random suffix to avoid same-second collisions.

File reads and writes are limited to 32 MiB. Both pages use a process-local lock and same-directory temporary-file replacement. Invalid JSON is reported and left in place. This is not a database, a cross-process lock, or a guarantee against power-loss corruption.

The unused `SaveCompleteExperimentData()` method shows an intended fuller record format. Its photo encoder is unfinished, so its fields should not be read as proof of complete snapshot persistence.

## Import and Export

CCD CSV uses the header `pixelIndex,intensity` or `像素索引,光强度`. It has exactly 3648 data rows, ordered indices 0..3647 and integer intensities 0..255. UTF-8 BOM and CRLF are accepted. Validation completes before the displayed frame changes.

The TXT report contains the current measurement rows and interface text. It is not a complete restorable archive. Photographs are saved separately as JPEG files in the Windows Pictures library.

## Later Python Examples

`simulated_ccd_reader.py` now follows the C# dark-region rule on synthetic 8-bit data. A flat input can still produce a width under the historical rule; the application rejects a flat frame when recording a measurement. A detected width alone is not a quality certificate.

`uncertainty_budget.py` is an independent single-point formula and first-order propagation example. It assumes independent input uncertainties and uses k=2 for expanded uncertainty. It does not reproduce the C# multi-point fit, infer a confidence level, or represent a measured result.

## 中文摘要

CCD CSV 是一帧波形，测量记录是一组“载荷和光学宽度”，JSON 才是首页保存的实验列表。导入 CSV 不等于恢复完整实验。钢丝参数目前仍是应用级设置，照片单独保存；使用旧记录时必须重新核对参数。计算示例里的数字全部是合成输入，不作为仪器精度或比赛结果。
