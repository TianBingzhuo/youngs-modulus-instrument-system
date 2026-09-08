# Software Operation Guide

This guide describes the maintained C# application. The original [程序操作.docx](source-materials/程序操作.docx) is retained as a historical project document; some features described there were planned but are not implemented.

**The title-bar stop button is a software acquisition lock, not a physical emergency stop.** It cancels CCD polling and prevents new acquisition, data recording and CSV import. It does not remove power, unload the wire, stop the camera or reset hardware. Unlocking requires an explicit new Start action.

The rest of this guide uses the Chinese labels shown by the application.

## 准备运行

先按 [README](../README.md#build-on-windows) 构建 Windows 程序，在 Visual Studio 中选择相应的启动配置进行部署与调试。需要连接 CCD 时，先安装板卡对应的 USB 转串口驱动，确认接线和供电，再进入“实验中心”。

本程序沿用原项目的串口配置：921600 波特率、8 数据位、无校验、1 停止位。它假定一次 CCD 响应由 3648 个 8 位光强值组成。相同的 TCD1304 传感器不代表板卡通信协议相同，请先核对你使用的模块。

## 不连接硬件试用

在仓库根目录执行：

```sh
python simulations/simulated_ccd_reader.py --csv synthetic-ccd.csv
```

进入“实验中心”，在未采集、未锁定的状态下点击“导入实验数据”，选择这个 CSV。程序会显示合成波形、边界和宽度。它可以用来熟悉界面，不应作为实验结果提交。

CSV 必须是 UTF-8 文本，首行为 `pixelIndex,intensity` 或 `像素索引,光强度`，随后恰好 3648 行。索引按 0 到 3647 排列，光强为 0 到 255 的整数。最大文件大小为 128 KiB。格式不正确时保留当前波形并提示错误。

## 采集 CCD 波形

1. 刷新串口列表，选择 CCD 实际使用的端口。
2. 选择积分时间。修改值在下一次点击“开始采集”时发送，采集中不可修改。
3. 点击“开始采集”，观察光强曲线和暗区边界。
4. 需要调整积分时间时，先停止采集，再修改并重新开始。
5. 波形稳定后再记录测量。边界落在明显错误的位置时，先检查光路、接线和曝光，不要只看程序给出的宽度。

“停止采集”取消轮询并清理待处理数据。页面切换会关闭该页的串口与摄像头。串口读写失败或缓存溢出时，程序停止采集并提示重新连接。

原协议没有在本程序中实现帧头、校验和或丢字节重同步。重新连接也不能证明物理信号已经恢复正确，仍需检查波形。

## 记录测量

在“钢丝参数”中填写直径、有效长度和 CCD 到钢丝的比例系数。直径和长度使用毫米。比例系数默认 0.343，是原装置的几何参数，不是所有装置通用的标定值。

载荷输入使用 **kg**。源码的历史字段名叫 `AppliedForce`，拟合时才乘以 9.8 换算成 N。没有接入力传感器自动读取载荷。

输入载荷后点击“记录数据”。每条记录包含载荷、光学宽度、左右边界、材料和时间。全平、全黑等没有明暗变化的波形不能记作有效测量。其他噪声、错位或失焦仍需要人工判断。

数据保存到应用本地数据目录的 `experiment_records.json`。保存失败时会显示“未保存”，内存中的数据仍在，但不能当作已经写入磁盘。首页读取文件失败时不再自动生成示例记录，也不会用空列表覆盖损坏的文件。

**钢丝参数仍保存在应用设置中，不是每条记录独立保存。** 打开旧记录或继续追加前，需要核对当前参数。至少两个有效点可进入源码中的拟合计算，但这一版没有开启杨氏模量和拟合质量的数值显示。

## 拍照与导出

“启动摄像头”用于观察装置；拍照会把 JPEG 保存到 Windows 图片库的“杨氏模量测试仪”文件夹。当前页面只保留最近 16 张缩略图，已保存的 JPEG 不会因此删除。摄像头权限和图片库写入权限需要允许。

底部按钮的用途是：

| 按钮 | 文件 | 内容 |
|---|---|---|
| 导出CCD数据 | CSV | 当前帧的像素索引和原始光强值。 |
| 保存实验报告 | TXT | 当前测量记录、界面参数与统计文本。 |
| 导入实验数据 | CSV | 单帧 CCD 数据，不是整个实验档案。 |

报告不是完整的参数备份，也不保证包含拟合数值。原代码中的 `SaveCompleteExperimentData()` 未接入保存流程，`ConvertBitmapToBase64()` 仍返回空字符串。照片、完整 CCD 帧和所有设备设置不能依赖 JSON 实验记录自动恢复。

## 软件停止与恢复

按标题栏的电源形状按钮后，按钮变红。程序锁定采集、记录和导入入口，等待采集任务结束，并忽略旧采集任务排队的画面更新。已经进入串口驱动的写入可能仍需完成或超时，因此这不是实时硬件急停。

摄像头观察可以继续。需要停止电机、切断供电或卸载砝码时，使用仪器的实体控制。

再次点击按钮只解除软件锁定。它不会执行 `ResetInstrument()` 的硬件动作，也不会自动开始采集。确认设备安全、波形和参数正确后，再手动开始。

## 常见问题

| 现象 | 先检查 |
|---|---|
| 找不到串口 | USB 驱动、连接线、供电，以及端口是否被其他程序占用。 |
| 已连接但没有波形 | 模块是否支持当前波特率、0xA2 请求和 3648 字节响应约定。 |
| 提示缓存溢出 | 停止后重新连接，检查设备是否持续发送、协议是否匹配；不要把超长数据直接截成“有效帧”。 |
| CSV 被拒绝 | 编码、表头、完整行数、索引顺序和光强范围。 |
| 记录文件读写失败 | 先备份原文件，再检查磁盘空间、权限与 JSON 格式。不要用初始化操作覆盖唯一副本。 |
| 校准或系统体检页没有功能 | 这些页面目前是预留页，不是自动校准或设备诊断。 |
