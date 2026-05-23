# Contribution Scope

本文件用于说明当前公开仓库里的贡献边界、材料边界和结果数据边界。杨氏模量实验仪是团队竞赛成果，本仓库只整理其中可公开复盘的工程代码、原理说明和项目材料。

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

Young's modulus measurement results can vary with instrument calibration, wire condition, loading process, optical alignment, CCD scaling, and local environment. For public release, this repository treats result data as setup-specific rather than universal.

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
