#!/usr/bin/env node
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { spawn, spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';

const EXIT = Object.freeze({ PASS: 0, ERROR: 1, BLOCKED_OR_INCOMPLETE: 2, USAGE: 64 });
const SECRET_NAME = /(token|secret|password|passwd|api[_-]?key|authorization|bearer|credential|anthropic|openai|gemini|provider[_-]?key)/i;
const SECRET_VALUE_PATTERNS = [
  /\b(sk-[A-Za-z0-9_-]{8,})\b/g,
  /\b(gh[pousr]_[A-Za-z0-9_]{8,})\b/g,
  /(Authorization\s*[:=]\s*)([^\r\n,;]+)/gi,
  /(Bearer\s+)([A-Za-z0-9._~+/=-]{8,})/gi,
  /((?:api[_-]?key|token|secret|password|credential)\s*[:=]\s*["']?)([^\s"',;]+)/gi,
];

function redact(value, key = '') {
  if (value == null) return value;
  if (SECRET_NAME.test(key)) return '[REDACTED]';
  if (Array.isArray(value)) return value.map((v) => redact(v));
  if (typeof value === 'object') {
    const out = {};
    for (const [k, v] of Object.entries(value)) out[k] = redact(v, k);
    return out;
  }
  if (typeof value !== 'string') return value;
  let out = value;
  for (const pattern of SECRET_VALUE_PATTERNS) {
    out = out.replace(pattern, (...args) => {
      const full = args[0];
      if (/^(Authorization|Bearer|api[_-]?key|token|secret|password|credential)/i.test(full)) {
        const prefix = args[1] ?? '';
        return `${prefix}[REDACTED]`;
      }
      return '[REDACTED]';
    });
  }
  return out;
}

function parseArgs(argv) {
  const args = {
    mode: 'all',
    json: null,
    fccClaude: null,
    healthUrl: null,
    prompt: 'Reply with exactly FCC_PROBE_OK and nothing else.',
    timeoutMs: 45000,
    allowLivePrompt: false,
    cliArgsJson: null,
    cancelAfterMs: 2500,
    workspaceRoot: null,
  };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const take = () => {
      if (i + 1 >= argv.length) throw new Error(`Missing value for ${a}`);
      return argv[++i];
    };
    if (a === '--mode') args.mode = take();
    else if (a === '--json') args.json = take();
    else if (a === '--fcc-claude') args.fccClaude = take();
    else if (a === '--health-url') args.healthUrl = take();
    else if (a === '--prompt') args.prompt = take();
    else if (a === '--timeout-ms') args.timeoutMs = Number(take());
    else if (a === '--allow-live-prompt') args.allowLivePrompt = true;
    else if (a === '--cli-args-json') args.cliArgsJson = take();
    else if (a === '--cancel-after-ms') args.cancelAfterMs = Number(take());
    else if (a === '--workspace-root') args.workspaceRoot = take();
    else if (a === '--help' || a === '-h') args.help = true;
    else throw new Error(`Unknown argument: ${a}`);
  }
  if (!['all', 'discovery', 'cli'].includes(args.mode)) throw new Error(`Invalid --mode: ${args.mode}`);
  if (!Number.isFinite(args.timeoutMs) || args.timeoutMs < 1000) throw new Error('--timeout-ms must be >= 1000');
  if (!Number.isFinite(args.cancelAfterMs) || args.cancelAfterMs < 250) throw new Error('--cancel-after-ms must be >= 250');
  return args;
}

function usage() {
  console.log(`FCC / fcc-claude P00 contract probe\n\nUsage:\n  node probe.mjs [options]\n\nOptions:\n  --mode discovery|cli|all\n  --json <output.json>\n  --fcc-claude <explicit executable path>\n  --health-url <http://127.0.0.1:PORT/...>\n  --allow-live-prompt                Explicitly permit a real provider-backed prompt\n  --prompt <text>\n  --cli-args-json <json-array>       Override invocation args; use {prompt} placeholder\n  --timeout-ms <ms>\n  --cancel-after-ms <ms>\n  --workspace-root <safe temp root>\n\nExit codes:\n  0 = requested contract evidence completed\n  1 = probe infrastructure/error\n  2 = target runtime unavailable or required live evidence not completed\n  64 = usage error\n`);
}

function whichAll(name) {
  const pathValue = process.env.PATH ?? '';
  const dirs = pathValue.split(path.delimiter).filter(Boolean);
  const exts = process.platform === 'win32'
    ? (process.env.PATHEXT ?? '.COM;.EXE;.BAT;.CMD').split(';').filter(Boolean)
    : [''];
  const names = path.extname(name) || process.platform !== 'win32' ? [name] : exts.map((e) => `${name}${e.toLowerCase()}`).concat(exts.map((e) => `${name}${e.toUpperCase()}`));
  const found = [];
  for (const dir of dirs) {
    for (const candidateName of names) {
      const candidate = path.resolve(dir, candidateName);
      try {
        const st = fs.statSync(candidate);
        if (st.isFile()) found.push(candidate);
      } catch {}
    }
  }
  return [...new Set(found.map((p) => path.normalize(p)))];
}

function resolveSpawn(exe, args) {
  const ext = path.extname(exe).toLowerCase();
  if (process.platform === 'win32' && (ext === '.cmd' || ext === '.bat')) {
    const powershell = whichAll('pwsh')[0] ?? whichAll('powershell')[0];
    if (!powershell) return { file: exe, args, wrapper: null, wrapperError: 'PowerShell required to launch .cmd/.bat safely but was not found.' };
    const script = '$target=$args[0]; $rest=@(); if($args.Count -gt 1){$rest=$args[1..($args.Count-1)]}; & $target @rest; exit $LASTEXITCODE';
    return { file: powershell, args: ['-NoProfile', '-NonInteractive', '-Command', script, exe, ...args], wrapper: 'powershell-call-operator', wrapperError: null };
  }
  return { file: exe, args, wrapper: null, wrapperError: null };
}

function runSync(exe, args, options = {}) {
  const started = Date.now();
  const launch = resolveSpawn(exe, args);
  if (launch.wrapperError) return redact({ command: [exe, ...args], wrapper: launch.wrapper, exitCode: null, signal: null, error: { message: launch.wrapperError }, stdout: '', stderr: '', durationMs: Date.now() - started });
  try {
    const res = spawnSync(launch.file, launch.args, {
      cwd: options.cwd,
      encoding: 'utf8',
      timeout: options.timeoutMs ?? 7000,
      windowsHide: true,
      shell: false,
      env: options.env ?? process.env,
      maxBuffer: 1024 * 1024,
    });
    return redact({
      command: [exe, ...args],
      wrapper: launch.wrapper,
      exitCode: res.status,
      signal: res.signal,
      error: res.error ? { code: res.error.code, message: res.error.message } : null,
      stdout: (res.stdout ?? '').slice(0, 12000),
      stderr: (res.stderr ?? '').slice(0, 12000),
      durationMs: Date.now() - started,
    });
  } catch (error) {
    return redact({ command: [exe, ...args], wrapper: launch.wrapper, exitCode: null, signal: null, error: { message: String(error) }, stdout: '', stderr: '', durationMs: Date.now() - started });
  }
}

function probeExecutable(name, explicitPath = null) {
  const paths = explicitPath ? [path.resolve(explicitPath)] : whichAll(name);
  const existing = paths.filter((p) => {
    try { return fs.statSync(p).isFile(); } catch { return false; }
  });
  const result = { name, found: existing.length > 0, paths: existing, version: null, help: null };
  if (!result.found) return result;
  const exe = existing[0];
  const versionAttempts = [['--version'], ['version'], ['-V']];
  for (const a of versionAttempts) {
    const r = runSync(exe, a);
    if (r.error?.code === 'ETIMEDOUT') continue;
    if (r.exitCode === 0 && `${r.stdout}\n${r.stderr}`.trim()) { result.version = r; break; }
  }
  const helpAttempts = [['--help'], ['help'], ['-h']];
  for (const a of helpAttempts) {
    const r = runSync(exe, a);
    if (r.error?.code === 'ETIMEDOUT') continue;
    if (`${r.stdout}\n${r.stderr}`.trim()) { result.help = r; break; }
  }
  return redact(result);
}

function versionProbe(name, args = ['--version']) {
  const found = whichAll(name);
  if (!found.length) return { found: false, paths: [] };
  return { found: true, paths: found, result: runSync(found[0], args) };
}

function listConfigMetadata() {
  const home = os.homedir();
  const candidates = [];
  const envPathNames = ['FCC_CONFIG', 'FCC_CONFIG_PATH', 'FCC_HOME', 'FCC_CLAUDE_CONFIG', 'CLAUDE_CONFIG_DIR'];
  for (const envName of envPathNames) {
    const v = process.env[envName];
    if (v && !SECRET_NAME.test(envName)) candidates.push({ source: `env:${envName}`, path: v });
  }
  const roots = [
    home,
    process.env.APPDATA,
    process.env.LOCALAPPDATA,
    process.env.PROGRAMDATA,
  ].filter(Boolean);
  const names = ['.fcc', 'fcc', 'fcc-claude', '.claude', 'claude'];
  for (const root of roots) for (const name of names) candidates.push({ source: 'common-location', path: path.join(root, name) });
  const seen = new Set();
  const metadata = [];
  for (const c of candidates) {
    const normalized = path.normalize(c.path);
    if (seen.has(normalized)) continue;
    seen.add(normalized);
    try {
      const st = fs.statSync(normalized);
      const item = { source: c.source, path: normalized, exists: true, type: st.isDirectory() ? 'directory' : 'file', size: st.isFile() ? st.size : null, children: [] };
      if (st.isDirectory()) {
        item.children = fs.readdirSync(normalized, { withFileTypes: true }).slice(0, 100).map((d) => ({ name: d.name, type: d.isDirectory() ? 'directory' : 'file' }));
      }
      metadata.push(item);
    } catch {
      metadata.push({ source: c.source, path: normalized, exists: false });
    }
  }
  return redact(metadata);
}

function environmentVariablePresence() {
  const interesting = Object.keys(process.env).filter((k) => /^(FCC|CLAUDE|ANTHROPIC|MODEL|PROVIDER)/i.test(k)).sort();
  return interesting.map((name) => ({ name, present: true, value: SECRET_NAME.test(name) ? '[REDACTED]' : redact(process.env[name] ?? '') }));
}

function processSnapshot() {
  if (process.platform === 'win32') {
    const pwsh = whichAll('pwsh')[0] ?? whichAll('powershell')[0];
    if (!pwsh) return { source: 'powershell', error: 'PowerShell not found', processes: [] };
    const command = 'Get-CimInstance Win32_Process | Select-Object ProcessId,ParentProcessId,Name | ConvertTo-Json -Compress';
    const r = runSync(pwsh, ['-NoProfile', '-Command', command], { timeoutMs: 10000 });
    try {
      const raw = JSON.parse(r.stdout || '[]');
      const rows = Array.isArray(raw) ? raw : [raw];
      return {
        source: 'Win32_Process',
        exitCode: r.exitCode,
        processes: rows.filter(Boolean).map((x) => ({ pid: Number(x.ProcessId), ppid: Number(x.ParentProcessId), name: String(x.Name ?? '') })),
      };
    } catch (error) {
      return { source: 'Win32_Process', exitCode: r.exitCode, error: `Unable to parse process snapshot: ${error}`, processes: [] };
    }
  }
  const ps = runSync('ps', ['-eo', 'pid=,ppid=,comm=']);
  const processes = [];
  for (const line of (ps.stdout ?? '').split(/\r?\n/)) {
    const m = line.trim().match(/^(\d+)\s+(\d+)\s+(.+)$/);
    if (m) processes.push({ pid: Number(m[1]), ppid: Number(m[2]), name: m[3] });
  }
  return { source: 'ps', exitCode: ps.exitCode, processes };
}

function listRelevantProcesses(snapshot) {
  const processes = (snapshot?.processes ?? []).filter((p) => /(fcc|claude|node)/i.test(p.name ?? ''));
  return { source: snapshot?.source ?? null, error: snapshot?.error ?? null, processes };
}

function descendantsOf(snapshot, rootPid) {
  const rows = snapshot?.processes ?? [];
  const byParent = new Map();
  for (const row of rows) {
    if (!byParent.has(row.ppid)) byParent.set(row.ppid, []);
    byParent.get(row.ppid).push(row);
  }
  const result = [];
  const queue = [rootPid];
  const seen = new Set(queue);
  while (queue.length) {
    const parent = queue.shift();
    for (const child of byParent.get(parent) ?? []) {
      if (seen.has(child.pid)) continue;
      seen.add(child.pid);
      result.push(child);
      queue.push(child.pid);
    }
  }
  return result;
}

function listeningPortsForProcesses(relevant) {
  const pids = new Set((relevant?.processes ?? []).map((p) => p.pid));
  if (!pids.size) return { source: null, listeners: [] };
  if (process.platform === 'win32') {
    const r = runSync('netstat.exe', ['-ano', '-p', 'tcp'], { timeoutMs: 10000 });
    const listeners = [];
    for (const line of (r.stdout ?? '').split(/\r?\n/)) {
      const m = line.trim().match(/^TCP\s+(\S+):(\d+)\s+\S+\s+LISTENING\s+(\d+)$/i);
      if (!m) continue;
      const pid = Number(m[3]);
      if (pids.has(pid)) listeners.push({ address: m[1], port: Number(m[2]), pid });
    }
    return { source: 'netstat -ano -p tcp', exitCode: r.exitCode, listeners };
  }
  const ssPath = whichAll('ss')[0];
  if (!ssPath) return { source: 'ss', error: 'ss not found', listeners: [] };
  const r = runSync(ssPath, ['-ltnp'], { timeoutMs: 10000 });
  const listeners = [];
  for (const line of (r.stdout ?? '').split(/\r?\n/)) {
    const portMatch = line.match(/LISTEN\s+\d+\s+\d+\s+\S+:(\d+)\s+/);
    const pidMatch = line.match(/pid=(\d+)/);
    if (portMatch && pidMatch && pids.has(Number(pidMatch[1]))) listeners.push({ port: Number(portMatch[1]), pid: Number(pidMatch[1]) });
  }
  return { source: 'ss -ltnp', exitCode: r.exitCode, listeners };
}

function inferCliArgs(helpText, prompt) {
  if (!helpText) return { strategy: 'unknown', args: null, reason: 'No help output available.' };
  const text = helpText.toLowerCase();
  if (text.includes('--print')) return { strategy: '--print', args: ['--print', prompt] };
  if (text.includes('--prompt')) return { strategy: '--prompt', args: ['--prompt', prompt] };
  if (/(^|\s)-p([,\s]|$)/m.test(text)) return { strategy: '-p', args: ['-p', prompt] };
  return { strategy: 'unknown', args: null, reason: 'No non-interactive prompt flag was safely inferred from help output.' };
}

function classifyText(stdout, stderr, exitCode, timedOut, cancelled) {
  const text = `${stdout}\n${stderr}`.toLowerCase();
  if (cancelled) return 'CANCELLED';
  if (timedOut) return 'TIMEOUT';
  if (/429|too many requests|rate.?limit/.test(text)) return 'RATE_LIMITED';
  if (/auth|unauthori[sz]ed|forbidden|api.?key|credential/.test(text) && exitCode !== 0) return 'AUTH_OR_PROVIDER_ERROR';
  if (/model.*(not found|invalid|unavailable)|provider.*(not found|invalid|unavailable)/.test(text)) return 'MODEL_OR_PROVIDER_UNAVAILABLE';
  if (/econnrefused|connection refused|fcc.*unavailable|server.*unavailable/.test(text)) return 'FCC_UNAVAILABLE';
  if (exitCode === 0) return 'SUCCESS';
  if (exitCode == null) return 'LAUNCH_OR_SIGNAL_FAILURE';
  return 'NON_ZERO_EXIT';
}

function extractSessionIds(text) {
  const out = [];
  const patterns = [
    /session(?:[_ -]?id)?\s*[:=]\s*["']?([0-9a-zA-Z_-]{8,})/gi,
    /\b([0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12})\b/gi,
  ];
  for (const p of patterns) {
    for (const match of text.matchAll(p)) out.push(match[1]);
  }
  return [...new Set(out)].slice(0, 10);
}

async function runStreaming(exe, args, options = {}) {
  const events = [];
  const started = Date.now();
  let timedOut = false;
  let cancelled = false;
  let forcedTreeKill = false;
  let gracefulSignalAttempted = false;
  let child;
  const launch = resolveSpawn(exe, args);
  if (launch.wrapperError) return { launchError: launch.wrapperError, pid: null, exitCode: null, signal: null, events, stdout: '', stderr: '', durationMs: Date.now() - started, classification: 'LAUNCH_OR_SIGNAL_FAILURE', wrapper: launch.wrapper };
  try {
    child = spawn(launch.file, launch.args, {
      cwd: options.cwd,
      env: process.env,
      windowsHide: true,
      shell: false,
      detached: process.platform !== 'win32',
      stdio: ['ignore', 'pipe', 'pipe'],
    });
  } catch (error) {
    return { launchError: String(error), pid: null, exitCode: null, signal: null, events, stdout: '', stderr: '', durationMs: Date.now() - started, classification: 'LAUNCH_OR_SIGNAL_FAILURE', wrapper: launch.wrapper };
  }
  await new Promise((r) => setTimeout(r, 150));
  const initialSnapshot = processSnapshot();
  const observedProcessTree = [{ pid: child.pid, role: launch.wrapper ? 'launcher-wrapper' : 'launcher', name: path.basename(launch.file) }, ...descendantsOf(initialSnapshot, child.pid).map((x) => ({ ...x, role: 'descendant' }))];
  let stdout = '';
  let stderr = '';
  const push = (stream, chunk) => {
    const text = chunk.toString('utf8');
    if (stream === 'stdout') stdout += text; else stderr += text;
    events.push({ atMs: Date.now() - started, stream, text: redact(text) });
  };
  child.stdout.on('data', (d) => push('stdout', d));
  child.stderr.on('data', (d) => push('stderr', d));

  const timeoutTimer = setTimeout(() => { timedOut = true; }, options.timeoutMs ?? 45000);
  let cancelTimer = null;
  if (options.cancelAfterMs != null) cancelTimer = setTimeout(() => { cancelled = true; }, options.cancelAfterMs);

  const exit = await new Promise((resolve) => {
    let escalationRunning = false;
    const watcher = setInterval(async () => {
      if ((timedOut || cancelled) && !escalationRunning && child.exitCode == null) {
        escalationRunning = true;
        gracefulSignalAttempted = true;
        try { child.kill('SIGINT'); } catch {}
        await new Promise((r) => setTimeout(r, 1200));
        if (child.exitCode == null) {
          if (process.platform === 'win32') {
            const kill = runSync('taskkill.exe', ['/PID', String(child.pid), '/T', '/F'], { timeoutMs: 7000 });
            forcedTreeKill = kill.exitCode === 0;
          } else {
            try { process.kill(-child.pid, 'SIGKILL'); forcedTreeKill = true; } catch {
              try { child.kill('SIGKILL'); forcedTreeKill = true; } catch {}
            }
          }
        }
      }
    }, 100);
    child.on('error', (error) => { clearInterval(watcher); resolve({ code: null, signal: null, error: String(error) }); });
    child.on('exit', (code, signal) => { clearInterval(watcher); resolve({ code, signal, error: null }); });
  });
  clearTimeout(timeoutTimer);
  if (cancelTimer) clearTimeout(cancelTimer);
  const redactedStdout = redact(stdout.slice(0, 200000));
  const redactedStderr = redact(stderr.slice(0, 200000));
  await new Promise((r) => setTimeout(r, 150));
  const finalSnapshot = processSnapshot();
  const observedIds = new Set(observedProcessTree.map((x) => x.pid));
  const remainingOwnedProcesses = (finalSnapshot.processes ?? []).filter((x) => observedIds.has(x.pid));
  return {
    pid: child.pid,
    wrapper: launch.wrapper,
    observedProcessTree,
    remainingOwnedProcesses,
    processTreeCleanupObserved: remainingOwnedProcesses.length === 0,
    exitCode: exit.code,
    signal: exit.signal,
    launchError: exit.error,
    durationMs: Date.now() - started,
    timedOut,
    cancelled,
    gracefulSignalAttempted,
    forcedTreeKill,
    stdout: redactedStdout,
    stderr: redactedStderr,
    events: events.slice(0, 1000),
    classification: classifyText(redactedStdout, redactedStderr, exit.code, timedOut, cancelled),
    sessionIds: extractSessionIds(`${redactedStdout}\n${redactedStderr}`),
  };
}

async function probeHealth(url) {
  if (!url) return { attempted: false, reason: 'No explicit or safely discovered health URL.' };
  let parsed;
  try { parsed = new URL(url); } catch { return { attempted: false, reason: 'Invalid health URL.' }; }
  if (!['127.0.0.1', 'localhost', '::1'].includes(parsed.hostname)) return { attempted: false, reason: 'Refused non-loopback health URL.' };
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 4000);
  try {
    const res = await fetch(parsed, { signal: controller.signal, headers: { Accept: 'application/json,text/plain;q=0.8,*/*;q=0.1' } });
    const rawBody = (await res.text()).slice(0, 12000);
    let body;
    try { body = redact(JSON.parse(rawBody)); } catch { body = redact(rawBody); }
    return { attempted: true, url: parsed.toString(), status: res.status, ok: res.ok, body };
  } catch (error) {
    return { attempted: true, url: parsed.toString(), error: String(error) };
  } finally { clearTimeout(timer); }
}

async function probeHealthCandidates(explicitUrl, ports) {
  if (explicitUrl) return { mode: 'explicit', results: [await probeHealth(explicitUrl)] };
  const uniquePorts = [...new Set((ports ?? []).filter((p) => Number.isInteger(p) && p > 0 && p < 65536))].slice(0, 8);
  if (!uniquePorts.length) return { mode: 'none', results: [{ attempted: false, reason: 'No explicit or process-correlated FCC port discovered.' }] };
  const results = [];
  for (const port of uniquePorts) {
    for (const suffix of ['/health', '/status', '/']) {
      const r = await probeHealth(`http://127.0.0.1:${port}${suffix}`);
      results.push(r);
      if (r.ok) break;
    }
  }
  return { mode: 'process-correlated', results };
}

async function buildDiscovery(args) {
  const powershell = process.platform === 'win32'
    ? (() => {
        const pwsh = whichAll('pwsh');
        const winps = whichAll('powershell');
        const exe = pwsh[0] ?? winps[0];
        return exe ? runSync(exe, ['-NoProfile', '-Command', '$PSVersionTable.PSVersion.ToString()']) : { found: false };
      })()
    : { found: false, reason: 'Target is Windows; this probe host is not Windows.' };
  const executables = {
    fcc: probeExecutable('fcc'),
    fccServer: probeExecutable('fcc-server'),
    fccClaude: probeExecutable('fcc-claude', args.fccClaude),
    claude: probeExecutable('claude'),
  };
  const explicitPort = process.env.FCC_PORT && /^\d+$/.test(process.env.FCC_PORT) ? Number(process.env.FCC_PORT) : null;
  const snapshot = processSnapshot();
  const relevantProcesses = listRelevantProcesses(snapshot);
  const listening = listeningPortsForProcesses(relevantProcesses);
  const fccProcessIds = new Set(relevantProcesses.processes.filter((p) => /fcc/i.test(p.name ?? '')).map((p) => p.pid));
  const processCorrelatedPorts = listening.listeners.filter((x) => fccProcessIds.has(x.pid)).map((x) => x.port);
  const healthPorts = explicitPort ? [explicitPort, ...processCorrelatedPorts] : processCorrelatedPorts;
  return redact({
    schemaVersion: 1,
    probeId: randomUUID(),
    capturedAtUtc: new Date().toISOString(),
    host: {
      platform: process.platform,
      osType: os.type(),
      osRelease: os.release(),
      osVersion: typeof os.version === 'function' ? os.version() : null,
      arch: os.arch(),
      node: process.version,
      powershell,
      git: versionProbe('git', ['--version']),
      dotnet: versionProbe('dotnet', ['--version']),
      python: versionProbe(process.platform === 'win32' ? 'python' : 'python3', ['--version']),
    },
    executables,
    configMetadata: listConfigMetadata(),
    environmentVariablePresence: environmentVariablePresence(),
    relevantProcesses,
    listeningPorts: listening,
    fccPort: { fromEnvironment: explicitPort, processCorrelated: processCorrelatedPorts },
    health: await probeHealthCandidates(args.healthUrl, healthPorts),
    secretsPolicy: 'Values matching secret names/patterns are redacted before output.',
  });
}

function parseCliOverride(json, prompt) {
  if (!json) return null;
  const parsed = JSON.parse(json);
  if (!Array.isArray(parsed) || !parsed.every((x) => typeof x === 'string')) throw new Error('--cli-args-json must be a JSON array of strings');
  return { strategy: 'explicit-override', args: parsed.map((x) => x.replaceAll('{prompt}', prompt)) };
}

async function buildCliProbe(args, discovery) {
  const runtime = discovery?.executables?.fccClaude ?? probeExecutable('fcc-claude', args.fccClaude);
  const result = {
    schemaVersion: 1,
    capturedAtUtc: new Date().toISOString(),
    runtimeFound: runtime.found,
    executable: runtime.paths?.[0] ?? null,
    livePromptAllowed: args.allowLivePrompt,
    invocationStrategy: null,
    workspaceCases: [],
    cancellationCase: null,
    fallbackAssessment: 'NOT_VERIFIED',
    unsupportedOrNotVerified: [],
  };
  if (!runtime.found) {
    result.fallbackAssessment = 'BLOCKED_RUNTIME_NOT_FOUND';
    result.unsupportedOrNotVerified.push('prompt transmission', 'working-directory execution', 'streaming', 'completion extraction', 'real exit-code model', 'cancellation', 'process-tree cleanup', 'session/resume');
    return result;
  }
  const helpText = `${runtime.help?.stdout ?? ''}\n${runtime.help?.stderr ?? ''}`;
  const strategy = parseCliOverride(args.cliArgsJson, args.prompt) ?? inferCliArgs(helpText, args.prompt);
  result.invocationStrategy = strategy;
  if (!strategy.args) {
    result.fallbackAssessment = 'BLOCKED_NO_SAFE_PROMPT_INVOCATION_INFERRED';
    result.unsupportedOrNotVerified.push('real prompt invocation; provide --cli-args-json only after verifying local help/contract');
    return result;
  }
  if (!args.allowLivePrompt) {
    result.fallbackAssessment = 'LIVE_PROMPT_NOT_AUTHORIZED_FOR_THIS_RUN';
    result.unsupportedOrNotVerified.push('real provider-backed prompt transmission requires --allow-live-prompt');
    return result;
  }

  const base = args.workspaceRoot ? path.resolve(args.workspaceRoot) : fs.mkdtempSync(path.join(os.tmpdir(), 'fcc-contract-probe-'));
  const ownsBase = !args.workspaceRoot;
  const workspaces = [
    path.join(base, 'normal'),
    path.join(base, 'path with spaces'),
    path.join(base, 'مسار-اختبار'),
  ];
  try {
    for (const cwd of workspaces) {
      fs.mkdirSync(cwd, { recursive: true });
      const run = await runStreaming(result.executable, strategy.args, { cwd, timeoutMs: args.timeoutMs });
      result.workspaceCases.push({ cwdCase: path.basename(cwd), cwd, run });
    }
    const cancelCwd = path.join(base, 'cancel-case');
    fs.mkdirSync(cancelCwd, { recursive: true });
    result.cancellationCase = await runStreaming(result.executable, strategy.args, { cwd: cancelCwd, timeoutMs: args.timeoutMs, cancelAfterMs: args.cancelAfterMs });
    const completedRuns = result.workspaceCases.map((x) => x.run);
    const successCount = completedRuns.filter((r) => r.classification === 'SUCCESS').length;
    const observable = completedRuns.every((r) => Array.isArray(r.events));
    const cwdPass = successCount === completedRuns.length;
    const cancelPass = ['CANCELLED', 'NON_ZERO_EXIT', 'LAUNCH_OR_SIGNAL_FAILURE'].includes(result.cancellationCase?.classification) && (result.cancellationCase?.gracefulSignalAttempted || result.cancellationCase?.forcedTreeKill);
    result.fallbackAssessment = successCount === completedRuns.length && observable && cancelPass ? 'VERIFIED_FOR_TESTED_RUNTIME' : 'PARTIAL_OR_FAILED';
    result.summary = {
      runtimeLaunch: successCount > 0 ? 'PASS' : 'FAIL',
      promptTransmission: successCount > 0 ? 'PASS' : 'FAIL',
      workingDirectoryCases: cwdPass ? 'PASS' : 'FAIL',
      outputObservability: observable ? 'PASS' : 'FAIL',
      cancellation: cancelPass ? 'PASS' : 'FAIL',
      sessionIdsObserved: [...new Set(completedRuns.flatMap((r) => r.sessionIds ?? []))],
      rateLimitObserved: completedRuns.some((r) => r.classification === 'RATE_LIMITED') || result.cancellationCase?.classification === 'RATE_LIMITED',
    };
    if (!result.summary.sessionIdsObserved.length) result.unsupportedOrNotVerified.push('session identifier not observed');
    result.unsupportedOrNotVerified.push('resume/continuation is not auto-attempted unless a stable tested CLI contract is documented from observed help/runtime behavior');
  } finally {
    if (ownsBase) {
      try { fs.rmSync(base, { recursive: true, force: true }); } catch {}
    }
  }
  return redact(result);
}

function resultExitCode(args, discovery, cli) {
  if ((args.mode === 'discovery' || args.mode === 'all') && !discovery.executables.fccClaude.found) return EXIT.BLOCKED_OR_INCOMPLETE;
  if (args.mode === 'cli' || args.mode === 'all') {
    if (!cli || cli.fallbackAssessment !== 'VERIFIED_FOR_TESTED_RUNTIME') return EXIT.BLOCKED_OR_INCOMPLETE;
  }
  return EXIT.PASS;
}

async function main() {
  let args;
  try { args = parseArgs(process.argv.slice(2)); }
  catch (error) { console.error(`Usage error: ${error.message}`); usage(); process.exit(EXIT.USAGE); }
  if (args.help) { usage(); process.exit(EXIT.PASS); }
  const output = { schemaVersion: 1, probe: 'fcc-p00-contract', requestedMode: args.mode, capturedAtUtc: new Date().toISOString(), discovery: null, cli: null };
  try {
    if (args.mode === 'discovery' || args.mode === 'all') output.discovery = await buildDiscovery(args);
    if (args.mode === 'cli' || args.mode === 'all') output.cli = await buildCliProbe(args, output.discovery);
    const sanitized = redact(output);
    const json = `${JSON.stringify(sanitized, null, 2)}\n`;
    if (args.json) {
      fs.mkdirSync(path.dirname(path.resolve(args.json)), { recursive: true });
      fs.writeFileSync(path.resolve(args.json), json, { encoding: 'utf8', mode: 0o600 });
    }
    console.log(JSON.stringify({
      mode: args.mode,
      fccClaudeFound: sanitized.discovery?.executables?.fccClaude?.found ?? sanitized.cli?.runtimeFound ?? null,
      cliFallback: sanitized.cli?.fallbackAssessment ?? 'NOT_REQUESTED',
      outputFile: args.json ? path.resolve(args.json) : null,
    }, null, 2));
    process.exit(resultExitCode(args, sanitized.discovery, sanitized.cli));
  } catch (error) {
    console.error(`Probe failed: ${redact(String(error))}`);
    process.exit(EXIT.ERROR);
  }
}

await main();
