# Measurement Data Flow

This document walks through how the instrument software turns CCD and experiment inputs into usable Young's modulus records. It's based on the original operation guide and report.

## Inputs

The experiment page brings together several data sources:

- applied load entered by the operator;
- CCD intensity data received through the selected serial port;
- camera preview for observing the instrument state;
- wire parameters: diameter, original length, and CCD-to-wire scale factor;
- experiment-state information such as current force, recorded points, and emergency-stop state.

## CCD Acquisition And Curve Quality

The operation guide describes a practical CCD adjustment loop that we found really helpful during actual experiments:

1. Refresh the serial-port list.
2. Select the port used by the CCD module.
3. Start CCD acquisition and observe the real-time intensity curve.
4. If the curve bottom touches the chart boundary, reduce integration time.
5. If the upper/lower contrast is unclear, increase integration time.
6. Record data only after the optical feature is stable enough.

This gives the operator direct feedback before any data is written into the measurement record -- which saved us from recording a lot of garbage data early on.

## Feature Extraction

```text
CCD byte stream
  -> numeric intensity samples
  -> smoothing / boundary search
  -> optical feature width or boundary position
  -> deformation estimate through CCD scale factor
```

The project software keeps this process visible through the live curve. The goal isn't just to compute a final number; it's to let the user judge whether the acquisition state looks reasonable before committing to a measurement.

## Measurement Record

For every recorded point, the application stores:

- applied force;
- CCD-derived length/deformation value;
- current wire and scale parameters;
- experiment record information for later recovery/export.

After at least two records are collected, the software estimates Young's modulus with least-squares fitting and reports fitting quality. This matches the teaching goal: students can see how additional measurement points affect the result, which is honestly a much better learning experience than just plugging numbers into a formula once.

## Export And Import

The software supports:

- exporting experiment images;
- exporting CCD raw data;
- exporting experiment records;
- importing historical data to recover previous results.

This makes the application useful not just for live measurement, but also for report writing, review, and teaching demonstrations.

## Hardware-Independent Simulations

The scripts in `simulations/` are small standalone demos we wrote for public explanation and hardware-free inspection:

- `simulated_ccd_reader.py`: generates CCD-like data and finds a synthetic peak.
- `uncertainty_budget.py`: computes a Young's modulus value and a simple uncertainty interval.

They do not replace the original WinUI 3 / C# application or the physical instrument. The original application logic is under `app/YoungsModuleTest/`; the Python files only preserve the data-processing idea in a lighter simulation form.

---

# 中文版本

本文件介绍仪器软件如何把 CCD 和实验输入变成可用的杨氏模量记录。内容来自原始操作说明和报告。

## 输入

实验页面整合了多个数据来源：

- 操作员输入的施加载荷；
- 通过选定串口接收的 CCD 光强数据；
- 摄像头预览，用于观察仪器状态；
- 钢丝参数：直径、原长和 CCD 到钢丝的比例系数；
- 实验状态信息，如当前施加力、已记录的数据点和紧急停机状态。

## CCD 采集与曲线质量

操作说明里描述了一个实用的 CCD 调节循环，实际做实验时这套流程帮了大忙：

1. 刷新串口列表。
2. 选择 CCD 模块使用的串口。
3. 启动 CCD 采集，观察实时光强曲线。
4. 如果曲线底部已经贴到图表边界，就减小积分时间。
5. 如果上下对比度不明显，就增大积分时间。
6. 等光学特征稳定后再记录数据。

这样操作员在数据写入测量记录之前就能直观判断——这帮我们省了不少废数据。

## 特征提取

```text
CCD byte stream
  -> numeric intensity samples
  -> smoothing / boundary search
  -> optical feature width or boundary position
  -> deformation estimate through CCD scale factor
```

项目软件通过实时曲线让这个过程始终可见。目的不只是算出最终数字，更是让用户在正式记录前能判断采集状态是否合理。

## 测量记录

每记录一个数据点，程序会保存：

- 施加力；
- CCD 推导的长度/形变值；
- 当前的钢丝和比例参数；
- 实验记录信息，便于后续恢复/导出。

收集到至少两组数据后，软件会用最小二乘法估算杨氏模量并报告拟合质量。这和教学目标是一致的：学生可以看到每多加一个数据点对结果的影响——说实话，这比只代一次公式的体验好多了。

## 导出与导入

软件支持：

- 导出实验图片；
- 导出 CCD 原始数据；
- 导出实验记录；
- 导入历史数据以恢复之前的结果。

这让应用不仅适用于现场测量，也能用来写报告、回顾数据和做教学演示。

## 不依赖硬件的仿真

`simulations/` 下的脚本是我们为公开展示和无硬件体验写的小演示程序：

- `simulated_ccd_reader.py`：生成类似 CCD 的数据并找到合成峰值。
- `uncertainty_budget.py`：计算杨氏模量值和简单的不确定度区间。

它们不能替代原始的 WinUI 3 / C# 应用或物理仪器。原始程序逻辑在 `app/YoungsModuleTest/` 下；Python 文件只是用更轻量的仿真形式保留了数据处理的思路。
