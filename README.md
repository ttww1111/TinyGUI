# TinyGUI

TinyPNG 图片压缩工具的 Windows 桌面客户端，基于 WPF / .NET Framework 4.8 构建。支持批量、多线程压缩，内置多 Key 轮换、多语言界面与便携模式。

> 本仓库为 [chenjing1294/TinyGUI](https://github.com/chenjing1294/TinyGUI) 的修改版本，在原版基础上增加了版本号展示、多语言、无用资源清理与多 Key 轮换等特性。

## 功能特性

- **批量压缩**：拖入图片或文件夹（含子目录递归扫描），一键压缩
- **多线程并行**：内置线程池并行处理，压缩速度快
- **多 Key 轮换（KeyPool）**：可配置多个 TinyPNG API Key 自动轮换，突破单 Key 月度配额限制
- **压缩模式**：支持 Scale / Fit / Cover / Thumb 等 TinyPNG 接口模式（方法名保持英文，Tooltip 提供中文说明）
- **多语言界面**：简体中文 / 繁體中文 / English / Deutsch，切换即时生效，无需重启
- **便携优先**：配置文件优先存于程序所在目录，便于放到 U 盘或随压缩包携带
- **配额显示**：状态栏实时显示每个 Key 的剩余次数与总额度
- **版本号**：标题栏与窗口右上角显示当前版本

## 支持的界面语言

| 语言       | Culture   |
| ---------- | --------- |
| 简体中文   | `zh`      |
| 繁體中文   | `zh-hant` |
| English    | `en`      |
| Deutsch    | `de-de`   |

## 下载

前往 [Releases](https://github.com/ttww1111/TinyGUI/releases) 页面下载：

- `TinyGUI-green-1.1.0.zip`：绿色版，解压即用，无需安装
- `TinyGUI-setup.exe`：安装版，标准安装程序

> 运行需要 Windows 与 .NET Framework 4.8 运行环境。

## 使用说明

1. 在 [TinyPNG Developer](https://tinypng.com/developers) 免费申请 API Key（免费账户每月 500 次压缩）。
2. 打开 TinyGUI，在设置中填入 API Key（支持填写多个，自动轮换）。
3. 将图片或文件夹拖入主窗口，或点击按钮添加。
4. 选择压缩模式（Scale / Fit / Cover / Thumb）与输出选项。
5. 点击开始压缩，状态栏实时显示进度与剩余配额。

## 关于 API Key 与多 Key 轮换

TinyPNG 免费账户每月 500 次压缩。TinyGUI 的 **KeyPool** 允许你填写多个 Key，程序在压缩时自动轮换使用，从而成倍提升可用额度。Key 仅保存在本地配置文件中，不会上传。

## 便携模式

配置文件 `settings.json` 的保存逻辑为：

- 优先保存到**程序所在目录**（适合绿色版 / U 盘携带）；
- 若该目录不可写，则回退保存到 `%LocalAppData%\TinyGUI\settings.json`。

因此绿色版可直接放在任意位置使用，配置随程序走。

## 从源码构建

### 环境要求

- Visual Studio 2019 / 2022（含“.NET 桌面开发 / WPF”工作负载），或
- .NET 8 SDK（项目引用 `Microsoft.NETFramework.ReferenceAssemblies.net48`，无需单独安装 .NET Framework 4.8 开发包即可编译 `net48`）

### 编译

```bash
dotnet build TinyGUI/TinyGUI.csproj -c Release
```

产物位于 `TinyGUI/TinyGUI/bin/Release/net48/`。

### 打包安装程序

使用 [Inno Setup](https://jrsoftware.org/isinfo.php) 打开 `publish/TinyGUI-setup.iss` 编译安装包；版本号由 `net48\TinyGUI.exe` 自动读取，无需手动填写。

## 项目结构

```
TinyGUI/
├─ TinyGUI/                # WPF 源码（.NET Framework 4.8）
│  ├─ Views/              # 主窗口与各弹窗的 XAML / 逻辑
│  ├─ ViewModels/         # MainModel 等
│  ├─ Services/           # ImageCompressor / KeyPool / TinifyKeyGate
│  ├─ Models/             # ApiKeyItem / ImageItem
│  ├─ Properties/         # Resources.*.resx 多语言资源
│  ├─ Loc.cs              # 可通知的多语言绑定源
│  └─ AppSettings.cs      # 便携优先的配置读写
├─ TinyGUI.sln
└─ publish/               # Inno 安装脚本与构建产物（zip/exe 不入库）
```

## 致谢

基于 [chenjing1294/TinyGUI](https://github.com/chenjing1294/TinyGUI) 修改。

## 许可证

本仓库在原项目基础上做了修改，原项目作者保留其相关权利。当前仓库未附带独立许可证文件，使用与再分发请遵循原项目的相关条款；如需开源分发，建议补充合适的许可证。
