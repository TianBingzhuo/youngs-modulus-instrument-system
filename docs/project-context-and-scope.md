# Project Context And Scope

This repository documents our team project for the 2025 National Undergraduate Physics Experiment Competition (Innovation): **Optical Improvement and Remote Pre-Lab Device for Young's Modulus Measurement**.

## Project Problem

Young's modulus measurement is a classic college physics experiment, but the traditional teaching setup has several practical problems that we experienced firsthand:

- the frame and wire can shake during loading and adjustment;
- the optical path isn't intuitive enough for first-time learners;
- telescope/scale adjustment takes too much class time;
- manual reading and repeated calculation introduce subjective errors;
- pre-lab learning and offline experiment operation are often disconnected.

Our goal wasn't just to build a calculator. We wanted to rethink how the Young's modulus experiment could be made more stable, more intuitive, and more suitable for teaching.

## Project Solution

The project combines four parts:

| Part | Role |
|---|---|
| Folded optical path method | Keeps the optical-lever idea visible and provides an independent measurement route. |
| Projection method with linear CCD | Converts slit/optical-feature changes into a CCD intensity curve and software-readable deformation value. |
| Improved mechanical structure | Uses an aluminum-profile triangular frame, custom fixtures, movable platforms, and pulley loading to reduce shaking. |
| Teaching workflow | Combines offline measurement with an online pre-lab concept so students can understand the principle before entering the lab. |

The offline instrument software included in this repository focuses on the projection/CCD route: serial data acquisition, live CCD curve display, boundary/feature estimation, measurement records, least-squares fitting, import/export, and emergency stop/reset.

## Repository Scope

Included here:

- `app/YoungsModuleTest/`: the uploaded WinUI 3 / C# offline instrument program.
- `docs/source-materials/`: original Word/PPT source materials for the report, operation guide, and presentation.
- `docs/assets/`: representative figures extracted from the original materials.
- `docs/*.md`: GitHub-readable explanations of the physics model, software architecture, data flow, and operation guide.
- `simulations/`: hardware-independent Python explanations for CCD feature extraction and uncertainty calculation.

Not included here:

- the online pre-lab software written for the remote learning part;
- physical CAD manufacturing files beyond the preserved pictures and report descriptions;
- packaged Windows build outputs, local Visual Studio state, signing certificates, and device-vendor drivers.

This separation is intentional. The current repository is centered on the uploaded offline program, the optical/measurement principle design, and public-readable documentation.

## Team Contribution Notes

This was a five-person team project. The original report records that all five members participated in the project plan. The current public repository uses the following more explicit contribution note:

| Member | Main contribution |
|---|---|
| 丁庆祥 | Online pre-lab software / remote learning software. This part is not included in the current repository. |
| 朱兆兴 | Mechanical structure. |
| 田秉卓 | Offline program design included in this repository, plus measurement-principle design. |
| 郭智美 | Measurement-principle design and data processing. |
| 赵思梦 | Measurement-principle design and data processing. |

中文分工说明：

| 成员 | 主要贡献 |
|---|---|
| 丁庆祥 | 在线版软件 / 远程预习软件。该部分不在当前仓库中。 |
| 朱兆兴 | 机械结构。 |
| 田秉卓 | 当前仓库上传的线下仪器程序设计，以及测量原理设计。 |
| 郭智美 | 测量原理设计与数据处理。 |
| 赵思梦 | 测量原理设计与数据处理。 |

The competition result should still be understood as collaborative team work. This table clarifies the public repository scope; it doesn't diminish the shared project ownership recorded in the original report.

## How To Read This Repository

Start with:

1. `docs/project-report-extract.md` for the full project narrative.
2. `docs/physics-model.md` for measurement principles.
3. `docs/software-architecture.md` and `docs/csharp-implementation-pseudocode.md` for the C# application logic.
4. `docs/visual-supplement.md` for instrument and UI images.
5. `app/YoungsModuleTest/` for the actual uploaded offline application source.

---

# 中文版本

本仓库记录的是我们团队参加 2025 年全国大学生物理实验竞赛（创新）的作品：**杨氏模量测量实验的光学改进及远程预习装置**。

本文件用于说明项目背景、当前仓库范围，以及队友分工。完整研究报告、软件操作说明和答辩材料保存在 `docs/source-materials/`，便于核对原始叙述。

## 项目问题

杨氏模量测量是大学物理实验中的经典项目，但传统教学装置存在几个我们实际碰到的问题：

- 加载和调节时支架和钢丝容易晃动；
- 光路对初次接触的学生来说不够直观；
- 调望远镜/标尺花了太多课上时间；
- 人工读数和反复计算引入主观误差；
- 课前预习和线下实验操作常常是脱节的。

我们的目标不只是做一个计算器，而是围绕"如何让杨氏模量实验更稳定、更直观、更适合教学"来重构实验系统。

## 项目方案

项目由四个部分组成：

| Part | Role |
|---|---|
| Folded optical path method | Keeps the optical-lever idea visible and provides an independent measurement route. |
| Projection method with linear CCD | Converts slit/optical-feature changes into a CCD intensity curve and software-readable deformation value. |
| Improved mechanical structure | Uses an aluminum-profile triangular frame, custom fixtures, movable platforms, and pulley loading to reduce shaking. |
| Teaching workflow | Combines offline measurement with an online pre-lab concept so students can understand the principle before entering the lab. |

本仓库中包含的线下仪器软件主要聚焦投影法/CCD 路线：串口数据采集、CCD 实时曲线显示、边界/特征估算、测量记录、最小二乘拟合、导入导出以及紧急停机/复位。

## 仓库范围

包含在内：

- `app/YoungsModuleTest/`：上传的 WinUI 3 / C# 线下仪器程序。
- `docs/source-materials/`：报告、操作说明和答辩的原始 Word/PPT 材料。
- `docs/assets/`：从原始材料中提取的代表性图片。
- `docs/*.md`：适合在 GitHub 上阅读的物理模型、软件架构、数据流和操作说明。
- `simulations/`：不依赖硬件的 Python 原理说明（CCD 特征提取和不确定度计算）。

不包含在内：

- 远程预习部分的线上预习软件；
- 超出保留图片和报告描述之外的实体 CAD 制造文件；
- 打包的 Windows 构建产物、本地 Visual Studio 状态、签名证书和设备供应商驱动。

这种分离是有意为之。当前仓库以线下程序、光学/测量原理设计和公开可读的文档为核心。

## 团队贡献说明

这是一个五人团队项目。原始报告记载五位成员都参与了方案设计。当前公开仓库使用以下更明确的贡献说明：

| 成员 | 主要贡献 |
|---|---|
| 丁庆祥 | 在线版软件 / 远程预习软件。该部分不在当前仓库中。 |
| 朱兆兴 | 机械结构。 |
| 田秉卓 | 当前仓库上传的线下仪器程序设计，以及测量原理设计。 |
| 郭智美 | 测量原理设计与数据处理。 |
| 赵思梦 | 测量原理设计与数据处理。 |

竞赛成果仍应被理解为团队的协作成果。这张表澄清的是公开仓库的范围，并不削弱原始报告中记载的共同项目所有权。

## 如何阅读本仓库

建议从以下文件开始：

1. `docs/project-report-extract.md` — 完整的项目叙述。
2. `docs/physics-model.md` — 测量原理。
3. `docs/software-architecture.md` 和 `docs/csharp-implementation-pseudocode.md` — C# 应用逻辑。
4. `docs/visual-supplement.md` — 仪器和 UI 图片。
5. `app/YoungsModuleTest/` — 实际上传的线下应用源码。
