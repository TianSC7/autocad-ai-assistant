# Contributing

Thanks for taking a look. This project is an AutoCAD managed-API plugin, so the build
requires a local AutoCAD 2027 installation.

## Before you open a PR

1. Build locally:
   ```powershell
   dotnet build src/CadPerception/CadPerceptionPlugin.csproj -c Release
   ```
2. Keep the diff focused. One topic per PR.
3. Do not commit API keys, absolute personal paths, `bin/`, `obj/` or log files.
4. If you change the action protocol, update `docs/ARCHITECTURE.md` and `docs/USAGE.md`.

## Reporting bugs

Use the issue template. Include:

- AutoCAD version and .NET SDK version
- The model/endpoint type (do not paste your API key)
- The exact `<<CAD_ACTION>>` block, if the problem is about action execution
- Relevant lines from `CadPerception_debug.log`

## Code style

- C# 12, nullable enabled, 4-space indent.
- Match the surrounding naming and comment density.
- Comments that explain "why" are welcome; do not restate the code.

## Security

Never paste real API keys into issues, pull requests or logs. See [SECURITY.md](SECURITY.md).