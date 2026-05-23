# Young's Modulus Optical Measurement Instrument

杨氏模量光学测量实验仪

This repository documents a Young's modulus experiment-improvement project for the 2025 National Undergraduate Physics Experiment Competition (Innovation). The project is titled **Optical Improvement and Remote Pre-Lab Device for Young's Modulus Measurement**.

本仓库整理的是“杨氏模量测量实验的光学改进及远程预习装置”的工程资料。项目针对传统杨氏模量实验中装置稳定性不足、调节繁琐、光路不直观、人工读数误差较大的问题，设计了双测量方法、CCD自动读数软件和线上预习装置。

## Project At A Glance

| Item | Summary |
|---|---|
| Competition | 2025 National Undergraduate Physics Experiment Competition (Innovation). |
| System type | Young's modulus optical measurement instrument with folded optical path, projection/CCD route, and offline WinUI 3 software. |
| Repository focus | Offline C# instrument software, original project materials, public-readable docs, visual supplement, and hardware-independent Python explanations. |
| My role | Uploaded offline program design and measurement-principle design, plus public repository maintenance. |
| Not included | Online pre-lab software, unreviewed raw experiment-result data, packaged Windows outputs, local IDE state, signing certificates, and third-party executable tools. |

## Project Highlights

- A single instrument integrates two measurement methods: folded optical path measurement and projection measurement.
- The mechanical structure uses an aluminum-profile tripod frame, custom wire fixtures, and a pulley-based loading mechanism to improve stability.
- The projection method uses a slit, laser, and linear CCD to capture optical feature changes, then estimates deformation through software.
- The WinUI 3 desktop application supports camera view, serial CCD acquisition, experiment records, data export/import, parameter configuration, least-squares fitting, and emergency stop/reset.
- The project also includes a remote pre-lab concept so students can study the principle and observe/operate the experiment before the offline session.
- This repository contains the uploaded offline WinUI 3 instrument program and public-readable documentation. The online pre-lab software was a separate teammate contribution and is not included here.

## 中文说明

项目核心思路不是简单“把传统实验搬到电脑上”，而是把传统拉伸法中的微小形变测量重新组织为一套更稳定、更直观、更适合教学的实验系统：

- 光路折叠法用于保留和强化光杠杆原理的可视化理解。
- 投影法利用狭缝、点光源/线光源和线阵CCD，将钢丝形变量转化为可采集的光学特征尺寸变化。
- 软件端通过串口读取CCD数据，绘制实时曲线，识别边界/特征尺寸，并结合施加力、钢丝直径、原长和CCD比例参数计算杨氏模量。
- 当记录两组及以上数据后，程序可用最小二乘法计算当前材料的杨氏模量和拟合质量。
- 线上预习装置用于把实验原理、模拟操作和远程观察结合起来，提高正式实验前的理解效率。

Because Young's modulus measurements depend strongly on instrument calibration, sample condition, and local setup, this public repository focuses on the measurement system and workflow rather than presenting one set of result data as a universal benchmark.

## Visual Materials

The images below are extracted from the project's own Word/PPT materials.

![Instrument overview](docs/assets/instrument-overview.jpg)

![WinUI experiment interface](docs/assets/winui-experiment-page.jpg)

## Repository Layout

| Path | Description |
|---|---|
| `app/YoungsModuleTest/` | Original WinUI 3 / .NET C# desktop application source code. |
| `simulations/simulated_ccd_reader.py` | Hardware-independent Python simulation for understanding CCD feature extraction. |
| `simulations/uncertainty_budget.py` | Hardware-independent Python calculation demo for Young's modulus and uncertainty. |
| `docs/project-context-and-scope.md` | Project scope, competition context, and explicit team contribution notes. |
| `docs/contribution-scope.md` | Team contribution split, public repository boundaries, and result-data note. |
| `docs/physics-model.md` | Measurement principle and instrument design notes. |
| `docs/software-architecture.md` | Desktop software architecture and main pages. |
| `docs/measurement-data-flow.md` | CCD acquisition and experiment-record data flow. |
| `docs/csharp-implementation-pseudocode.md` | Pseudocode summary of the original C# application logic. |
| `docs/vendor-tooling-notes.md` | Notes on TCD1304 CCD customer materials, SSCOM, CH340 drivers, and third-party redistribution scope. |
| `docs/visual-supplement.md` | Representative images extracted from the original Word/PPT materials. |
| `docs/project-report-extract.md` | Markdown extraction of the original project report. |
| `docs/software-operation-guide.md` | Markdown extraction of the original software operation guide. |
| `docs/presentation-outline.md` | Extracted text outline from the presentation deck. |
| `docs/source-materials/` | Original Word/PPT source materials preserved for reference. |

Run:

```bash
python simulations/simulated_ccd_reader.py
python simulations/uncertainty_budget.py
```

The original software is the C# WinUI 3 application under `app/YoungsModuleTest/`. The Python files under `simulations/` are later public-facing, hardware-independent explanations; they are not the original control program.

## Application Snapshot

The application source keeps the main WinUI 3 pages, project metadata, and app assets. Local build outputs, `.vs/`, `bin/`, `obj/`, signing certificates, user-specific publish files, and packaged executables are not committed.

Third-party CCD module tools, serial assistants, USB-to-serial drivers, and Visual Studio installers are not mirrored in this repository unless their redistribution terms are clear. See `docs/vendor-tooling-notes.md`.

Main UI source locations:

- `app/YoungsModuleTest/MainWindow.xaml`
- `app/YoungsModuleTest/Views/HomePage.xaml`
- `app/YoungsModuleTest/Views/ExperimentPage.xaml`
- `app/YoungsModuleTest/Views/SettingsPage.xaml`

## Source Documents

The original project documents are now included under `docs/source-materials/`:

- `仪器讲解.docx`
- `程序操作.docx`
- `演讲版本.pptx`

For quick reading on GitHub, see:

- `docs/project-context-and-scope.md`
- `docs/contribution-scope.md`
- `docs/project-report-extract.md`
- `docs/software-operation-guide.md`
- `docs/vendor-tooling-notes.md`
- `docs/presentation-outline.md`
- `docs/visual-supplement.md`

## Team And Copyright

This project was developed by a student team. The published competition report records the team members as Ding Qingxiang, Zhu Zhaoxing, Tian Bingzhuo, Guo Zhimei, and Zhao Simeng, with guidance from Wu Jianhai and Yu Xiao. The design, device assembly, software development, experiment operation, data processing, report writing, and presentation work were completed collaboratively.

Known contribution split for the current public-repository scope:

| Member | Main contribution |
|---|---|
| 丁庆祥 | Online pre-lab software / remote learning software. This part is not included in the current repository. |
| 朱兆兴 | Mechanical structure. |
| 田秉卓 | Offline program design included in this repository, plus measurement-principle design. |
| 郭智美 | Measurement-principle design and data processing. |
| 赵思梦 | Measurement-principle design and data processing. |

本项目为团队竞赛成果。软件、硬件、实验操作、报告、视频、数据处理和展示材料均有队友共同参与，每位成员都应获得相应尊重。本仓库由田秉卓维护，用于整理其中可公开复盘的工程代码与说明。

当前公开仓库范围内的已知分工如下：

| 成员 | 主要贡献 |
|---|---|
| 丁庆祥 | 在线版软件 / 远程预习软件；该部分不在当前仓库中。 |
| 朱兆兴 | 机械结构。 |
| 田秉卓 | 当前仓库上传的线下仪器程序设计，以及测量原理设计。 |
| 郭智美 | 测量原理设计与数据处理。 |
| 赵思梦 | 测量原理设计与数据处理。 |

Unless otherwise stated, code and documents authored for this repository are released under the Apache License 2.0 to the extent the contributors have the right to license them. Third-party drivers, SDKs, documents, and device-vendor materials retain their original licenses.
