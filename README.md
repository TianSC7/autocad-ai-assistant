# AutoCAD AI Assistant (AI command)

An in-AutoCAD AI panel: chat with any OpenAI-compatible model, attach a viewport screenshot,
and let the model execute vetted drawing/query actions on the current drawing.

This repository is a clean extraction of the `AI` command from a larger private CAD plugin.
It contains **only** the AI panel and its action executor. The WebSocket bridge, external
DevTools page, selection perception and Python helper scripts are intentionally not included.

中文说明见下方 [中文](#中文说明)。

---

## English

### What it does

- `AI` command opens a dockable WPF panel inside AutoCAD.
- Chat with any OpenAI-compatible chat completions endpoint (streaming, reasoning, token usage).
- Attach an image or capture the current viewport and send it to a vision-capable model.
- The model can return a structured action block; the plugin vets it, executes it on the CAD main
  thread, and reads the result back into the conversation.
- `AICFG` prints the active model routing and configuration file location.

### Requirements

| Item | Version |
| --- | --- |
| AutoCAD | 2027 (managed API) |
| .NET SDK | 10.0 |
| OS | Windows x64 |

AutoCAD's managed assemblies (`acdbmgd.dll`, `acmgd.dll`, `accoremgd.dll`) are host-provided and
are never copied into the build output.

### Build

```powershell
dotnet build src/CadPerception/CadPerceptionPlugin.csproj -c Release
```

If AutoCAD is installed elsewhere:

```powershell
dotnet build src/CadPerception/CadPerceptionPlugin.csproj -c Release -p:AutoCadDir="D:\Autodesk\AutoCAD 2027"
```

### Install

1. Start AutoCAD 2027.
2. Run `NETLOAD` and select `CadAiAssistant.dll` from `bin/Release/net10.0-windows/`.
3. Type `AI` to open the panel.

### Configure a model

The plugin reads `ai_presets.json` from `%USERPROFILE%\Documents\cadsys\NET\` by default.
Override the root with the `CAD_SYS_ROOT` environment variable.

```powershell
mkdir "$env:USERPROFILE\Documents\cadsys\NET"
copy ai_presets.example.json "$env:USERPROFILE\Documents\cadsys\NET\ai_presets.json"
```

Then edit `ai_presets.json`:

- `active_language_model`: id of the chat model to use by default.
- `presets[]`: OpenAI-compatible endpoints. `isEmbeddingModel: true` marks the embedding model.
- `lsp_directory`: optional folder offered to the AI as a LISP command directory.

Alternatively use the panel's settings dialog (gear icon) to set the chat/embedding API keys.

### Commands

| Command | Action |
| --- | --- |
| `AI` | Open or hide the AI panel |
| `AICFG` | Print active model routing and config path to the command line |

### Action protocol

The assistant may reply with a fenced action block:

```text
<<CAD_ACTION>>{"action":"draw","items":[...]}<<CAD_ACTION_END>>
```

The panel extracts the block, passes it through a safety vetter (blocked commands, dangerous
operations, interactive commands), asks for confirmation when required, then executes it through a
main-thread queue. The execution result is appended to the conversation, so the model cannot
silently claim success.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/USAGE.md](docs/USAGE.md).

### Security notes

- API keys are stored in plain JSON under your user profile. They are never committed here.
- The action vetter blocks destructive commands by default (`QUIT`, `SAVEAS`, `ERASE`, ...).
- No telemetry. Network traffic only goes to the endpoint you configure.

### License

MIT. See [LICENSE](LICENSE).

---

## 中文说明

### 这是什么

把 `AI` 命令从私有 CAD 插件里剥离出来的干净版本：AutoCAD 内可停靠的 AI 面板，支持任意
OpenAI 兼容接口，能把当前视口截图发给多模态模型，并执行经过审查的 CAD 动作块。

**不包含**：WebSocket 桥、外部 DevTools 网页、选区感知引擎、Python 中转脚本。

### 环境

- AutoCAD 2027
- .NET SDK 10.0
- Windows x64

### 构建

```powershell
dotnet build src/CadPerception/CadPerceptionPlugin.csproj -c Release
```

### 安装

1. 启动 AutoCAD 2027。
2. 命令行执行 `NETLOAD`，选择 `CadAiAssistant.dll`。
3. 输入 `AI` 打开面板。

### 配置

默认读取 `%USERPROFILE%\Documents\cadsys\NET\ai_presets.json`，可用环境变量 `CAD_SYS_ROOT`
改变根目录。把 `ai_presets.example.json` 复制成 `ai_presets.json` 后填入自己的接口地址与 Key；
也可以点面板齿轮按钮，在设置窗口里填对话/向量模型的 Key。

### 命令

| 命令 | 作用 |
| --- | --- |
| `AI` | 打开/隐藏 AI 面板 |
| `AICFG` | 在命令行输出当前模型路由与配置文件路径 |

### 动作执行与安全

模型回复里的 `<<CAD_ACTION>>...<<CAD_ACTION_END>>` 块会被提取、审查（黑名单/危险命令/交互命令），
必要时弹确认，然后投递到 CAD 主线程队列执行，并把真实结果回读给模型。模型无法只靠文字声称"已完成"。

### 许可

MIT，见 [LICENSE](LICENSE)。