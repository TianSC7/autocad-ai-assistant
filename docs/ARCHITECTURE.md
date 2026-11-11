# Architecture

## Scope

This repository contains the `AI` command only. The private upstream plugin also ships a
WebSocket bridge, an external DevTools page and selection perception; none of that is here.

## Components

| File | Responsibility |
| --- | --- |
| `src/CadPerception/PluginMain.cs` | `IExtensionApplication`, `AI` / `AICFG` commands, main-thread action queue, structured action executor |
| `src/CadPerception/AiAssistantView.cs` | WPF panel: chat UI, model calls, action extraction and safety vetting, session persistence |
| `src/CadPerception/AiSettingsWindow.cs` | Settings dialog for chat/embedding API keys and usage bar switches |
| `src/CadPerception/AiModelCatalog.cs` | Reads `ai_presets.json`, builds the model list, tracks the active model |
| `src/CadPerception/CadViewportCapture.cs` | Captures the AutoCAD main window for vision requests |
| `src/CadPerception/CadPaths.cs` | User-scoped runtime paths (`CAD_SYS_ROOT` override) |

## Data flow

```text
User prompt
  -> AiAssistantView.SubmitPrompt
     -> optional viewport capture (CadViewportCapture)
     -> HTTP chat completions request (OpenAI-compatible)
        -> text response
        -> optional <<CAD_ACTION>>{json}<<CAD_ACTION_END>> block
           -> ActionVetter (blocked / dangerous / interactive)
           -> confirmation dialog when required
           -> CadPerceptionApp.ExecuteAiActionSync
              -> main-thread queue (120 ms timer)
              -> RunStructuredAction / RunExtendedAction
              -> readback result
  -> response text + execution result rendered in the panel
```

## Action protocol

The model must wrap machine-readable work in a sentinel block:

```text
<<CAD_ACTION>>{"action":"draw","items":[...]}<<CAD_ACTION_END>>
```

Supported actions are parsed by `AiAssistantView.ExtractAndRunAiAction` and dispatched by
`PluginMain.HandleClientMessage`. Every execution returns a result object; the panel appends it to
the conversation so a text-only "done" claim cannot pass as a real edit.

## Configuration

`ai_presets.json` lives in `%USERPROFILE%\Documents\cadsys\NET\` unless `CAD_SYS_ROOT` is set.

```text
active_language_model   id of the default chat model
active_vision_model     id of the default vision model
active_embedding_model  id of the embedding model
presets[]               OpenAI-compatible endpoints
```

`AiModelCatalog` caches the parsed list and reloads when the file timestamp changes.

## Threading

AutoCAD document locks must be taken on the main thread. Downstream actions are therefore queued in
`_actionQueue` and drained by a WinForms timer; background threads never lock the document.

## Non-goals

- No external IPC server.
- No telemetry.
- No bundled Autodesk assemblies (host-provided).