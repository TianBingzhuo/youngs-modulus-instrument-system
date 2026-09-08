# Young's Modulus Optical Measurement Instrument

A Windows application for collecting linear-CCD data during a Young's modulus experiment. It displays the intensity curve, locates the dark region, records the applied load and optical width, and exports data for analysis.

The instrument was our team entry, **Optical Improvement and Remote Pre-Lab Device for Young's Modulus Measurement**, in the 11th National Undergraduate Physics Experiment Competition (2025). It received **Third Prize**. The project explored both a folded optical path and a slit/laser/CCD measurement route.

![Instrument overview](docs/assets/instrument-overview.jpg)

The application is written in C# with WinUI 3. The Python examples were added later and run without an instrument. 丁庆祥's online pre-lab application is a separate contribution and is not included here.

## Start Here

- **Try the processing example:** run the Python commands below.
- **Use the Windows application:** follow the [operation guide](docs/software-operation-guide.md).
- **Read the implementation:** see the [architecture](docs/software-architecture.md), [data formats](docs/measurement-data-flow.md), and [C# pseudocode](docs/csharp-implementation-pseudocode.md).
- **Understand the instrument:** read the [measurement principle](docs/physics-model.md) and browse the [project images](docs/visual-supplement.md).

### Without Hardware

Requires Python 3.10 or later; no third-party packages are needed.

```sh
python simulations/simulated_ccd_reader.py
python simulations/uncertainty_budget.py
python -m unittest discover -s tests -p "test_*.py"
```

On systems where Python is named `python3`, use that command instead. The output is generated from synthetic inputs, not experimental results. To create a CSV for offline import:

```sh
python simulations/simulated_ccd_reader.py --csv synthetic-ccd.csv
```

The command refuses to overwrite an existing file.

### Build on Windows

The app targets .NET 9, Windows SDK 10.0.26100.0 and Windows App SDK 1.7. Use Visual Studio with WinUI tooling, or a Windows command-line environment with the corresponding SDKs. From the repository root:

```powershell
dotnet restore app/YoungsModuleTest/YoungsModuleTest.csproj -p:Platform=x64 -r win-x64
dotnet build app/YoungsModuleTest/YoungsModuleTest.csproj -c Debug -p:Platform=x64 -r win-x64 --no-restore
```

Open `app/YoungsModuleTest/YoungsModuleTest.sln` to run and debug the packaged application. Building it does not install the package or verify a connected instrument. See [compatibility and tests](docs/compatibility.md) for the tested configurations.

## Current Limits

The title-bar stop button **stops and locks software CCD acquisition**. It is not a hardware emergency stop. Unlocking does not reset the instrument or restart collection. Use the instrument's physical controls to remove power or unload the wire.

The Theory, Calibration and System Check pages are placeholders. The least-squares calculation remains in the source, but its numerical result display is disabled in this edition. Camera photographs are saved separately; complete photo-and-parameter restoration from experiment records is unfinished.

The measurement rule and default parameters come from a particular optical setup. Check the waveform and calibrate your own instrument before interpreting a width as wire deformation.

## Project Documents

The original, reviewed team documents are retained alongside the maintained software guide:

- [仪器讲解.docx](docs/source-materials/仪器讲解.docx): instrument design, principle and experiment procedure.
- [程序操作.docx](docs/source-materials/程序操作.docx): original software guide, including planned features.
- [演讲版本.pptx](docs/source-materials/演讲版本.pptx): presentation deck.

For what the current code actually implements, use the Markdown guide. [Maintenance notes](docs/maintenance.md) identify the preserved source snapshot and later fixes.

## Source Versions

| Commit | What it preserves |
|---|---|
| `a3abf30` (2026-05-22) | First public repository snapshot. |
| `3a8a006` (2026-07-17) | Last published version before the September maintenance changes. |
| `main` | Maintained code, tests and operating documentation. |

Earlier versions remain in the Git history. Use the hosting platform's commit history to browse a snapshot or inspect a change, or run `git diff 3a8a006 HEAD` locally to compare the pre-maintenance version with your checkout. See [maintenance notes](docs/maintenance.md) for the scope and test results of the September changes.

The first public snapshot is the repository as first published, not an untouched competition submission. Some historical descriptions have since been corrected; use the current operation guide when working with the instrument.

## Team

The project was completed under the guidance of 吴建海 and 宇霄.

| Member | Contribution |
|---|---|
| 丁庆祥 | Online pre-lab and remote-learning software, maintained separately. |
| 朱兆兴 | Mechanical structure. |
| 田秉卓 | Offline C# application, measurement-principle design, and repository maintenance. |
| 郭智美 | Measurement-principle design and data processing. |
| 赵思梦 | Measurement-principle design and data processing. |

## License

Read [LICENSE_SCOPE.md](LICENSE_SCOPE.md) and [NOTICE](NOTICE) for the scope of Apache-2.0 and team-owned materials. Third-party CCD tools and drivers keep their own terms; see [vendor tools](docs/vendor-tooling-notes.md).

---

## 中文说明

这是杨氏模量实验仪的 Windows 配套程序：从线阵 CCD 串口读取光强数据，显示波形和暗区边界，记录载荷与光学宽度，并导出数据。

我们以“杨氏模量测量实验的光学改进及远程预习装置”参加了 2025 年第十一届全国大学生物理实验竞赛，获**三等奖**。仪器包含光路折叠和狭缝、激光、线阵 CCD 两条测量思路。本仓库收录的是线下 C# / WinUI 3 程序；丁庆祥负责的在线预习软件不在这里。

**没有设备也可以先试。** 上面的 Python 命令只需要标准库，会生成一帧合成暗区信号，按 C# 的平滑和阈值规则计算边界。它是后加的讲解示例，不是比赛时的控制程序，也不包含实测结果。

**连接设备前先读[操作说明](docs/software-operation-guide.md)。** 软件停止按钮只停止并锁定 CCD 采集，不负责设备断电、机械卸载或硬件复位。解除锁定后还需要手动开始采集。

目前原理、校准和系统体检页面仍为预留页；拟合计算保留，但这一版没有开启数值结果显示。原始 Word/PPT 原样保留，当前实现以 Markdown 说明和源码为准。运行条件与验证范围见[兼容性说明](docs/compatibility.md)，后续修复见[维护记录](docs/maintenance.md)。

**旧版本保留在 Git 历史中。** 在平台的提交记录中可以查看首次公开版本 `a3abf30`、此次维护前的版本 `3a8a006`，以及后续修复的 diff；本地也可以用 `git diff 3a8a006 HEAD` 比较。无需另外保存一份旧代码。“首次公开”指仓库第一次发布时的内容，不等同于未经整理的比赛原件；使用设备时请以当前操作说明为准。
