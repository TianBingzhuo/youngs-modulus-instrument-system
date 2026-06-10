# Contribution Scope

This file explains what's in scope and what isn't for the public repository. Our Young's modulus instrument was a team competition effort, and this repo only covers the parts that can be openly reviewed -- the engineering code, design notes, and project materials that we're comfortable sharing publicly.

## Project Scope

This repository focuses on:

- The uploaded offline WinUI 3 / C# instrument program.
- Measurement-principle and software documentation extracted from project materials.
- Visual materials that come from the team's own Word/PPT files.
- Hardware-independent Python explanations for CCD feature extraction and uncertainty calculation.
- Public notes about vendor tooling and third-party redistribution boundaries.

The online pre-lab software was a separate teammate contribution and is not included here.

## Team Contribution Notes

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

## Result-Data Boundary

Young's modulus measurement results can vary quite a bit depending on instrument calibration, wire condition, how you load the weights, optical alignment, CCD scaling, and the local environment. So for the public release, we treat result data as setup-specific rather than something universal.

Public documentation should emphasize:

- measurement principle;
- instrument structure;
- software workflow;
- data-flow and fitting method;
- calibration parameters that must be remeasured on each physical setup.

It should avoid presenting one team's experimental result table as a general performance guarantee.

## Public Repository Boundaries

Included:

- C# offline software source.
- Original Word/PPT materials already reviewed for public repository use.
- Visual supplement extracted from project-owned materials.
- Markdown documentation for GitHub reading.

Not included:

- Online pre-lab software.
- Unreviewed raw experiment-result data.
- Packaged Windows build outputs, local Visual Studio state, signing certificates, and third-party executable tools.
- Vendor driver archives or closed-source utilities without clear redistribution permission.

---

# 中文版本

本文件用于说明当前公开仓库里的贡献边界、材料边界和结果数据边界。杨氏模量实验仪是团队竞赛成果，本仓库只整理其中可公开复盘的工程代码、原理说明和项目材料。

## 项目范围

本仓库重点关注：

- 上传的线下 WinUI 3 / C# 仪器程序。
- 从项目材料中提取的测量原理和软件文档。
- 来自团队自有 Word/PPT 文件的图片资料。
- 不依赖硬件的 Python 原理说明（CCD 特征提取和不确定度计算）。
- 关于供应商工具和第三方再分发边界的公开说明。

线上预习软件是另一位队友的独立贡献，不在本仓库中。

## 团队贡献说明

| 成员 | 主要贡献 |
|---|---|
| 丁庆祥 | 在线版软件 / 远程预习软件。该部分不在当前仓库中。 |
| 朱兆兴 | 机械结构。 |
| 田秉卓 | 当前仓库上传的线下仪器程序设计，以及测量原理设计。 |
| 郭智美 | 测量原理设计与数据处理。 |
| 赵思梦 | 测量原理设计与数据处理。 |

## 结果数据边界

杨氏模量的测量结果会因仪器标定、钢丝状态、加载方式、光路对准、CCD 缩放比例和现场环境而有所不同。因此在公开发布时，我们把结果数据视为依赖于具体装置的，而不是通用基准。

公开文档应该着重说明：

- 测量原理；
- 仪器结构；
- 软件工作流；
- 数据流和拟合方法；
- 每套物理装置都需要重新标定的参数。

不应该把某一组的实验数据表当作通用性能保证来展示。

## 公开仓库边界

包含在内：

- C# 线下软件源码。
- 已审核可用于公开仓库的原始 Word/PPT 材料。
- 从项目自有材料中提取的图片补充。
- 用于 GitHub 阅读的 Markdown 文档。

不包含在内：

- 线上预习软件。
- 未审核的原始实验结果数据。
- 打包的 Windows 构建产物、本地 Visual Studio 状态、签名证书和第三方可执行工具。
- 没有明确再分发许可的供应商驱动压缩包或闭源工具。
