# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A CLI test harness that students in DigiPen's CS 260 (Computer Networks I) use to exercise their **Assignment 3 HTTP proxy server**. It makes HTTP requests *through* the student's proxy (which listens on `127.0.0.1:8888` by default) and reports timing, byte counts, and pass/fail. It is distributed as a self-contained single-file executable so students don't need to install the .NET runtime.

The original `.BAT` / PowerShell harness was abandoned in favor of this .NET 8 app; the `Run*.bat` files are thin wrappers around the EXE kept for student convenience.

## Build, run, test

```bash
dotnet build                    # build (solution at repo root)
dotnet run --project FakeHttpClient -- --url http://cs260.meancat.com/delay   # run against a local proxy on :8888
```

There is **no unit test project** — "tests" in this codebase means the HTTP request scenarios the tool runs, not xUnit/NUnit. Verify changes by building and running against a live proxy.

Cross-platform release builds (zips per RID):

```bash
pwsh ./build-all.ps1            # publishes win-x64, win-arm64, linux-x64, osx-x64, osx-arm64 into dist/
pwsh ./deploy-release.ps1       # creates a GitHub release from dist/*.zip
```

Versioning is driven by **Nerdbank.GitVersioning** (`version.json`); `nbgv get-version` yields the version. Don't hand-edit assembly versions — they flow from git height. CI release builds run via `.github/workflows/*.yaml` (manual `workflow_dispatch`); Windows binaries are code-signed through SignPath.

`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is set — the build fails on any warning. `Nullable` is enabled.

## CLI surface

The CLI is defined entirely by the parameters of `Program.Main` via **System.CommandLine.DragonFruit** — there is no explicit command/option wiring. To add a flag, add a parameter to `Main` and document it with an XML `<param>` comment (DragonFruit surfaces those in `--help`). DragonFruit is deprecated/prerelease here; `Main` deliberately `.Wait()`s instead of awaiting because of a known DragonFruit async bug (see comment in `Program.cs`).

Key flags: `--url` (single request), `--test-suite` (named embedded JSON resource), `--test-file-override` (path to local JSON), `--interactive` (console vs. file output), `--raw` (raw TCP vs. HttpClient — **defaults to true**), `--proxy-port`, `--proxy-ip`, `--test-name`.

## Architecture

Four operating modes, selected by `RunConfig` from the flags above: single-URL vs. test-suite × interactive (console) vs. non-interactive (one output file per test). `RunConfig.Validate()` enforces input/port invariants.

Request flow:
- `ProxyRequestFactory.CreateProxyRequest` picks the implementation from the `raw` flag.
- `ProxyRequest` (abstract) is the template: `PrepareTest` → `ExecuteTest` → `EvaluateResult`, wrapping timing and the Begin/End log lines. Subclasses implement only `PrepareTest`/`ExecuteTest`.
  - `TcpProxyRequest` (`raw=true`): opens a raw `TcpClient` to the proxy, writes a hand-built HTTP/1.1 request, and streams back the **raw** response (headers + body).
  - `HttpProxyRequest` (`raw=false`): uses `HttpClient` with a `WebProxy`, returning only the parsed body.
- Pass/fail is heuristic (`ProxyRequest.EvaluateResult`): a request is `TooSlow` past ~7s or `NotEnoughData` under 100 bytes received. These thresholds, plus the global 20s timeout (`Program.TimeoutSeconds`), are the core "is the student's proxy working" signals — the delay-test fixtures stream over ~5s by design.
- `Launcher` runs a suite **concurrently** (`Task.WhenAll` with a timeout race). This is the most important mode: it checks whether the student's proxy handles parallel connections independently, vs. serializing them (request N taking N×5s indicates a bug).
- `WriterFactory` chooses `Console.Out` (interactive) or a sanitized `<name>.txt` file writer (non-interactive) next to the executable.

Note: `Launcher` **ignores `--interactive`** — suites always run non-interactively on every OS, because multi-window interactive output breaks on macOS/Linux (see remarks in `Launcher.cs` / `Program.cs`).

Test fixtures (`single.json`, `multi.json`) are **loose JSON files shipped next to the executable** (`CopyToOutputDirectory` in the csproj, so they land in `dist/<rid>/`), no longer embedded resources. `--test-suite NAME` resolves `NAME` to a file in the app directory (`AppContext.BaseDirectory`) — both `multi` and `multi.json` work; `--test-file-override PATH` reads an arbitrary path instead. Adding a new suite is just dropping another `.json` next to the exe. `multi.json` intentionally mixes DNS-name and raw-IP URLs and duplicate delay requests to test concurrency and name resolution.

The raw-IP delay test uses a `{meancat-ip}` placeholder rather than a literal address. `TestDefinition.SubstitutePlaceholdersAsync` resolves `cs260.meancat.com` via DNS at load time and substitutes the current A record — only doing the lookup if some URL actually contains the token. This is why the box's yearly IP change no longer requires hand-editing the fixture or cutting a release.