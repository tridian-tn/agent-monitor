# Agent Monitor

A Windows tray icon that tells you when a local coding agent has finished and is
waiting for you, so you can get on with something else in the meantime.

It works with Claude Code and Codex, including the CLI and desktop app of each.

| Icon | Meaning |
|---|---|
| 🟢 Green | A session has finished or needs your input, and you haven't looked at it yet. |
| 🟠 Amber | At least one session is working, and none is waiting for you. |
| ⚪ Grey | Nothing needs you. Sessions are idle or already seen, or nothing is running. |

## Install

You need 64-bit Windows and the
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). If
the runtime is missing, `AgentMonitor.exe` prompts you with a download link.

1. Download the latest `AgentMonitor-<version>-win-x64.zip` from
   [Releases](../../releases).
2. Unzip it somewhere permanent, such as `C:\Tools\AgentMonitor`, and keep the files
   together.
3. Run `AgentMonitor.exe`. The icon appears in the system tray. If you can't see it,
   click the **^** arrow next to the clock.

The app has no main window. Right-click the icon to open the menu.

## Usage

The menu lists recently active sessions and marks the ones waiting for you with a ★.
Sessions that aren't working and haven't updated in 30 minutes are left off the list.

A session stops counting as waiting for you, and stops turning the icon green, when
one of these happens:

- You focus the session's window. This only works if the window has a single session,
  because Windows can't tell which tab you're looking at. A terminal or Claude desktop
  window with several sessions stays green, and so do Codex sessions.
- The session starts working again or exits.
- Five minutes pass.

### Precise mode (Claude Code only)

By default, the app works out what Claude Code is doing from the files it writes. A
session that goes quiet for 30 seconds is assumed to be waiting for you, so a long
build can turn the icon green by mistake.

Precise mode fixes this by having Claude Code report its status directly, including
when it's waiting for permission. It's optional. To turn it on, right-click the icon
and choose **Precise mode (hooks) > Install hooks**.

This adds four hooks to your Claude Code `settings.json` and saves the previous
version as `settings.json.bak`. Running sessions pick up the change on their next
turn. **Remove hooks**, in the same menu, takes them out again.

Codex doesn't need this, because its logs already record when turns start and finish.

### Menu options

| Option | Default | What it does |
|---|---|---|
| **Notify when ready** | On | Shows a notification when a session starts waiting for you. This resets to on each time the app starts. |
| **Flash green circle when ready** | On | Shows a large green circle in the top-right corner of the active monitor for about five seconds when the icon turns green. |
| **Start with Windows** | Off | Starts the app when you sign in. |

### Moving the app

Hooks and **Start with Windows** both store the app's full path. Before moving the
folder, remove the hooks and untick **Start with Windows**, then set them up again
from the new location.

### Uninstall

1. Choose **Precise mode (hooks) > Remove hooks**. Otherwise, Claude Code will keep
   trying to run a program that no longer exists.
2. Untick **Start with Windows**.
3. Choose **Exit** and delete the app's folder.

These files may be left behind, and you can delete them:

- `%USERPROFILE%\.claude\agent-monitor`: session status files from precise mode.
- `%USERPROFILE%\.claude\settings.json.bak`: the settings backup.
- `%AppData%\AgentMonitor`: the app's saved settings.

---

# Technical notes

The rest of this document is for anyone building or extending the app.

## Building from source

You need Windows and the .NET 10 SDK.

```sh
dotnet build -c Release
dotnet run --project src/AgentMonitor.Tray -c Release
dotnet test
```

## Projects

| Project | Target | Purpose |
|---|---|---|
| `AgentMonitor.Core` | `net10.0` | Shared session model, `ISessionProvider` and `StatusAggregator`. Contains nothing specific to a tool or OS. |
| `AgentMonitor.Providers.ClaudeCode` | `net10.0` | Reads Claude Code's session registry, transcripts and hook markers. |
| `AgentMonitor.Providers.Codex` | `net10.0` | Reads the Codex desktop app's thread database and rollout logs. |
| `AgentMonitor.HookSink` | `net10.0` | Small executable run by Claude Code hooks. Writes a status file for each session. |
| `AgentMonitor.Tray` | `net10.0-windows` | WinForms app: icon, polling, menu, notifications, attention tracking and the hook installer. |
| `tests/AgentMonitor.Tests` | `net10.0` | xUnit tests for status logic, both providers, the hook installer and settings. |

`tools/smoke` is a console app outside the solution. It prints what both providers can
see on your machine and checks the hook interpreter against a running `claude` process.

Each provider keeps its knowledge of the tool's file formats in its `Internal/` folder.
These formats are undocumented and can change, so this limits what a change can break.

## Claude Code status

Claude Code's files are read from `~/.claude`, or from `CLAUDE_CONFIG_DIR` if it's set.
The formats described here are from Claude Code 2.1.x.

- `sessions/<PID>.json` is written for each running session, from both the CLI and the
  desktop app. It provides the session list. The app keeps interactive sessions whose
  PID still belongs to a `claude` process, which filters out stale files and reused PIDs.
- `projects/<slug>/<sessionId>.jsonl` is the session transcript, used to work out status.

### Without hooks

Status comes from the transcript's last assistant record and last write time, checked
in this order:

| Condition | Status |
|---|---|
| The last assistant record has `stop_reason: end_turn` | Awaiting you (finished) |
| The file was written in the last 6 seconds | Working |
| The file hasn't been written for 30 seconds or more | Awaiting you (idle) |
| Anything else | Working |

The third rule is a guess. A session waiting at a permission prompt and a session
running a long tool look the same in the transcript, since both stop at a `tool_use`
record.

### With hooks

Precise mode registers `AgentMonitor.HookSink.exe` for four events. It writes a file
per session to `agent-monitor/status/` under the Claude config directory.

| Hook event | Result |
|---|---|
| `UserPromptSubmit` | Working |
| `Stop` | Awaiting you (finished) |
| `Notification` | Awaiting you (permission request or idle) |
| `SessionEnd` | Status file deleted |

There are no per-tool hooks, so the overhead stays low.

If the transcript is written more than 2 seconds after a `Stop` or `Notification`, the
session has resumed, for example because you approved a tool, and it's reported as
working. Sessions with no status file use the transcript rules above.

A registry record can also carry an explicit `tempo` (`active`, `idle` or `blocked`),
as daemon-backed sessions do. This takes priority over hooks and the transcript.

`HookStatusInterpreter` and `HeuristicStatusInterpreter` implement this logic behind
`ISessionStatusInterpreter`.

## Codex status

Codex's files are read from `~/.codex`, or from `CODEX_HOME` if it's set.

Status comes from the rollout logs at `sessions/YYYY/MM/DD/rollout-*.jsonl`, which
record turn events explicitly. Only the last turn-lifecycle event counts: `task_started`,
`task_complete` or an approval request. Other events, such as `token_count`, are ignored.

| Condition | Status |
|---|---|
| The last turn-lifecycle event is `task_complete` | Awaiting you (finished) |
| The last turn-lifecycle event is an approval request | Awaiting you (needs approval) |
| The last turn-lifecycle event is `task_started`, or the file was written in the last 6 seconds | Working |
| Anything else | Unknown |

Sessions are found in one of two ways:

- **Desktop app database (preferred).** If `state_5.sqlite` exists, its `threads` table
  provides thread titles, an archived flag and a recency timestamp, and points to the
  rollout files. The app is using this database, so we query a copy of it and its WAL.
- **File scan (fallback).** Otherwise, the provider looks through `sessions/` for
  recently modified logs and names each session after its working directory.

Threads that have no local rollout file, such as cloud threads, aren't monitored.

Codex has no per-session process registry, which has two consequences:

- A session is only listed if it was active in the last 15 minutes and a `codex` process
  is running. Idle sessions drop off the list after that, even if they're still open.
- Sessions have no process ID, so focusing a window can't clear their green status.

## Choosing the icon colour

Providers report each session's raw status. `AttentionTracker` lives in the tray app,
because it depends on window focus and elapsed time, and it picks the colour:

- **Green:** a session is `AwaitingInput`, started waiting less than 5 minutes ago, and
  hasn't been acknowledged.
- **Amber:** no session qualifies for green, and at least one is `Working`.
- **Grey:** everything else.

A session counts as acknowledged when the foreground window's process is on the same
parent/child chain as the session's process, and no other session is on that chain.
Windows exposes no per-tab signal, so a window hosting several sessions can't acknowledge
any of them. If it could, a background tab finishing would be cleared just because the
app happened to be in front. See `ForegroundWindow`.

## Adding a provider

1. Implement `ISessionProvider`. `GetSessions()` runs every second, so it must be cheap
   and shouldn't throw. Map the tool's states onto `SessionStatus`, and set `LastActivity`
   to the time the session entered its current state.
2. Add the provider to the list in the `TrayApplicationContext` constructor.

Nothing else needs to change. `AgentMonitor.Providers.Codex` is a good example to follow.

## Versioning

`Directory.Build.props` sets the base version and adds the short git commit, giving a
version like `0.1.0+ab12cd34`. The About box shows this. Release builds take the version
number from the git tag.

## CI and releases

CI (`.github/workflows/ci.yml`) builds and tests on `windows-latest` for every pull
request and every push to `main`.

Pushing a `v*` tag runs `.github/workflows/release.yml`. It runs the tests, publishes
framework-dependent `win-x64` builds of the tray app and hook sink, zips them, and
creates a GitHub release.

```sh
git tag v0.2.0
git push origin v0.2.0
```
