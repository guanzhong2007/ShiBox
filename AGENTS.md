# ShiBox development

- Keep changes scoped to the desktop collection app.
- Never inspect, upload or modify real desktop items, `%USERPROFILE%\拾匣收纳`, or `%LOCALAPPDATA%\ShiBox` during checks.
- Use `--storage-check` with a scratch directory for storage changes.
- Preserve original paths, recovery receipts, collision protection and the flat storage layout.
- Do not add automatic deletion, automatic startup registration, telemetry or network services.
- Keep directory opening delegated to Windows, not a custom file browser.
- Build on Windows with .NET 9. UI checks and an actual reboot are separate from compilation and storage checks; report them accurately.
