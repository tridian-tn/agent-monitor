# Agent Monitor

A Windows system-tray "traffic light" for local LLM coding agents. One coloured
icon tells you, at a glance, whether any session wants your attention.

- 🔴 **Red** — no live sessions (the tool isn't running).
- 🟠 **Amber** — at least one session is working and none are awaiting you.
- 🟢 **Green** — a session has finished / is waiting on you (or nothing is working).

Left-click the icon's menu to see every session, its state, and which tool /
entrypoint it came from. The green/amber meaning is switchable via **Policy**.

Built on **.NET 10** / WinForms. The first provider is **Claude Code** (covering
both the desktop app and the terminal CLI); the architecture is provider-agnostic
so other tools (e.g. Codex) slot in behind one interface.

## Projects

| Project | Target | Responsibility |
|---|---|---|
| `AgentMonitor.Core` | `net10.0` | Provider-agnostic models, the `ISessionProvider` interface, the colour policies and the `StatusAggregator`. No tool- or OS-specific code. |
| `AgentMonitor.Providers.ClaudeCode` | `net10.0` | Reads `~/.claude` (session registry + transcripts) and maps Claude Code sessions onto the shared model. **All** Claude-internal format knowledge lives in its `Internal/` folder. |
| `AgentMonitor.Providers.Codex` | `net10.0` | Stub showing the extension point — implement and drop in. |
| `AgentMonitor.Tray` | `net10.0-windows` | The WinForms tray app: icon rendering, polling timer, context menu. |

## Build & run

```sh
dotnet build -c Release
dotnet run --project src/AgentMonitor.Tray -c Release
```

The app has no installer yet; it runs as a tray-only process (no main window).
Exit from the icon's context menu.

## How status is detected (Claude Code)

Claude Code leaves enough state on disk to monitor it read-only, no API needed:

1. **`~/.claude/sessions/<PID>.json`** — a live registry, one file per interactive
   session, written by every session regardless of entrypoint (`"cli"` for the
   terminal, `"claude-desktop"` for the desktop app). Gives the session list,
   cwd, PID and `kind`. We filter to `kind == "interactive"` and verify the PID is
   a live `claude` process (guards against stale files and PID reuse).
2. **`~/.claude/projects/<slug>/<sessionId>.jsonl`** — the transcript. Status is
   derived from write-recency plus the most recent **assistant** record's
   `stop_reason` (`end_turn` = finished; otherwise recent growth = working).

### The honest caveat

An idle interactive session often rests at a `tool_use` record (paused at a
permission prompt), not a clean `end_turn`. The transcript alone **cannot** always
distinguish "blocked, waiting on you" from "a long tool is still running". The
heuristic (`TranscriptStatusReader`) treats a session quiet for ≥ 30s as
*awaiting you*, which fits the "tell me when it wants me" goal but will
occasionally show green during a long build/test run.

**Precise mode (below) removes this ambiguity.**

## Precise mode (hooks)

Enable it from the tray menu → **Precise mode (hooks) → Install hooks**. This adds
four hooks to your user `settings.json` (non-destructively, with a
`settings.json.bak` backup):

| Hook event | Meaning |
|---|---|
| `UserPromptSubmit` | a turn started → **working** |
| `Stop` | the turn finished → **awaiting you** |
| `Notification` | Claude needs attention (permission / idle) → **awaiting you** |
| `SessionEnd` | session ended → marker cleared |

Each hook runs `AgentMonitor.HookSink.exe`, a tiny fast executable that reads the
hook payload and writes a per-session marker under
`~/.claude/agent-monitor/status/`. These fire about once per turn, so the overhead
is negligible — we deliberately do **not** hook per-tool events.

`HookStatusInterpreter` then layers the hook event over transcript recency: if the
transcript grows *after* the last hook event, the session has resumed work (e.g. a
permission prompt was approved), so it reports *working* rather than a stale
*awaiting you*. When no hook marker exists (hooks off, or none fired yet), it falls
back to the transcript heuristic — so the app works either way.

A third source is read automatically when present:

- **Daemon `tempo`/`needs`** — background/"fleet" sessions already carry an
  explicit `tempo` (`active`/`idle`/`blocked`) and `needs` string.

All three sources sit behind `ISessionStatusInterpreter` without touching the tray.

> These are **undocumented internal** files. They're stable today (Claude Code
> 2.1.x) but could change in an update — which is why every byte of that format
> knowledge is confined to `AgentMonitor.Providers.ClaudeCode/Internal/`.

## Adding a provider (e.g. Codex)

1. Implement `ISessionProvider.GetSessions()`, discovering that tool's own session
   state and mapping it onto `SessionStatus`.
2. Add it to the provider list in `TrayApplicationContext`.

That's it — the policies, aggregator and tray are unchanged. See
`AgentMonitor.Providers.Codex/CodexProvider.cs` for the skeleton.

## Dev diagnostic

`tools/smoke` is a small console (not part of the solution) that prints what the
Claude provider currently sees and the colour each policy yields — handy for
sanity-checking the heuristic against live sessions:

```sh
dotnet run --project tools/smoke -c Release
```
