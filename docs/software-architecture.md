# Software Architecture

The application is a Windows desktop program built with C#, .NET 9 and WinUI 3. Most instrument logic remains in `ExperimentPage.xaml.cs`, matching the original project structure. This maintenance pass adds small input and storage helpers; it does not replace the application with a new framework.

![Experiment page from the project documents](assets/winui-experiment-page.jpg)

The image comes from the original project materials. Labels and stop behavior in the maintained build may differ; see the [operation guide](software-operation-guide.md).

## Pages and Responsibilities

| Source | Responsibility |
|---|---|
| `App.xaml.cs` | Application startup and the main window. |
| `MainWindow.xaml.cs` | Navigation and the global software acquisition lock. |
| `Views/HomePage.xaml.cs` | Load, open and delete local experiment records. |
| `Views/ExperimentPage.xaml.cs` | Serial acquisition, camera capture, plotting, boundary detection, recording and fitting. |
| `Views/TheoryPage.xaml.cs` | Placeholder. |
| `Views/CalibrationPage.xaml.cs` | Placeholder, not an automatic calibration service. |
| `Views/SystemCheckPage.xaml.cs` | Placeholder, not a hardware health check. |
| `Views/SettingsPage.xaml.cs` | Application information. |

丁庆祥's online pre-lab software is not part of this process or source tree.

## Acquisition Lifetime

Selecting a port opens it. Starting acquisition sends the selected integration-time command and then polls with `0xA2`. A receive callback reads actual bytes into a bounded frame buffer. Complete frames are dispatched to the UI for analysis and plotting.

Three synchronization boundaries have different purposes:

- `_connectionGate` serializes opening and closing the serial port.
- `_collectionGate` serializes starting and stopping the polling task.
- `_portGate` protects the live port, receive callback and cancellation source.

Closing detaches the event handler and releases the port lock before disposing the serial port. The polling task is retained and awaited on stop. UI callbacks carry an acquisition generation number, so a callback from a stopped run cannot repaint a newer run.

Leaving the page starts cleanup. Closing the window cancels that close request long enough to clean up the current experiment page, then closes the window. This is resource management, not a hard real-time stop guarantee.

## Input and Storage Helpers

```text
Maintenance/
  CcdFrameBuffer.cs   bounded 3648-byte frame assembly
  CcdCsv.cs           strict single-frame CSV validation
  RecordFile.cs       bounded UTF-8 file reads and temporary-file replacement
```

These helpers do not reference WinUI. The console test project links the actual helper files; it does not test a separate reimplementation. They provide a small testable boundary, not a portable hardware driver or a completed cross-platform application.

The homepage and experiment page share `RecordFile.Gate` around the entire read-modify-write sequence. JSON is serialized before replacing the destination file. If serialization or writing fails, the app reports it instead of clearing the old file first. The gate protects this process only; concurrent independent app instances are not coordinated.

## Software Lock vs Hardware Control

The global lock prevents new CCD collection, measurement recording and CSV import. It stops polling and rejects stale UI updates. Camera observation is separate and remains available.

`StopAllOperations()` and `ResetInstrument()` are reserved hooks without device commands. No actuator stop, power isolation or hardware reset is implemented. The lock is not a safety interlock for the physical instrument.

## 中文导读

阅读顺序建议从主窗口进入实验页，再跟踪串口接收、边界识别和保存记录。核心实验逻辑仍在原来的 C# 页面文件中，新增的三个小文件只负责帧缓存、CSV 校验和文件读写。

“停止采集”“软件锁定”“关闭串口”和“设备急停”不是同一件事。当前代码实现前三类软件行为，不实现设备断电、机械卸载和硬件复位。完整操作流程见[操作说明](software-operation-guide.md)，算法细节见[伪代码](csharp-implementation-pseudocode.md)。
