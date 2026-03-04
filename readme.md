# UABEA Batch Export Tool

基于 UABEAvalonia 的 Unity AssetBundle 批量导出工具。

---

## 功能简介

本工具提供命令行方式批量提取 Unity 游戏的 AssetBundle 资源，无需打开 GUI 界面。

支持从 `.bundle` 或 Hash 名文件（UnityFS 格式）中提取原始资源文件（`.assets`、`.resS` 等）。

---

## 快速开始

### 使用 bundle.bat（推荐）

项目根目录提供了 `bundle.bat`，可在项目根目录直接运行：

```bash
# 方式1：双击运行（使用预设路径）
bundle.bat

# 方式2：拖拽 Bundle 文件到 bat 文件上
bundle.bat "C:/Game/StreamingAssets/characters.bundle"

# 方式3：命令行指定路径
bundle.bat "C:/Game/StreamingAssets"
```

**配置方法**：
编辑 `bundle.bat` 修改以下路径：
```bat
set "INPUT_PATH=C:\你的游戏\StreamingAssets"  ← 修改为你的 Bundle 路径
```

---

## 命令行用法

### Batch Export Bundle

从 Unity AssetBundle（UnityFS 格式）中提取所有原始文件。

```bash
UABEAvalonia batchexportbundle <输入路径> [输出目录]
```

**参数说明**：
- `<输入路径>` - 单个 Bundle 文件，或包含 Bundle 的文件夹
- `[输出目录]` - 可选，默认在输入路径下创建 `exported` 文件夹

**示例**：
```bash
# 提取单个 Bundle
UABEAvalonia batchexportbundle "C:/Game/StreamingAssets/5a2f8c3d..." "C:/Export"

# 批量提取文件夹内所有 Bundle
UABEAvalonia batchexportbundle "C:/Game/StreamingAssets" "C:/Export/AllBundles"
```

**⚠️ 重要说明**：
导出时**必须保持原始文件名**（包括 `CAB-xxx.assets`、`CAB-xxx.resS` 等）。

原因：`.assets` 文件内部通过**硬编码的文件名**引用 `.resS` 资源文件：
```yaml
Texture2D:
  m_StreamData:
    path: "CAB-abc123.resS"  ← 硬编码引用！
    offset: 123456
    size: 1048576
```

如果重命名文件，资源加载时将无法找到对应的数据。

**选项**：
- `-md` - 内存中解压（不创建临时 `.decomp` 文件）
- `-kd` - 保留临时解压文件

---

## 技术原理

### UnityFS 文件格式

Unity AssetBundle 的文件头标识为 `UnityFS`：

```
┌─────────────────────────────────────────┐
│ Header (未压缩)                          │
│ ├── Magic: "UnityFS" (7 bytes)          │
│ ├── Version: 0x06 / 0x16                │
│ ├── UnityVersion: "2021.3.15f1"         │
│ ├── Size: 文件总大小                     │
│ └── Flags: 压缩方式标志                  │
├─────────────────────────────────────────┤
│ BlocksInfo (可能压缩)                    │
│ └── 数据块信息：偏移、大小、压缩类型      │
├─────────────────────────────────────────┤
│ Data Blocks (压缩存储)                   │
│ ├── Block 1: CAB-xxx.assets 数据         │
│ ├── Block 2: CAB-xxx.resS 数据           │
│ └── ...                                  │
└─────────────────────────────────────────┘
```

### 加载流程

1. **识别** - 读取文件头，检测 `UnityFS` 魔数
2. **解压** - 根据 Flags 使用 LZ4/LZMA 算法解压
3. **解析目录** - 读取 `BlockAndDirInfo.DirectoryInfos` 获取文件列表
4. **提取** - 根据偏移和大小从数据块中截取文件内容

### Bundle 内文件结构

典型 Bundle 包含以下文件：

| 文件名 | 类型 | 说明 |
|--------|------|------|
| `CAB-xxx.assets` | AssetsFile | 序列化资源元数据 |
| `CAB-xxx.resS` | Resource | 大型二进制数据（纹理、音频） |
| `sharedAssets0.assets` | SharedAssets | 跨资源共享数据 |
| `sharedAssets0.resource` | Resource | 共享资源数据 |

---

## 构建项目

```bash
# Release 版本
dotnet build UABEAvalonia/UABEAvalonia.csproj -c Release -p:Platform=x64

# 输出位置
UABEAvalonia/bin/x64/Release/net8.0/UABEAvalonia.exe
```

---

## 依赖

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) (已包含在 Libs/)

---

## 原始项目

基于 [UABEA](https://github.com/nesrak1/UABEA) 修改。
