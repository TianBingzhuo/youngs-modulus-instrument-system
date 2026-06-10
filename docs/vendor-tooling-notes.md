# Vendor Tooling Notes

This file keeps track of the third-party CCD and USB-to-serial tools you might need when trying to reproduce our instrument. Most of these came with the TCD1304 CCD module we bought, or were tools we used during debugging.

## Repository Policy

This repository does not mirror third-party executable installers, driver archives, or closed-source utilities unless their redistribution terms are clear.

It's not that these tools aren't useful -- they absolutely are. The issue is that a public GitHub repo needs to distinguish between:

- **Right to use:** you bought the equipment and use the tools for experiments and development.
- **Right to redistribute:** you re-upload those tool files to a public repo for others to download.

These two things aren't the same. If we later get explicit permission or find a clear public license, we can add the files to `docs/vendor-materials/` or `third_party/` and note the source and license in a `NOTICE` file.

## Local Customer Package

The local TCD1304 customer package contains the following files:

| Local file | Current repository decision | Reason |
|---|---|---|
| `说明文档 -20210202.pdf` | Not mirrored for now | Vendor document; useful for reference, but no explicit redistribution license was found in the local package. |
| `单片机采集程序-STM32F103C8T6.rar` | Not mirrored for now | Vendor sample firmware package; useful for development, but redistribution terms should be confirmed first. |
| `上位机程序C#-VS2019.rar` | Not mirrored for now | Vendor upper-computer sample package; useful for development, but redistribution terms should be confirmed first. |
| `CCD上位机.exe` | Not mirrored | Third-party/vendor executable binary without explicit public redistribution terms. |
| `串口调试助手sscom5.13.1.exe` | Not mirrored | SSCOM is widely mirrored online and often marked as freeware, but a clear redistribution license was not confirmed. |
| `sscom51.ini` | Not mirrored | Runtime configuration file for SSCOM; not useful without the executable and may contain local settings. |
| `USB转TTL CH340模块驱动.rar` | Not mirrored | CH340/CH341 drivers are publicly available from WCH and should be downloaded from the chip vendor or authorized source. |
| `vs2019安装.exe` | Not mirrored | Visual Studio is proprietary Microsoft software; users should download it from Microsoft. |

## Reproducibility Guidance

For reproducing the instrument:

1. Use the C# WinUI 3 source code in `app/YoungsModuleTest/` as the project implementation included in this repository.
2. Use `docs/software-operation-guide.md` for the operation workflow.
3. Install USB-to-serial drivers from the USB-serial chip vendor or board vendor instead of relying on a mirrored driver archive.
4. Use any trusted serial-port assistant, such as SSCOM or an open-source equivalent, for protocol debugging.
5. Treat CCD module vendor examples as reference material unless their license explicitly allows public redistribution.

## Notes On Visual Studio

The original vendor package references Visual Studio 2019 for the sample upper-computer project. This repository does not redistribute Visual Studio installers. The source code included here targets .NET / WinUI 3 and should be opened with a suitable Microsoft-supported Visual Studio installation.

---

# 中文版本

本文件记录杨氏模量实验仪复现时可能涉及的第三方 CCD / USB 转串口工具材料。它们来自购买 TCD1304 CCD 模块时随附的客户资料包，或实验调试中常用的公共工具。

## 仓库策略

本仓库默认不直接上传第三方 `.exe`、驱动压缩包、闭源调试工具或 IDE 安装器。原因不是这些工具不好用——恰恰相反，它们非常实用。问题在于 GitHub 公开仓库需要区分：

- **使用权**：购买设备后用于实验调试和二次开发。
- **再分发权**：把工具文件重新上传到公开仓库供他人下载。

这两件事不完全一样。如果后续取得明确授权或找到公开的许可协议，我们可以把相应文件补入 `docs/vendor-materials/` 或 `third_party/`，并在 `NOTICE` 中标明来源和许可。

## 本地客户资料包

本地 TCD1304 客户资料包包含以下文件：

| Local file | Current repository decision | Reason |
|---|---|---|
| `说明文档 -20210202.pdf` | Not mirrored for now | Vendor document; useful for reference, but no explicit redistribution license was found in the local package. |
| `单片机采集程序-STM32F103C8T6.rar` | Not mirrored for now | Vendor sample firmware package; useful for development, but redistribution terms should be confirmed first. |
| `上位机程序C#-VS2019.rar` | Not mirrored for now | Vendor upper-computer sample package; useful for development, but redistribution terms should be confirmed first. |
| `CCD上位机.exe` | Not mirrored | Third-party/vendor executable binary without explicit public redistribution terms. |
| `串口调试助手sscom5.13.1.exe` | Not mirrored | SSCOM is widely mirrored online and often marked as freeware, but a clear redistribution license was not confirmed. |
| `sscom51.ini` | Not mirrored | Runtime configuration file for SSCOM; not useful without the executable and may contain local settings. |
| `USB转TTL CH340模块驱动.rar` | Not mirrored | CH340/CH341 drivers are publicly available from WCH and should be downloaded from the chip vendor or authorized source. |
| `vs2019安装.exe` | Not mirrored | Visual Studio is proprietary Microsoft software; users should download it from Microsoft. |

## 复现指引

如果要复现本仪器：

1. 使用 `app/YoungsModuleTest/` 中的 C# WinUI 3 源码作为本项目包含的程序实现。
2. 参考 `docs/software-operation-guide.md` 了解操作流程。
3. 从 USB 转串口芯片厂商或开发板厂商安装驱动，而不是依赖仓库里镜像的驱动包。
4. 使用任何可信赖的串口调试助手（如 SSCOM 或开源替代品）进行协议调试。
5. CCD 模块供应商的示例程序作为参考材料使用，除非其许可明确允许公开再分发。

## 关于 Visual Studio

原始供应商资料包中引用了 Visual Studio 2019 用于示例上位机项目。本仓库不再分发 Visual Studio 安装程序。本仓库包含的源码基于 .NET / WinUI 3，应使用合适的微软官方 Visual Studio 打开。
