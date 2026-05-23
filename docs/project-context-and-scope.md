# Project Context And Scope

This repository documents a team project for the 2025 National Undergraduate Physics Experiment Competition (Innovation): **Optical Improvement and Remote Pre-Lab Device for Young's Modulus Measurement**.

本文件用于说明项目背景、当前仓库范围，以及队友分工。完整研究报告、软件操作说明和答辩材料保存在 `docs/source-materials/`，便于核对原始叙述。

## Project Problem

Young's modulus measurement is a classic college physics experiment, but the traditional teaching setup has several practical problems:

- the frame and wire can shake during loading and adjustment;
- the optical path is not intuitive enough for first-time learners;
- telescope/scale adjustment takes too much class time;
- manual reading and repeated calculation introduce subjective errors;
- pre-lab learning and offline experiment operation are often disconnected.

本项目的目标不是只做一个计算器，而是围绕“如何让杨氏模量实验更稳定、更直观、更适合教学”重构实验系统。

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

The competition result should still be understood as collaborative team work. This table clarifies the public repository scope; it does not diminish the shared project ownership recorded in the original report.

## How To Read This Repository

Start with:

1. `docs/project-report-extract.md` for the full project narrative.
2. `docs/physics-model.md` for measurement principles.
3. `docs/software-architecture.md` and `docs/csharp-implementation-pseudocode.md` for the C# application logic.
4. `docs/visual-supplement.md` for instrument and UI images.
5. `app/YoungsModuleTest/` for the actual uploaded offline application source.
