# Agent Monitor

A Windows system-tray light for local LLM coding agents. One coloured icon tells
you, at a glance, whether any session is **ready for you**:

- 🟢 **Green** — a session recently finished / is waiting on you, and you haven't
  looked yet. The "go look" ping.
- 🟠 **Amber** — something is working; nothing is waiting on you.
- ⚪ **Grey** — nothing wants you: idle, already-seen, or not running.

Green is deliberately rare — it appears only when something genuinely needs your
attention and fades back to grey once you've looked or after a few minutes.

It currently monitors **Claude Code** — both the desktop app and the terminal CLI —
and is built so other tools (e.g. Codex) can be added behind one interface.

---

# Getting started (first-time users)

**Prerequisite:** the **.NET 10 Desktop Runtime** (x64). If you don't have it,
get it from <https://dotnet.microsoft.com/download/dotnet/10.0> (the "Desktop
Runtime" download). Running `AgentMonitor.exe` without it shows a prompt with a
link.

### 1. Download and run

1. Go to the [**Releases**](../../releases) page and download the latest
   `AgentMonitor-<version>-win-x64.zip`.
2. Unzip it anywhere (e.g. `C:\Tools\AgentMonitor`). Keep the contents together —
   the two executables you'll use are:
   - `AgentMonitor.exe` — the tray app.
   - `AgentMonitor.HookSink.exe` — a small helper used by precise mode (below).
3. Double-click **`AgentMonitor.exe`**. A coloured dot appears in your system tray
   (click the **^** arrow by the clock if you don't see it).

There's no window — the app lives entirely in the tray. **Right-click the icon**
for the menu.

### 2. Read the light

| Colour | Meaning |
|:---:|---|
| 🟢 Green | A session recently became ready for you and you haven't looked yet — **go look**. |
| 🟠 Amber | Something is working; nothing is waiting on you yet. |
| ⚪ Grey | Nothing wants you: idle, already-seen, or not running. |

Green fades back to grey once you've **looked at the session** (focused its Claude
window) or after ~5 minutes — so it only lights up for things you haven't dealt
with. Right-click → the menu lists every session and marks the ones that are
**★ ready for you**, with their folders, so you can see *which* one to open.

### 3. (Recommended) Turn on precise mode

Out of the box the app infers status by watching Claude's files, which is good but
occasionally shows green during a long build. **Precise mode** makes Claude Code
report its status exactly (including "waiting for your permission").

- Right-click the icon → **Precise mode (hooks) → Install hooks**.

This adds a few hooks to your Claude Code settings (with an automatic backup) and
takes effect on each session's next turn. You can **Remove hooks** any time from the
same menu. It's completely optional — the app works without it.

### 4. Optional extras

- **Notify when awaiting** *(on by default)* — pops a balloon the moment a session
  finishes and is waiting for you. Toggle it from the right-click menu.
- **Start with Windows** — right-click → tick it to launch the tray automatically
  at sign-in.
- **About…** — shows the version you're running.

### Uninstalling

Remove hooks first (right-click → Precise mode → **Remove hooks**), untick **Start
with Windows**, then **Exit** and delete the folder. Nothing else is left behind
except a small status folder at `%USERPROFILE%\.claude\agent-monitor` you can delete.

---

# Technical notes

Everything below is for people building, extending, or curious about how it works.
Skip it if you just want to run the app.

## Build from source

Requires the **.NET 10 SDK**.

```sh
dotnet build -c Release
dotnet run --project src/AgentMonitor.Tray -c Release
dotnet test                      # run the unit tests
```

## Projects

| Project | Target | Responsibility |
|---|---|---|
| `AgentMonitor.Core` | `net10.0` | Provider-agnostic models, the `ISessionProvider` interface, the colour policies and the `StatusAggregator`. No tool- or OS-specific code. |
| `AgentMonitor.Providers.ClaudeCode` | `net10.0` | Reads `~/.claude` (session registry + transcripts + hook markers) and maps Claude Code sessions onto the shared model. **All** Claude-internal format knowledge lives in its `Internal/` folder. |
| `AgentMonitor.Providers.Codex` | `net10.0` | Stub showing the extension point — implement and drop in. |
| `AgentMonitor.HookSink` | `net10.0` | Tiny exe invoked by Claude Code hooks; writes per-session status markers. |
| `AgentMonitor.Tray` | `net10.0-windows` | WinForms tray app: icon rendering, polling, menu, notifications, hook installer. |
| `tests/AgentMonitor.Tests` | `net10.0` | xUnit tests for the status logic and the hook installer. |

`tools/smoke` is a console (not in the solution) that prints what the Claude
provider sees live — handy for sanity checks against real sessions.

## How status is detected (Claude Code)

Claude Code leaves enough state on disk to monitor it read-only, no API needed:

1. **`~/.claude/sessions/<PID>.json`** — a live registry, one file per interactive
   session, written by every session regardless of entrypoint (`"cli"` for the
   terminal, `"claude-desktop"` for the desktop app). Gives the session list, cwd,
   PID and `kind`. We keep `kind == "interactive"` and verify the PID is a live
   `claude` process (guards against stale files and PID reuse).
2. **`~/.claude/projects/<slug>/<sessionId>.jsonl`** — the transcript. Status is
   derived from write-recency plus the most recent **assistant** record's
   `stop_reason` (`end_turn` = finished; otherwise recent growth = working).

### The heuristic's limit

An idle interactive session often rests at a `tool_use` record (paused at a
permission prompt), not a clean `end_turn`. The transcript alone **cannot** always
distinguish "blocked, waiting on you" from "a long tool is still running". The
heuristic treats a session quiet for ~30s as *awaiting you* — which fits the goal
but can show green during a long build. **Precise mode removes this ambiguity.**

### Precise mode (hooks)

Installing hooks (tray menu, or by hand in `settings.json`) wires four events to
`AgentMonitor.HookSink.exe`, which writes a marker per session under
`~/.claude/agent-monitor/status/`:

| Hook event | Meaning |
|---|---|
| `UserPromptSubmit` | a turn started → **working** |
| `Stop` | the turn finished → **awaiting you** |
| `Notification` | needs attention (permission / idle) → **awaiting you** |
| `SessionEnd` | session ended → marker cleared |

These fire ~once per turn, so overhead is negligible (we deliberately don't hook
per-tool events). `HookStatusInterpreter` layers the event over transcript
recency: if the transcript grows *after* the last event, the session has resumed
(e.g. a prompt was approved), so it reports *working* rather than a stale *awaiting
you*. With no marker present it falls back to the heuristic, so the app works
either way.

Background/"fleet" sessions additionally carry an explicit `tempo`/`needs` status,
which the interpreter prefers when present. All three sources sit behind
`ISessionStatusInterpreter` without touching the tray.

> These are **undocumented internal** files, stable as of Claude Code 2.1.x but
> liable to change — which is why every byte of that format knowledge is confined
> to `AgentMonitor.Providers.ClaudeCode/Internal/`.

## Deciding the colour (attention model)

Providers report each session's raw status (working / awaiting / …); `AttentionTracker`
turns the whole set into one colour by *how much it wants you*:

- **Green** — at least one session is `AwaitingInput`, became so recently (within a
  ~5-minute window), **and** you haven't acknowledged it.
- **Amber** — something is working and nothing qualifies for green.
- **Grey** — everything else (idle, already-seen, stale, or not running).

A green session is acknowledged — fading to grey — when any of:

1. **You act on it** — it resumes or dies (no longer awaiting).
2. **You look at it** — its window shares the foreground window's process lineage,
   **and** that window hosts only this one session. The OS exposes no per-tab
   signal, so focus only acknowledges when it *uniquely* identifies a session:
   a single-session terminal or single-tab Claude Desktop clears on focus, but a
   multi-tab Claude Desktop (or multi-tab terminal) does not — there a background
   session that finishes still goes green rather than being silently cleared
   because the app happened to be in front. See `ForegroundWindow`.
3. **Timeout** — the freshness window elapses.

This state is UI-side (it needs focus + time), so it lives in the tray, not in a
provider. The freshness window is a constant in `AttentionTracker` today.

## Adding a provider (e.g. Codex)

1. Implement `ISessionProvider.GetSessions()`, discovering that tool's own session
   state and mapping it onto `SessionStatus` (and set `LastActivity` to when it
   entered that state — the attention model uses it for freshness).
2. Add it to the provider list in `TrayApplicationContext`.

The aggregator, attention model and tray are unchanged. See
`AgentMonitor.Providers.Codex/CodexProvider.cs` for the skeleton.

## Versioning

`Directory.Build.props` sets a base `VersionPrefix` and embeds the short git commit
into `InformationalVersion` (e.g. `0.1.0+ab12cd3`), shown in the About box. Release
builds override the version from the git tag (see below).

## CI / releases

- **CI** (`.github/workflows/ci.yml`) — builds and runs the tests on every push to
  `main` and every PR (windows-latest, .NET 10).
- **Release** (`.github/workflows/release.yml`) — pushing a `v*` tag (e.g.
  `v0.2.0`) runs the tests, publishes framework-dependent `win-x64` builds of the
  tray and sink (requires the .NET 10 Desktop Runtime on the user's machine), zips
  them, and creates a GitHub Release with the version taken from the tag.

```sh
git tag v0.2.0
git push origin v0.2.0   # -> builds and publishes the release
```
