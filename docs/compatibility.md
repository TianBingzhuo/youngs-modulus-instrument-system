# Compatibility and Tests

The repository contains three different things: a Windows instrument application, small platform-independent C# helpers, and Python teaching examples. Their support boundaries are different.

## Platform Matrix

| Component | Windows x64 | WSL Ubuntu x64 | Other Linux boards | macOS |
|---|---|---|---|---|
| WinUI 3 application | Debug and Release build validation; GUI and hardware checklist still required. | Not a native Linux application. | Not supported by the current UI. | Not supported by the current UI. |
| Maintenance helpers | Console checks pass against the production source files. | Same checks pass in a self-contained Linux x64 process. | Not tested; requires a compatible .NET runtime and board OS. | Not tested. |
| Python examples | Unit tests pass. | Unit tests pass with Python 3. | Standard-library code; requires Python 3.10+, not tested on boards. | Standard-library code; not tested on macOS. |

[WinUI 3 targets Windows desktop](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/). WSL can provide a Linux test environment, but it does not make Windows UI APIs portable.

POSIX is not a promise that any managed application will run on every Unix-like system. CPU architecture, libc, runtime support, device permissions and drivers still matter. Serial and camera adapters would need their own tests on each target. No Linux/macOS physical CCD driver is provided here.

## Run the Tests

Run commands from the repository root.

### Python

Requires Python 3.10 or later and only the standard library:

```sh
python -m unittest discover -s tests -p "test_*.py" -v
python simulations/simulated_ccd_reader.py
python simulations/uncertainty_budget.py
```

Use `python3` if that is the interpreter name on your system. Tests use synthetic signals only.

### C# Helpers

Requires the .NET 10 SDK for the console test project. The instrument application itself continues to target .NET 9.

```sh
dotnet run --project tests/Maintenance.Tests -c Release
```

The project links `app/YoungsModuleTest/Maintenance/*.cs` directly. Checks cover partial and coalesced frames, overflow, stale-buffer clearing, CSV validation, bounded record reads, replacement failures, and serialized file updates. They do not exercise WinUI event handlers or the device.

To test on Linux without installing the SDK on that machine, publish a self-contained runner for its architecture. The following command is for **Linux x64**, not ARM boards:

```sh
dotnet publish tests/Maintenance.Tests -c Release -r linux-x64 --self-contained true -o tests/Maintenance.Tests/bin/linux-x64
```

On Linux, run `tests/Maintenance.Tests/bin/linux-x64/Maintenance.Tests`. If the copied executable lacks execute permission, grant it with `chmod +x`. Build output is ignored by Git.

### Windows Application

```powershell
dotnet restore app/YoungsModuleTest/YoungsModuleTest.csproj -p:Platform=x64 -r win-x64
dotnet build app/YoungsModuleTest/YoungsModuleTest.csproj -c Debug -p:Platform=x64 -r win-x64 --no-restore
dotnet build app/YoungsModuleTest/YoungsModuleTest.csproj -c Release -p:Platform=x64 -r win-x64 --no-restore
```

The application uses reflection-based JSON serialization, so Release trimming is disabled. Build success is not an MSIX installation, GUI acceptance test or hardware validation. Legacy unused-field and WindowsBase/WebView2 reference warnings remain documented in the maintenance notes.

## Before Using a Physical Instrument

The following checks remain manual and must use the matching CCD board and a safely unloaded setup:

- Start and stop repeatedly; confirm only one polling task is active and old frames do not repaint the stopped view.
- Lock acquisition, try to start or record, then unlock. No acquisition should resume until explicitly started.
- Disconnect the USB cable while collecting, reconnect, and confirm the failure is visible and a new connection is required.
- Navigate away or close while connecting, collecting or initializing the camera; confirm the port and camera can be opened again.
- Import a valid synthetic CSV, then malformed, oversized and unordered files. Invalid imports should leave the displayed frame unchanged.
- Save, reopen and delete test records. Make the test record file unwritable and confirm the app reports failure without erasing the previous file.
- Deploy the Release package and verify JSON save/load as well as camera permissions.
- Compare optical alignment, integration-time commands and frame shape against the actual module documentation.

The raw serial protocol has no implemented sequence number or resynchronization marker. A delayed response from an earlier request may still arrive at the device boundary. Software generation checks protect queued UI work; they cannot label bytes the hardware does not label.

## 中文说明

本轮已验证 Windows 和 WSL Ubuntu 的纯计算、帧缓存及文件读写，不代表整台仪器已经跨平台。原 C# 界面仍使用 WinUI，Linux 开发板和 macOS 需要另行设计界面与硬件接入层。本仓库没有做“兼容所有 Linux/POSIX 设备”的承诺。

上面的人工清单是接入实机前的验收步骤，尚不能用单元测试代替。
