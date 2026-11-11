# Changelog

All notable changes to this project are documented here.
This project follows [Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-11-11

### Added

- Initial public extraction of the `AI` command from the private CadPerception plugin.
- Dockable WPF AI panel: chat, streaming, reasoning display, token/usage bar.
- OpenAI-compatible model presets loaded from `ai_presets.json`.
- Vision support: attach an image or capture the current AutoCAD viewport.
- Structured action execution: `<<CAD_ACTION>>...<<CAD_ACTION_END>>` blocks, safety vetting,
  main-thread queue, result readback.
- `AICFG` command and a settings dialog for chat/embedding API keys.

### Removed from the private build

- WebSocket bridge and external DevTools page.
- Selection/command perception listeners.
- Phone/terminal expert channel and its private paths.
- Hard-coded API keys and personal absolute paths.