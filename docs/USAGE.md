# Usage

## Commands

| Command | Description |
| --- | --- |
| `AI` | Open or hide the dockable AI panel |
| `AICFG` | Print model routing and the configuration file path |

## First run

1. Build and `NETLOAD` the DLL.
2. Type `AI`.
3. Either:
   - copy `ai_presets.example.json` to `%USERPROFILE%\Documents\cadsys\NET\ai_presets.json` and
     edit it, then restart AutoCAD; or
   - open the gear icon in the panel and enter the chat/embedding API keys.

`AICFG` shows which file is actually being read.

## Model presets

Any OpenAI-compatible endpoint works:

```json
{
  "id": "local-chat",
  "name": "Local Chat",
  "baseUrl": "http://127.0.0.1:1234/v1",
  "model": "qwen2.5-coder",
  "apiKey": "not-needed",
  "timeoutSec": 120,
  "isEmbeddingModel": false,
  "isTextModel": true,
  "isImageModel": false,
  "supportVision": false
}
```

Set `isEmbeddingModel: true` for the embedding endpoint (used by the optional experience-library
retrieval). The panel lists only non-embedding models in the model picker.

## Image input

Two ways:

- Paperclip button: choose an image file.
- Viewport button: capture the current AutoCAD main window.

The request is sent to a model with `supportVision: true` (or `isImageModel: true`). If the active
model has no vision support, the panel still sends the text and logs the fallback.

## Action execution

Ask for a change in natural language. The model answers with an action block:

```text
<<CAD_ACTION>>{"action":"draw","items":[{"type":"line","start":[0,0],"end":[100,0]}]}<<CAD_ACTION_END>>
```

The panel vets the block, asks for confirmation when the operation is classified as dangerous, then
executes it on the main thread. The result (success, message, affected handles) is appended to the
conversation.

If the reply claims work was done but contains no action block, the panel flags it as an empty claim.

## Sessions

Conversations are stored in `ai_sessions.json` next to `ai_presets.json`. Use the history button to
switch sessions. Deleting that file resets history.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| `AI` does nothing | DLL loaded with `NETLOAD`; check `CadPerception_debug.log` |
| "no usable chat model" | `ai_presets.json` missing or every preset is an embedding model |
| Vision request ignored | active model has no vision support |
| Action fails | inspect the returned message and the action block JSON |