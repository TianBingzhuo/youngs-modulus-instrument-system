# Vendor Tooling Notes

本文件记录杨氏模量实验仪复现时可能涉及的第三方 CCD / USB-to-serial 工具材料。它们来自购买 TCD1304 CCD 模块时随附的客户资料包，或实验调试中常用的公共工具。

## Repository Policy

This repository does not mirror third-party executable installers, driver archives, or closed-source utilities unless their redistribution terms are clear.

本仓库默认不直接上传第三方 `.exe`、驱动压缩包、闭源调试工具或 IDE 安装器。原因不是这些工具不能使用，而是 GitHub 公开仓库需要区分：

- 使用权：购买设备后用于实验调试和二次开发。
- 再分发权：把工具文件重新上传到公开仓库供他人下载。

两者不完全等同。若后续取得明确授权或公开许可，可再把相应文件补入 `docs/vendor-materials/` 或 `third_party/`，并在 `NOTICE` 中标明来源和许可。

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
