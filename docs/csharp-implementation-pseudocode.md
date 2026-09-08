# C# Implementation Pseudocode

This is a source map for the maintained WinUI application, not a replacement implementation. The baseline published source is preserved at `original-source-snapshot-2026-09-08` (commit `3a8a006`). The low-valley and fitting rules below come from that source; lifecycle and file-handling changes are listed in [maintenance notes](maintenance.md).

The original C# application is under `app/YoungsModuleTest/`. Python files in `simulations/` are later examples.

## Navigation and Page Lifetime

```text
MainWindow:
  navigate to HomePage
  hold the global software-lock flag

on navigation selection:
  resolve the page type
  navigate only if this is not already the current page
  # opening a saved record must not be followed by a second empty navigation

on ExperimentPage entry:
  mark page active
  apply the global software-lock state
  restore the supplied record, if present
  enumerate cameras and serial ports

on ExperimentPage exit:
  mark page inactive
  stop and await CCD collection
  detach serial callback, close and dispose port
  clean up camera resources

on window close:
  defer close
  clean up the current experiment page
  close once cleanup has returned
```

The camera has a separate lifetime from CCD polling. Camera initialization, capture and cleanup are serialized so leaving the page during initialization does not leave a newly initialized camera running.

## Software Stop

```text
on title-bar stop button:
  temporarily disable the button while changing state
  toggle the global software-lock flag

  if locked:
    call StopAllOperations()       # reserved hook; NO hardware command
    disable Start, Record and CSV Import
    cancel and await CCD polling
    discard buffered input and invalidate queued frame updates
  else:
    call ResetInstrument()         # reserved hook; NO hardware command
    permit an explicit new Start
    do not automatically restart collection

  update the button and status text
```

This does not remove power, unload the wire or reset the device. An in-flight serial write may complete or time out before the polling task ends. The camera can remain available for observation. A physical emergency stop must not depend on this event handler.

## Serial Acquisition

```text
on port selection, under connection gate:
  clean up the previous port and collection task
  return if the page has already been left
  open SerialPort(port, 921600, parity=None, dataBits=8, stopBits=1)
  set read and write timeouts to 1000 ms
  attach DataReceived callback
  mark connected

on integration-time selection:
  parse the hexadecimal command tag
  remember it for the next Start; do not write to the port immediately

on Start, under collection gate:
  require active page, connected port, no software lock and no input fault
  return if already collecting
  discard pending serial input and clear the frame buffer
  create a cancellation source and new acquisition generation
  keep a reference to the background task

background task:
  send selected integration-time command
  repeat until cancelled:
    send 0xA2
    wait 100 ms with cancellation
    process one complete buffered frame, if available
    wait 200 ms with cancellation

on DataReceived, under port lock:
  ignore callbacks from an old port or inactive collection
  reject available input larger than buffer capacity
  read available bytes
  append only the number of bytes actually returned by Read()
  if read fails or buffer overflows:
    cancel collection and enqueue a fault notification
```

The buffer limit is `4 * 3648` bytes. Each frame is taken in byte order. There is no frame header or checksum parser, and no verified resynchronization algorithm for lost bytes.

Fault notifications are tagged with the acquisition generation, so a delayed failure from one run does not close a newer connection.

## Frame Dispatch

```text
take one 3648-byte frame from the buffer
if no complete frame exists: return
if a frame update is already queued: discard this display update

queue at most one UI update:
  require active page, collecting state, no software lock
  require the same acquisition generation
  create CCDDataPoint(index, intensity) for every sample
  replace the current frame
  analyze boundaries
  redraw curve and boundary markers
  update statistics
```

Dropping a display update limits UI backlog; it is not archival acquisition. This application does not retain every received frame.

## Dark-Region Detection

The code finds a low-intensity region, not a bright peak. Names such as `IlluminatedLength` are historical and refer here to the detected dark-region width.

```text
N = number of intensity samples

for i in 0..N-1:
  smoothed[i] = mean(intensity[max(0, i-5)..min(N-1, i+5)])
  # inclusive endpoints: up to 11 samples

firstAverage = mean(smoothed)
belowAverage = values in smoothed that are <= firstAverage
secondAverage = mean(belowAverage)
threshold = secondAverage + 5

low = indices whose smoothed value <= threshold

if low has at least 4 indices:
  left, right = low[1], low[-2]
else if low has 2 or 3 indices:
  left, right = low[0], low[-1]
else:
  minimum = first index with the minimum smoothed value
  left = max(0, minimum - 100)
  right = min(N - 1, minimum + 100)

if left >= right:
  center = floor(N / 2)
  left = max(0, center - 50)
  right = min(N - 1, center + 50)

widthPixels = abs(right - left)
widthMm = widthPixels * 8.0 / 1000
```

`SmoothData(intensities, 10)` does **not** mean a ten-sample window: the original inclusive loop uses up to eleven samples. The offset is **+5**, despite an old comment that said +10.

This rule can span disconnected low regions and returns a wide region even for a flat frame. Those behaviors are retained so the algorithm remains traceable. The maintained recording handler rejects frames without any intensity variation; it does not certify other frames as optically valid.

## Statistics and Measurement Records

```text
max, min, mean = statistics of the raw frame
contrastPercent = 0 if max + min == 0
                  else 100.0 * (max - min) / (max + min)

on Record:
  require active page, no software lock, no input fault, no save in progress
  require a finite nonnegative load in kg
  require a full non-flat frame and a finite positive width
  append load, width, timestamp, material, boundaries and pixel separation
  create or update the experiment record
  report success only after saving returns successfully
  on failure, retain in-memory data and display an unsaved warning
```

The frame supplies optical width, not wire elongation directly. Geometry and calibration enter the subsequent fit.

## Least-Squares Calculation

```text
require at least 2 measurements

if there are at least 4 measurements:
  lengths = sorted optical widths
  Q1 = lengths[floor(N / 4)]
  Q3 = lengths[floor(3 * N / 4)]
  IQR = Q3 - Q1
  keep widths within [Q1 - 1.5 * IQR, Q3 + 1.5 * IQR]

require at least 2 retained measurements
sort retained measurements by load
baseWidth, baseLoad = first measurement

for each retained point:
  x = (widthMm - baseWidth) * ccdToWireRatio / 1000   # metres
  y = (loadKg - baseLoad) * 9.8                      # newtons

D = N * sum(x*x) - sum(x)^2
if abs(D) < 1e-10: return no result
slope = (N * sum(x*y) - sum(x)*sum(y)) / D

wireLengthM = configuredWireLengthMm / 1000
wireDiameterM = configuredWireDiameterMm / 1000
area = pi * (wireDiameterM / 2)^2
return abs(slope * wireLengthM / area / 1e9)         # GPa
```

The maintenance version uses the configured wire length instead of the former hard-coded 0.530 m. The original IQR indexing, denominator cutoff and absolute-value convention are otherwise retained. They require review before generalizing the instrument to other measurement ranges.

`CalculateYoungsModulus()` still calls the calculation, but the assignments to numerical result labels are commented out. `UpdateExperimentRecord()` can store a finite result internally. The correlation function uses retained load/width pairs and returns squared correlation; it is not an uncertainty estimate.

## Persistence and File Operations

```text
save record, under the shared process-local record gate:
  read existing file as bounded UTF-8 (maximum 32 MiB)
  require a JSON array with non-null records
  replace the matching record name or append a new one
  serialize the complete array before touching the destination
  write and flush a temporary file in the same directory
  replace the destination
  clean up the temporary file on failure

delete from homepage:
  ask the user to confirm
  acquire the same gate and read the current file
  remove the requested records and save
  update the homepage only after success

import CCD CSV:
  require idle, unlocked acquisition
  read at most 128 KiB
  validate UTF-8, header, exactly 3648 rows, sequential indices and byte values
  re-check state after asynchronous reading
  replace the displayed frame only after validation

export CCD CSV:
  write current frame indices and intensities

export TXT report:
  write current measurement rows and interface text
```

The format is still the original local JSON schema, not a database. The shared gate does not coordinate multiple independent application instances.

## Unfinished Snapshot Code

`SaveCompleteExperimentData()` is not called by the recording workflow. `ConvertBitmapToBase64()` returns an empty string. The record schema has fields for CCD data, photos and device settings, but this is not a working end-to-end snapshot feature.

Photographs captured through the interface are saved separately as JPEG files. Importing CCD CSV does not restore those photographs, a measurement series or per-experiment calibration.

## 中文导读

这份伪代码沿着实际调用顺序描述程序，而不把预留接口写成已实现功能。重点是串口事件与 UI 分开、完整帧处理、暗区边界可视化、载荷与宽度记录，以及随后进行的拟合。

需要特别注意：软件锁定不等于实体急停；kg 输入不等于已经换算的 N；光学宽度不等于钢丝伸长；JSON 有某个字段不等于保存链路已经接通。原始项目文档保留设计意图，当前操作以[操作说明](software-operation-guide.md)为准。
