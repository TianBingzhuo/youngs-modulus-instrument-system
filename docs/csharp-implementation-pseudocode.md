# C# Implementation Pseudocode

This document summarizes the original WinUI 3 / C# application under `app/YoungsModuleTest/`. It isn't meant to replace the source code -- think of it more as a map of the engineering flow behind the UI, serial CCD acquisition, measurement records, fitting, import/export, and emergency stop logic. I wrote this so someone reading on GitHub can understand what the program does without having to open Visual Studio first.

The Python files under `simulations/` are later hardware-independent demos. The original application is the C# project.

## Main Window And Navigation

```text
on MainWindow startup:
  initialize WinUI shell
  navigate ContentFrame to HomePage
  select Home navigation item

on navigation item changed:
  map item tag to page type
    Home -> HomePage
    Theory -> TheoryPage
    Experiment -> ExperimentPage
    Calibration -> CalibrationPage
    SystemCheck -> SystemCheckPage
    Settings -> SettingsPage
  navigate ContentFrame to selected page

on emergency stop button clicked:
  toggle global emergency_stop flag

  if emergency_stop is true:
    paint stop button red
    call StopAllOperations()
    notify current ExperimentPage, if loaded
  else:
    restore button style
    call ResetInstrument()
    notify current ExperimentPage, if loaded
```

## Experiment Page Startup

```text
on ExperimentPage constructed:
  initialize components
  initialize CCD state, measurement lists, camera lists, and wire parameters

on page loaded / navigated to:
  check global emergency-stop state from MainWindow
  load available cameras
  load available serial ports
  load saved wire parameters

  if a saved experiment record is passed by navigation:
    load material type
    restore measurement records
    restore CCD data and boundary markers
    restore captured photos
    restore integration time, serial port, and camera metadata when available
```

## Serial CCD Acquisition

```text
load serial ports:
  run SerialPort.GetPortNames() off the UI thread
  dispatch back to UI:
    clear serial-port ComboBox
    if none found:
      show "no serial device"
    else:
      fill ComboBox and ask user to select a port

on serial port selected:
  show "connecting"
  cleanup previous serial port
  create SerialPort(port, 921600 baud, no parity, 8 data bits, 1 stop bit)
  attach DataReceived handler
  open serial port
  send current integration-time command
  mark CCD as connected

on integration-time selection changed:
  parse item tag as hex command
  store command as current integration time
  if CCD is connected:
    send command to serial port

on DataReceived event:
  mark receiveDataFlag = true
  read all available bytes
  lock shared data buffer
  append bytes into buffer
  mark receiveDataFlag = false
```

## CCD Frame Processing

```text
on Start CCD:
  require serial port connected
  if already collecting: return
  set isCollecting = true
  disable Start button, enable Stop button
  create CancellationTokenSource
  start background ProcessCCDDataLoop()

ProcessCCDDataLoop:
  while collecting and not cancelled:
    if serial port is open:
      send 0xA2 to request CCD frame
      wait 100 ms
      ProcessBufferedData()
      wait 200 ms

ProcessBufferedData:
  lock shared data buffer
  if buffer has fewer than 3648 bytes:
    return
  take first 3648 bytes as one CCD frame
  remove those bytes from buffer

  convert each byte to CCDDataPoint:
    PixelIndex = index
    Intensity = byte value

  dispatch to UI thread:
    replace current CCD data
    AnalyzeBoundaryPoints()
    UpdateCCDChart()
    UpdateStatistics()
```

## Boundary Detection

This is probably the most interesting algorithm in the whole program. It's not fancy, but it works for the CCD signal shape we were dealing with.

```text
AnalyzeBoundaryPoints:
  if no CCD data: return

  intensities = current frame intensity array
  smoothed = moving_average(intensities, window = 10)

  firstAverage = average(smoothed)
  belowAverageValues = all values <= firstAverage
  secondAverage = average(belowAverageValues)
  threshold = secondAverage + 5

  lowValleyIndices = all indices where smoothed[index] <= threshold

  if lowValleyIndices has at least 4 points:
    leftBoundary = second low-valley index
    rightBoundary = second-to-last low-valley index
  else if lowValleyIndices has 2 or 3 points:
    leftBoundary = first low-valley index
    rightBoundary = last low-valley index
  else:
    minIndex = index of minimum smoothed value
    leftBoundary = max(0, minIndex - 100)
    rightBoundary = min(lastIndex, minIndex + 100)

  if leftBoundary >= rightBoundary:
    center = frame length / 2
    leftBoundary = max(0, center - 50)
    rightBoundary = min(lastIndex, center + 50)

  illuminatedPixels = abs(rightBoundary - leftBoundary)
  illuminatedLengthMm = illuminatedPixels * 8 um / 1000
```

## Chart And Status Update

```text
UpdateCCDChart:
  clear Canvas
  compute chart bounds and intensity range
  draw grid lines
  draw X/Y axes
  draw red intensity curve over 3648 pixels
  draw green boundary markers
  fill the detected low-valley region with translucent color

UpdateStatistics:
  compute max, min, average intensity
  show boundary positions
  compute average intensity inside low-valley region
  compute contrast = (max - min) / (max + min)
  show current illuminated length
```

## Measurement Records

```text
on Record Data:
  require applied force input
  require current CCD illuminated length > 0

  create MeasurementRecord:
    serial number
    applied force
    illuminated length
    timestamp
    material type
    left/right CCD boundary
    pixel length
    actual length

  append record to measurement list
  refresh measurement ListView

  if this is the first point and no current experiment record exists:
    create a new ExperimentRecord
  else:
    update the current ExperimentRecord

  calculate Young's modulus internally
  show "recorded data point"
```

## Young's Modulus Calculation

```text
CalculateByLeastSquares:
  require at least 2 measurement records

  cleanedData = RemoveOutliers(records)
  require at least 2 cleaned records

  sort records by applied force
  baseLength = first record illuminated length
  baseForce = first record applied force

  wireLengthM = 0.530
  wireDiameterM = wireDiameterMm / 1000
  wireArea = pi * (wireDiameterM / 2)^2

  for each sorted record:
    shadowLengthChangeMm = record.illuminatedLength - baseLength
    actualDeformationM = shadowLengthChangeMm * ccdToWireRatio / 1000
    forceChangeN = (record.appliedForce - baseForce) * 9.8
    add actualDeformationM to deltaLengths
    add forceChangeN to deltaForces

  slope = least_squares_slope(x = deltaLengths, y = deltaForces)
  if denominator is too small:
    return null

  youngsModulusPa = slope * wireLengthM / wireArea
  return abs(youngsModulusPa / 1e9) as GPa

RemoveOutliers:
  if fewer than 4 records:
    return original records
  compute Q1, Q3, and IQR over illuminated length
  keep only records within [Q1 - 1.5 IQR, Q3 + 1.5 IQR]
```

## Persistence, Import, And Export

```text
CreateNewExperimentRecord:
  create record name with timestamp
  store material type, measurement count, serialized measurements
  save to ApplicationData.Current.LocalFolder / experiment_records.json

UpdateExperimentRecord:
  update description, material, count, serialized measurements
  calculate and store Young's modulus when available
  save back into experiment_records.json

SaveCompleteExperimentData:
  serialize current CCD data
  save left/right boundaries
  encode captured photos as Base64
  save integration time, serial port, camera name
  save CCD statistics and current illuminated length

ExportCCDDataToCSV:
  write "pixel index, intensity" rows for the current CCD frame

ExportExperimentReport:
  write material, fitting fields, integration time, measurement rows, CCD statistics, and current result text

ImportCCDDataFromCSV:
  parse CSV rows into CCDDataPoint objects
  AnalyzeBoundaryPoints()
  UpdateCCDChart()
  UpdateStatistics()
```

## Home Page Record Manager

```text
on HomePage startup:
  initialize quick-access module cards
  load experiment_records.json from local app data
  if no record exists:
    show a sample record

on quick-access card clicked:
  find matching NavigationView item in MainWindow
  switch selected navigation item

on open record:
  navigate to ExperimentPage and pass selected ExperimentRecord

on delete / clear records:
  ask for confirmation
  update in-memory collection
  save experiment_records.json
```

## What The Pseudocode Should Preserve

The important point isn't just "CCD data enters and a modulus comes out." The C# program is built around a live instrument workflow, and that context matters:

- hardware data comes in asynchronously through a serial event;
- the UI thread is updated through dispatcher callbacks;
- a complete CCD frame is treated as 3648 intensity samples;
- the optical low-valley region is made visible, not hidden as a black-box number;
- each measurement records the applied load, optical width, boundaries, timestamp, and material context;
- fitting is repeated as more points are recorded;
- local records, CCD raw data, photos, and parameters can be restored or exported;
- emergency stop is global and page-aware, so the instrument can enter and exit an abnormal state cleanly.

---

# 中文版本

本文件概述了 `app/YoungsModuleTest/` 下原始 WinUI 3 / C# 应用的逻辑。它不是源码的替代品——更像是一张工程流程图，帮助理解 UI、串口 CCD 采集、测量记录、拟合、导入导出和紧急停机背后的思路。写这个是为了让大家在 GitHub 上就能理解程序在做什么，不必打开 Visual Studio。

`simulations/` 下的 Python 文件是后来写的不依赖硬件的演示程序。原始应用是 C# 项目。

## 主窗口与导航

```text
on MainWindow startup:
  initialize WinUI shell
  navigate ContentFrame to HomePage
  select Home navigation item

on navigation item changed:
  map item tag to page type
    Home -> HomePage
    Theory -> TheoryPage
    Experiment -> ExperimentPage
    Calibration -> CalibrationPage
    SystemCheck -> SystemCheckPage
    Settings -> SettingsPage
  navigate ContentFrame to selected page

on emergency stop button clicked:
  toggle global emergency_stop flag

  if emergency_stop is true:
    paint stop button red
    call StopAllOperations()
    notify current ExperimentPage, if loaded
  else:
    restore button style
    call ResetInstrument()
    notify current ExperimentPage, if loaded
```

## 实验页面启动

```text
on ExperimentPage constructed:
  initialize components
  initialize CCD state, measurement lists, camera lists, and wire parameters

on page loaded / navigated to:
  check global emergency-stop state from MainWindow
  load available cameras
  load available serial ports
  load saved wire parameters

  if a saved experiment record is passed by navigation:
    load material type
    restore measurement records
    restore CCD data and boundary markers
    restore captured photos
    restore integration time, serial port, and camera metadata when available
```

## 串口 CCD 采集

```text
load serial ports:
  run SerialPort.GetPortNames() off the UI thread
  dispatch back to UI:
    clear serial-port ComboBox
    if none found:
      show "no serial device"
    else:
      fill ComboBox and ask user to select a port

on serial port selected:
  show "connecting"
  cleanup previous serial port
  create SerialPort(port, 921600 baud, no parity, 8 data bits, 1 stop bit)
  attach DataReceived handler
  open serial port
  send current integration-time command
  mark CCD as connected

on integration-time selection changed:
  parse item tag as hex command
  store command as current integration time
  if CCD is connected:
    send command to serial port

on DataReceived event:
  mark receiveDataFlag = true
  read all available bytes
  lock shared data buffer
  append bytes into buffer
  mark receiveDataFlag = false
```

## CCD 帧处理

```text
on Start CCD:
  require serial port connected
  if already collecting: return
  set isCollecting = true
  disable Start button, enable Stop button
  create CancellationTokenSource
  start background ProcessCCDDataLoop()

ProcessCCDDataLoop:
  while collecting and not cancelled:
    if serial port is open:
      send 0xA2 to request CCD frame
      wait 100 ms
      ProcessBufferedData()
      wait 200 ms

ProcessBufferedData:
  lock shared data buffer
  if buffer has fewer than 3648 bytes:
    return
  take first 3648 bytes as one CCD frame
  remove those bytes from buffer

  convert each byte to CCDDataPoint:
    PixelIndex = index
    Intensity = byte value

  dispatch to UI thread:
    replace current CCD data
    AnalyzeBoundaryPoints()
    UpdateCCDChart()
    UpdateStatistics()
```

## 边界检测

这可能是整个程序里最有意思的算法部分。方法不算复杂，但对我们面对的 CCD 信号形态确实好用。

```text
AnalyzeBoundaryPoints:
  if no CCD data: return

  intensities = current frame intensity array
  smoothed = moving_average(intensities, window = 10)

  firstAverage = average(smoothed)
  belowAverageValues = all values <= firstAverage
  secondAverage = average(belowAverageValues)
  threshold = secondAverage + 5

  lowValleyIndices = all indices where smoothed[index] <= threshold

  if lowValleyIndices has at least 4 points:
    leftBoundary = second low-valley index
    rightBoundary = second-to-last low-valley index
  else if lowValleyIndices has 2 or 3 points:
    leftBoundary = first low-valley index
    rightBoundary = last low-valley index
  else:
    minIndex = index of minimum smoothed value
    leftBoundary = max(0, minIndex - 100)
    rightBoundary = min(lastIndex, minIndex + 100)

  if leftBoundary >= rightBoundary:
    center = frame length / 2
    leftBoundary = max(0, center - 50)
    rightBoundary = min(lastIndex, center + 50)

  illuminatedPixels = abs(rightBoundary - leftBoundary)
  illuminatedLengthMm = illuminatedPixels * 8 um / 1000
```

## 图表与状态更新

```text
UpdateCCDChart:
  clear Canvas
  compute chart bounds and intensity range
  draw grid lines
  draw X/Y axes
  draw red intensity curve over 3648 pixels
  draw green boundary markers
  fill the detected low-valley region with translucent color

UpdateStatistics:
  compute max, min, average intensity
  show boundary positions
  compute average intensity inside low-valley region
  compute contrast = (max - min) / (max + min)
  show current illuminated length
```

## 测量记录

```text
on Record Data:
  require applied force input
  require current CCD illuminated length > 0

  create MeasurementRecord:
    serial number
    applied force
    illuminated length
    timestamp
    material type
    left/right CCD boundary
    pixel length
    actual length

  append record to measurement list
  refresh measurement ListView

  if this is the first point and no current experiment record exists:
    create a new ExperimentRecord
  else:
    update the current ExperimentRecord

  calculate Young's modulus internally
  show "recorded data point"
```

## 杨氏模量计算

```text
CalculateByLeastSquares:
  require at least 2 measurement records

  cleanedData = RemoveOutliers(records)
  require at least 2 cleaned records

  sort records by applied force
  baseLength = first record illuminated length
  baseForce = first record applied force

  wireLengthM = 0.530
  wireDiameterM = wireDiameterMm / 1000
  wireArea = pi * (wireDiameterM / 2)^2

  for each sorted record:
    shadowLengthChangeMm = record.illuminatedLength - baseLength
    actualDeformationM = shadowLengthChangeMm * ccdToWireRatio / 1000
    forceChangeN = (record.appliedForce - baseForce) * 9.8
    add actualDeformationM to deltaLengths
    add forceChangeN to deltaForces

  slope = least_squares_slope(x = deltaLengths, y = deltaForces)
  if denominator is too small:
    return null

  youngsModulusPa = slope * wireLengthM / wireArea
  return abs(youngsModulusPa / 1e9) as GPa

RemoveOutliers:
  if fewer than 4 records:
    return original records
  compute Q1, Q3, and IQR over illuminated length
  keep only records within [Q1 - 1.5 IQR, Q3 + 1.5 IQR]
```

## 持久化、导入与导出

```text
CreateNewExperimentRecord:
  create record name with timestamp
  store material type, measurement count, serialized measurements
  save to ApplicationData.Current.LocalFolder / experiment_records.json

UpdateExperimentRecord:
  update description, material, count, serialized measurements
  calculate and store Young's modulus when available
  save back into experiment_records.json

SaveCompleteExperimentData:
  serialize current CCD data
  save left/right boundaries
  encode captured photos as Base64
  save integration time, serial port, camera name
  save CCD statistics and current illuminated length

ExportCCDDataToCSV:
  write "pixel index, intensity" rows for the current CCD frame

ExportExperimentReport:
  write material, fitting fields, integration time, measurement rows, CCD statistics, and current result text

ImportCCDDataFromCSV:
  parse CSV rows into CCDDataPoint objects
  AnalyzeBoundaryPoints()
  UpdateCCDChart()
  UpdateStatistics()
```

## 首页记录管理

```text
on HomePage startup:
  initialize quick-access module cards
  load experiment_records.json from local app data
  if no record exists:
    show a sample record

on quick-access card clicked:
  find matching NavigationView item in MainWindow
  switch selected navigation item

on open record:
  navigate to ExperimentPage and pass selected ExperimentRecord

on delete / clear records:
  ask for confirmation
  update in-memory collection
  save experiment_records.json
```

## 伪代码要保留什么

重点不只是"CCD 数据进来、杨氏模量出来"。C# 程序是围绕实际仪器工作流构建的，这个上下文很重要：

- 硬件数据通过串口事件异步进入；
- UI 线程通过 dispatcher 回调更新；
- 一帧完整的 CCD 数据是 3648 个光强采样点；
- 光学低谷区域是可见的，不是藏在黑盒里的一个数字；
- 每条测量记录保存了施加载荷、光学宽度、边界、时间戳和材料信息；
- 随着更多数据点的记录，拟合会重复进行；
- 本地记录、CCD 原始数据、照片和参数都可以恢复或导出；
- 紧急停机是全局的且能感知当前页面，仪器可以干净地进入和退出异常状态。
