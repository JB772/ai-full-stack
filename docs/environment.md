# Environment & shell traps (Windows / PowerShell)

Non-obvious local-dev traps. **None are code problems.** The commands themselves live in `CLAUDE.md`
under **Commands** — this file is the "why it broke" reference.

## Node — normally already on PATH

`C:\Program Files\nodejs` is on the persisted user PATH, so `node` / `npm` / `npx` resolve with no setup
(verified 2026-07-16: `node --version` → v24.18.0 in both plain Bash and plain PowerShell).

Only if a shell reports `node: command not found` — which happens when the session's environment predates
the install and is therefore stale — either restart the session or prepend for that shell:
- PowerShell: `$env:Path = "C:\Program Files\nodejs;$env:Path"`
- Bash tool: `export PATH="/c/Program Files/nodejs:$PATH"`

The same stale-environment effect hides other recently-installed CLIs (e.g. `codex` at
`C:\Users\Admin\AppData\Local\Programs\OpenAI\Codex\bin`, also on the persisted user PATH). Restarting the
session is the general fix; prepending is the per-shell workaround.

## PowerShell blocks `npm` / `ng` / `npx`

Symptom: `PSSecurityException … 指令碼執行已停用 / execution of scripts is disabled`. The global shims are
`.ps1` files and this machine's execution policy forbids unsigned scripts.
- **Fix once:** `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` (answer `Y`). `RemoteSigned` still
  blocks unsigned scripts downloaded from the internet — the standard, safe dev setting.
- **Or sidestep per command** by calling the `.cmd` variant the policy doesn't gate: `npm.cmd start`,
  `npx.cmd ng serve`.
- The **Bash tool is unaffected** — `export PATH=…` then `npx ng …`.

## `dotnet test` fails with `MSB3021 … being used by another process`

A running `dotnet run` holds a lock on `src\CMS.API\bin\Debug\net9.0\CMS.API.dll`, and the test project
rebuilds the API into that *same* folder — so the build dies and the failure looks nothing like a
compile error.
- **Stop the API first:** `Get-Process CMS.API | Stop-Process -Force`.
- To type-check the API *without* stopping it, build to a throwaway folder:
  `dotnet build src\CMS.API --output <tmp>`.

## Headless Karma

`$env:CHROME_BIN = "C:\Program Files\Google\Chrome\Application\chrome.exe"` then
`npx ng test --watch=false --browsers=ChromeHeadless`.

## UTF-8 through the shell

Inline Chinese JSON to `curl` gets mangled — use `--data-binary @file` or Swagger UI.
