# RFAQuickPreview

Revit RFA 族文件快速预览工具。提供独立的 WPF 桌面应用，可递归扫描文件夹中的 `.rfa` 文件并展示缩略图预览。

对于需要真实几何体缩略图的文件，应用会在后台自动启动 Revit 2020 导出 PNG 缩略图，完成后关闭 Revit，桌面应用直接显示缓存的预览图。后续扫描直接使用缓存，无需重复启动 Revit。

## 功能特性

- 递归扫描文件夹中的 `.rfa` 族文件
- 文件夹右键菜单集成，一键启动预览
- Revit 2020 后台自动生成真实几何体缩略图
- PNG + JSON 缓存机制，后续扫描秒开
- 支持文件搜索、卡片网格视图、详情面板和日志面板

---

## 便携版使用（推荐）

便携版已包含 .NET 8.0 运行时，无需额外安装，解压即用。

### 1. 下载

从 [GitHub Releases](https://github.com/y1991427360s/RFAQuickPreview/releases) 下载 `RFAQuickPreviewPortable.zip` 并解压到任意目录。

### 2. 运行

双击 `RFAQuickPreview.exe` 启动应用。

### 3. 注册右键菜单

以 **管理员身份** 打开 PowerShell，进入便携版目录，运行：

```powershell
.\RegisterRightClick.ps1
```

注册后，在 Windows 资源管理器中右键点击任意文件夹，菜单中会出现 **"Preview RFA files"** 选项，点击即可启动预览。

### 4. 取消右键菜单

以管理员身份运行：

```powershell
.\UnregisterRightClick.ps1
```

### 5. 配置 Revit 路径

如果需要自动生成真实几何体缩略图，需安装 Revit 2020。编辑便携版目录下的 `RFAQuickPreview.config.json`，修改 Revit 可执行文件路径：

```json
{
  "RevitExePath": "D:\\Autodesk\\REVIT2020\\Revit 2020\\Revit.exe"
}
```

> 如不配置 Revit 路径，应用仍可正常运行，但仅使用系统缩略图，无法生成真实几何体预览。

---

## 从源码构建

### 环境要求

- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022 Build Tools（含 MSBuild）
- Revit 2020（仅自动化预览功能需要）

### 构建桌面应用（便携版）

```powershell
.\scripts\Publish-Desktop.ps1 -Configuration Release
```

输出目录：`dist\RFAQuickPreviewPortable\`

### 构建 Revit 自动化助手

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe" RFAQuickPreview.csproj /t:Restore,Build /p:Configuration=Release
.\scripts\Install-Addin.ps1 -Configuration Release
```

安装后 Revit 2020 启动时会自动加载该插件。

### 注册/取消右键菜单（源码版）

```powershell
.\scripts\Register-FolderContextMenu.ps1
.\scripts\Unregister-FolderContextMenu.ps1
```

---

## 项目结构

| 目录 | 说明 |
|------|------|
| `App/` | Revit 外部命令入口点 |
| `Desktop/` | 独立 WPF 桌面应用（主入口） |
| `UI/` | Revit 内嵌 WPF 窗口、搜索、卡片网格、详情和日志 |
| `Revit/` | Revit API 缩略图导出和参数提取 |
| `Cache/` | PNG 和 JSON 缓存管理 |
| `Services/` | 递归扫描编排服务 |
| `Models/` | UI、缓存和服务共用的数据传输对象 |
| `Properties/` | 程序集信息 |
| `scripts/` | 构建、发布和注册脚本 |
| `dist/` | 构建输出目录 |

---

## 下载

- [源码](https://github.com/y1991427360s/RFAQuickPreview)
- [便携版下载](https://github.com/y1991427360s/RFAQuickPreview/releases/download/v1.0.0/RFAQuickPreviewPortable.zip)
