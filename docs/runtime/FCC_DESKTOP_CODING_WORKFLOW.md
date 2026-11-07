# Integrated desktop coding workflow

Owner instruction: 2026-11-06. Work is integrated directly on `main`; no new branches.

## Launch and repository ownership

The desktop checks for `fcc-claude` and `fcc-server` on the configured PATH. It reuses a healthy loopback FCC service, or starts `fcc-server` as a supervised owned process with bounded output and a readiness deadline. Startup is serialized. Failed/cancelled startup cleans up the owned tree; shutdown never terminates an externally started server. The header exposes readiness/failure and Retry FCC. Health is not provider acceptance.

Opening a repository navigates to its chat. A first message creates a persisted session automatically. The runtime receives that repository's actual root as its working directory. Project/session changes are rejected while starting or executing a task. Models are discovered from the local server's `/v1/models` catalog, bounded to loopback, without proxies or redirects. The chosen model is saved as a non-secret SQLite preference after submission and cannot change during a task.

The coding instructions direct the agent to read applicable repository guidance and inspect `TASKS.md`, `TODO.md`, or the documented task ledger when the user requests repository work. They do not authorize pushes, deployment, destructive actions, secret disclosure, or changes in unrelated directories.

## Tool approvals

Desktop sessions use a bidirectional stream-json CLI transport. The installed CLI's control initialization and `can_use_tool` requests are correlated by request ID. The user sees the tool, repository and sanitized exact input, and can allow that request once or deny it. Approval is never a blanket permission bypass; only the original request input is returned to the CLI. Cancellation dismisses a pending prompt and stops the owned coding process. Missing or malformed terminal results cannot count as success.

Protocol reference: [Anthropic's SDK transport](https://github.com/anthropics/claude-agent-sdk-python/blob/main/src/claude_agent_sdk/_internal/transport/subprocess_cli.py) and [control-request handling](https://github.com/anthropics/claude-agent-sdk-python/blob/main/src/claude_agent_sdk/_internal/query.py). The implementation is project-owned C# and introduces no Python SDK dependency.

## Verification boundary

`tools/runtime/validate-fcc-server-startup.ps1 -RunFixtures -RequireRuntime` exercises lifecycle ownership, startup/retry/cancellation, loopback catalog limits, interactive approvals, exact request correlation/input, redaction, malformed protocol/absent terminal results, and WPF approval/session ownership behavior. It runs in the permanent Windows baseline. CI fixtures do not claim real provider acceptance.

A real local acceptance probe on 2026-11-06 started FCC to HTTP 200. The configured NIM default model returned an upstream 404; health alone did not conceal the failure. A model explicitly advertised by FCC, `claude-3-freecc-no-thinking/nvidia_nim/z-ai/glm-5.3-flash`, completed a controlled coding task in a disposable Git repository: read `TASKS.md`, create `calculator.py`, request a PowerShell operation (denied), request the exact whitelisted Bash verification command (allowed), return `PASS`, and finish with `AgentRuntimeTerminalState.Succeeded`. The generated function was inspected separately. This acceptance covers that exact task/model/runtime path, not all provider/model combinations or final release acceptance.

Full P04 owner acceptance, remaining mandatory P11–P22 implementation, packaging and clean-machine release acceptance remain outstanding. No final production release is claimed.
