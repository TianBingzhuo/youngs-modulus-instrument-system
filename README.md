# Young's Modulus Optical Measurement Instrument

杨氏模量光学测量实验仪

This repository documents a Young's modulus experiment-improvement project for the 2025 National Undergraduate Physics Experiment Competition (Innovation). The project is titled **Optical Improvement and Remote Pre-Lab Device for Young's Modulus Measurement**.

本仓库整理的是“杨氏模量测量实验的光学改进及远程预习装置”的工程资料。项目针对传统杨氏模量实验中装置稳定性不足、调节繁琐、光路不直观、人工读数误差较大的问题，设计了双测量方法、CCD自动读数软件和线上预习装置。

## Project Highlights

- A single instrument integrates two measurement methods: folded optical path measurement and projection measurement.
- The mechanical structure uses an aluminum-profile tripod frame, custom wire fixtures, and a pulley-based loading mechanism to improve stability.
- The projection method uses a slit, laser, and linear CCD to capture optical feature changes, then estimates deformation through software.
- The WinUI 3 desktop application supports camera view, serial CCD acquisition, experiment records, data export/import, parameter configuration, least-squares fitting, and emergency stop/reset.
- The project also includes a remote pre-lab concept so students can study the principle and observe/operate the experiment before the offline session.

## 中文说明

项目核心思路不是简单“把传统实验搬到电脑上”，而是把传统拉伸法中的微小形变测量重新组织为一套更稳定、更直观、更适合教学的实验系统：

- 光路折叠法用于保留和强化光杠杆原理的可视化理解。
- 投影法利用狭缝、点光源/线光源和线阵CCD，将钢丝形变量转化为可采集的光学特征尺寸变化。
- 软件端通过串口读取CCD数据，绘制实时曲线，识别边界/特征尺寸，并结合施加力、钢丝直径、原长和CCD比例参数计算杨氏模量。
- 当记录两组及以上数据后，程序可用最小二乘法计算当前材料的杨氏模量和拟合质量。
- 线上预习装置用于把实验原理、模拟操作和远程观察结合起来，提高正式实验前的理解效率。

## Repository Layout

| Path | Description |
|---|---|
| `app/YoungsModuleTest/` | WinUI 3 / .NET desktop application source code. |
| `src/simulated_ccd_reader.py` | Small simulated CCD reader for understanding feature extraction. |
| `src/uncertainty_budget.py` | Minimal Young's modulus and uncertainty calculation demo. |
| `docs/physics-model.md` | Measurement principle and instrument design notes. |
| `docs/software-architecture.md` | Desktop software architecture and main pages. |
| `docs/measurement-data-flow.md` | CCD acquisition and experiment-record data flow. |

Run:

```bash
python src/simulated_ccd_reader.py
python src/uncertainty_budget.py
```

## Application Snapshot

The application source keeps the main WinUI 3 pages, project metadata, and app assets. Local build outputs, `.vs/`, `bin/`, `obj/`, signing certificates, user-specific publish files, and packaged executables are not committed.

Main UI source locations:

- `app/YoungsModuleTest/MainWindow.xaml`
- `app/YoungsModuleTest/Views/HomePage.xaml`
- `app/YoungsModuleTest/Views/ExperimentPage.xaml`
- `app/YoungsModuleTest/Views/SettingsPage.xaml`

## Team And Copyright

This project was developed by a student team. The published competition report records the team members as Ding Qingxiang, Zhu Zhaoxing, Tian Bingzhuo, Guo Zhimei, and Zhao Simeng, with guidance from Wu Jianhai and Yu Xiao. The design, device assembly, software development, experiment operation, data processing, report writing, and presentation work were completed collaboratively.

本项目为团队竞赛成果。软件、硬件、实验操作、报告、视频、数据处理和展示材料均有队友共同参与，每位成员都应获得相应尊重。本仓库由田秉卓维护，用于整理其中可公开复盘的工程代码与说明。

Unless otherwise stated, code and documents authored for this repository are released under the Apache License 2.0. Third-party drivers, SDKs, documents, and device-vendor materials retain their original licenses.
