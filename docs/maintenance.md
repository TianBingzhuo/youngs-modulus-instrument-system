# Maintenance Notes

## 2026-09-08: Source Preservation and Targeted Fixes

The published source before this work remains available at commit:

```text
3a8a006961034b0b1a7a24e94468327e042d754a
```

This is a snapshot of the previously published repository, not a claim that every byte is identical to the original competition submission. The maintenance changes are added as a new commit in the same history; earlier commits are not replaced.

The earliest public repository commit is `a3abf30` from 2026-05-22. It contains the files as they existed at first publication, including descriptions corrected later. It is a historical reference, not the recommended version for running an instrument.

The original team Word/PPT files and images are unchanged. Team contributions, third-prize attribution and existing license scope are retained.

### Application Changes

- CCD collection now keeps and awaits its background task. Stop clears buffered input and invalidates queued frame updates; leaving the page closes its device resources.
- Serial open/close and collection start/stop are serialized. The receiver uses the actual number of bytes returned by `Read()` and bounds the buffer to four frames.
- Write failures, receive failures and overflow stop acquisition with a visible error. Delayed faults from an older run do not close a newer run.
- The title-bar control is described and implemented as a software acquisition lock. The hardware stop/reset hooks remain unimplemented. Unlocking never starts collection automatically.
- Integration time is applied on the next explicit Start rather than sent during selection changes.
- CSV import validates a complete bounded frame before changing the displayed data. Invalid UTF-8, indices, intensities and row counts are rejected.
- Record reads are bounded. Saving serializes first, writes a same-directory temporary file and replaces the destination. Homepage and experiment-page updates share a process-local gate.
- A failed record read no longer inserts fake example measurements. Failed writes are visible. Measurement deletion is saved, and new record names avoid same-second collisions.
- Contrast uses floating-point division and handles an all-zero frame. Recording rejects non-finite load/width values and frames without intensity variation.
- The fit uses the configured wire length instead of a hard-coded 0.530 m. The historical boundary algorithm, IQR filtering, fitting cutoff and sign convention remain.
- Camera initialization, capture and cleanup are serialized. Preview thumbnails are bounded; saved image files remain separate. Legacy Base64 input has size and pixel-count limits.
- Release trimming is disabled because the record serializer uses reflection. This avoids discarding serialized model properties or disabling reflection-based serialization in published output.

### Examples and Documentation

The former bright-peak Python example has been replaced with a dark-valley example that follows the C# smoothing, threshold and fallback rules. It clearly labels synthetic output and can create an offline-import CSV without overwriting an existing file.

The separate uncertainty example validates input and labels its first-order, independent-input assumptions. It is not presented as the C# fit.

README, operation guide, architecture, data flow and pseudocode now distinguish implemented behavior from planned features. Historical extracts remain available, with a note directing readers to the current guide.

### Validation

Validation was performed on 2026-09-08:

| Check | Result |
|---|---|
| Windows C# helper runner | 33 checks passed; linked production helper files. |
| WSL Ubuntu Linux x64 helper runner | Same 33 checks passed using a self-contained .NET 10 runner. |
| Python unit tests on Windows | 8 tests passed. |
| Python unit tests on WSL Ubuntu | Same 8 tests passed. |
| Windows x64 application | Debug and Release builds pass with .NET SDK 10.0.400 targeting .NET 9. |
| NuGet vulnerability query, including transitive packages | No vulnerable packages reported by the configured feeds at the time of the query; not a full security audit. |
| Hardware, interactive GUI, package installation, macOS and ARM boards | Not verified in this maintenance pass. |

Build warnings are not hidden. The remaining legacy unused parameter fields and WindowsBase/WebView2 reference conflict need a separate dependency and cleanup review. See [compatibility and tests](compatibility.md) for reproducible commands and the manual acceptance checklist.

### Known Limits

The application is still a Windows prototype. There is no physical emergency-stop protocol, hardware reset implementation, automatic calibration, complete experiment snapshot or universal device driver.

Record matching remains name-based. Wire parameters remain application-level settings, so reopening an old record does not restore that experiment's calibration. The process-local record gate does not coordinate independent app instances, and file replacement is not a guarantee against power-loss corruption.

The threshold algorithm can accept misleading optical shapes; flat-frame rejection does not solve all signal-quality problems. The fixed least-squares denominator cutoff and absolute-value convention are retained for traceability and need experimental review before reuse at other scales.

### Review the Changes

```sh
git log --oneline
git show 3a8a006:app/YoungsModuleTest/Views/ExperimentPage.xaml.cs
git diff 3a8a006 HEAD -- app simulations docs README.md
```

The hosting platforms also let you browse files at a particular commit and inspect its diff. For a fixed comparison later, replace `HEAD` with the maintenance commit ID shown in the history. These are source revisions, not signed application installers or hardware-validated releases.
