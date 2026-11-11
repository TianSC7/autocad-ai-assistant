# Security Policy

## Supported versions

Only the latest release on `main` is supported.

## Reporting a vulnerability

Open a private security advisory on GitHub, or contact the maintainer through the GitHub profile
listed on the repository. Do not open a public issue for an exploitable problem.

Please include:

- A description of the issue and its impact
- Steps to reproduce
- Affected version/commit

## Threat model and known limitations

This plugin runs inside the AutoCAD process with the user's privileges. It does not sandbox the
model: any action the model proposes is executed by the same process, after passing the built-in
vetter (`ActionVetter`) and, for dangerous operations, an explicit user confirmation.

- The vetter is a denylist plus heuristics, not a formal sandbox. Treat model output as untrusted.
- API keys are stored in plain JSON under the user profile. Protect that folder with OS
  permissions and never commit the file.
- No telemetry is collected. Network requests go only to the endpoint configured in
  `ai_presets.json`.
- Do not load the plugin into untrusted drawings or run it with elevated privileges.