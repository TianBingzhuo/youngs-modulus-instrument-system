# Project Context And Scope

This repository documents our entry to the 2025 National Undergraduate Physics Experiment Competition (Innovation): **Optical Improvement and Remote Pre-Lab Device for Young's Modulus Measurement**.

## Problem

The traditional tensile-method teaching setup exposed several practical problems during operation:

- vibration during loading and adjustment;
- an optical path that is difficult for first-time learners to understand;
- time-consuming telescope and scale adjustment;
- subjective manual reading and repeated calculation;
- a gap between pre-lab learning and instrument operation.

The project therefore treated the experiment as a complete teaching and measurement system rather than only adding a calculator.

## System Design

| Part | Role |
|---|---|
| Folded optical path | Preserves a visible optical-lever route and provides an independent measurement method. |
| Slit projection and linear CCD | Converts wire elongation into an optical feature that can be sampled and processed digitally. |
| Mechanical redesign | Uses an aluminum-profile triangular frame, fixtures, movable platforms, and pulley loading to improve stability and adjustment. |
| Offline WinUI 3 software | Handles system checks, serial acquisition, live waveform display, feature detection, records, fitting, and export/import. |
| Online pre-lab | A separate teammate contribution for learning the principle before the physical lab. |

## Repository Coverage

- `app/YoungsModuleTest/`: offline C# / WinUI 3 application.
- `docs/source-materials/`: reviewed Word/PPT report, operation guide, and presentation.
- `docs/assets/`: figures extracted from the team documents.
- `docs/*.md`: physics, architecture, data-flow, and operation notes for browser reading.
- `simulations/`: later hardware-independent explanations of CCD feature extraction and uncertainty propagation.

Physical CAD and the separate online pre-lab source are represented through the project documents and contribution record, not as source trees in this repository. Build output and device/vendor tools are handled according to their own ownership and distribution terms.

## Team Contributions

| Member | Main contribution |
|---|---|
| 丁庆祥 | Online pre-lab and remote-learning software. |
| 朱兆兴 | Mechanical structure. |
| 田秉卓 | Offline C# program and measurement-principle design. |
| 郭智美 | Measurement-principle design and data processing. |
| 赵思梦 | Measurement-principle design and data processing. |

The competition result remains a collaborative team achievement. See [../LICENSE_SCOPE.md](../LICENSE_SCOPE.md) for authorship and reuse terms.

## Reading Order

1. [project-report-extract.md](project-report-extract.md)
2. [physics-model.md](physics-model.md)
3. [software-architecture.md](software-architecture.md)
4. [csharp-implementation-pseudocode.md](csharp-implementation-pseudocode.md)
5. [visual-supplement.md](visual-supplement.md)
6. `../app/YoungsModuleTest/`

---

# 项目背景与范围

本仓库记录的是我们参加 2025 年全国大学生物理实验竞赛（创新）的作品：**杨氏模量测量实验的光学改进及远程预习装置**。

## 问题背景

传统拉伸法实验在实际操作中存在支架和钢丝易振动、光路理解门槛高、望远镜和标尺调节耗时、人工读数带来主观误差，以及预习与线下操作脱节等问题。因此，项目把它作为一套完整的教学和测量系统来重新设计，而不是只增加一个计算器。

## 系统方案

| 组成 | 作用 |
|---|---|
| 光路折叠法 | 保留可视化的光杠杆路径，并提供独立测量方法。 |
| 狭缝投影与线阵 CCD | 把钢丝伸长转化为可采样、可处理的光学特征。 |
| 机械结构改进 | 使用铝型材三角框架、夹具、移动平台和滑轮加载，提高稳定性与可调性。 |
| 线下 WinUI 3 软件 | 完成系统检查、串口采集、实时波形、特征识别、记录、拟合和导入导出。 |
| 在线预习 | 由队友独立负责，用于正式实验前理解原理。 |

## 仓库内容

- `app/YoungsModuleTest/`：线下 C# / WinUI 3 程序。
- `docs/source-materials/`：已审核的 Word/PPT 报告、操作说明和答辩材料。
- `docs/assets/`：从团队文档中提取的图片。
- `docs/*.md`：便于网页阅读的物理原理、架构、数据流和操作说明。
- `simulations/`：后续编写的不依赖硬件的 CCD 特征提取和不确定度传播说明。

机械 CAD 和独立的在线预习源码没有作为源码树放入当前仓库，其工作仍通过项目文档和分工记录体现。构建产物及设备/厂商工具遵循各自权利与分发条款。

## 团队分工

| 成员 | 主要贡献 |
|---|---|
| 丁庆祥 | 在线预习与远程学习软件。 |
| 朱兆兴 | 机械结构。 |
| 田秉卓 | 线下 C# 程序与测量原理设计。 |
| 郭智美 | 测量原理设计与数据处理。 |
| 赵思梦 | 测量原理设计与数据处理。 |

竞赛成果仍属于团队协作成果。署名和复用说明见 [../LICENSE_SCOPE.md](../LICENSE_SCOPE.md)。
