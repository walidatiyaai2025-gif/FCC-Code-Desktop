import path from 'node:path';
import process from 'node:process';
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { StringDecoder } from 'node:string_decoder';
import { maskSecretsPreserveLength, redact, redactString, resolveSpawn, runSync, whichAll } from './common.mjs';

const MAX_STREAM_CHARS = 2_000_000;

function processSnapshot() {
  if (process.platform === 'win32') {
    const pwsh = whichAll('pwsh')[0] ?? whichAll('powershell')[0];
    if (!pwsh) return { source: 'powershell', processes: [], error: 'PowerShell not found.' };
    const command = 'Get-CimInstance Win32_Process | Select-Object ProcessId,ParentProcessId,Name | ConvertTo-Json -Compress';
    const result = runSync(pwsh, ['-NoProfile', '-Command', command], { timeoutMs: 10000 });
    try {
      const parsed = JSON.parse(result.stdout || '[]');
      const rows = Array.isArray(parsed) ? parsed : [parsed];
      return { source: 'Win32_Process', processes: rows.filter(Boolean).map((x) => ({ pid: Number(x.ProcessId), ppid: Number(x.ParentProcessId), name: String(x.Name ?? '') })) };
    } catch (error) {
      return { source: 'Win32_Process', processes: [], error: `Unable to parse process snapshot: ${error}` };
    }
  }
  const result = runSync('ps', ['-eo', 'pid=,ppid=,comm=']);
  const processes = [];
  for (const line of (result.stdout ?? '').split(/\r?\n/)) {
    const match = line.trim().match(/^(\d+)\s+(\d+)\s+(.+)$/);
    if (match) processes.push({ pid: Number(match[1]), ppid: Number(match[2]), name: match[3] });
  }
  return { source: 'ps', processes };
}

function descendantsOf(snapshot, rootPid) {
  const byParent = new Map();
  for (const row of snapshot?.processes ?? []) {
    if (!byParent.has(row.ppid)) byParent.set(row.ppid, []);
    byParent.get(row.ppid).push(row);
  }
  const output = [];
  const queue = [rootPid];
  const seen = new Set(queue);
  while (queue.length) {
    const parent = queue.shift();
    for (const child of byParent.get(parent) ?? []) {
      if (seen.has(child.pid)) continue;
      seen.add(child.pid);
      output.push(child);
      queue.push(child.pid);
    }
  }
  return output;
}

function collectKeyPaths(value, predicate, base = '$', output = []) {
  if (value == null) return output;
  if (Array.isArray(value)) {
    value.forEach((item, index) => collectKeyPaths(item, predicate, `${base}[${index}]`, output));
    return output;
  }
  if (typeof value !== 'object') return output;
  for (const [key, child] of Object.entries(value)) {
    const keyPath = `${base}.${key}`;
    if (predicate(key, child)) output.push({ path: keyPath, value: redact(child, key) });
    collectKeyPaths(child, predicate, keyPath, output);
  }
  return output;
}

export function extractSessionCandidatesFromJson(value) {
  return collectKeyPaths(value, (key, child) => /session.*id|session_id|sessionid/i.test(key) && ['string', 'number'].includes(typeof child))
    .map((x) => ({ source: 'json-key', path: x.path, value: String(x.value) }));
}

export function extractSessionCandidatesFromText(text) {
  const output = [];
  const patterns = [
    /session(?:[_ -]?id)?\s*[:=]\s*["']?([0-9a-zA-Z_-]{6,})/gi,
    /\b([0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12})\b/gi,
  ];
  for (const pattern of patterns) for (const match of String(text ?? '').matchAll(pattern)) output.push({ source: 'text-pattern', value: match[1] });
  const seen = new Set();
  return output.filter((item) => !seen.has(item.value) && seen.add(item.value));
}

function semanticHintsFromJson(value) {
  const hints = new Set();
  const keys = collectKeyPaths(value, (key) => /(delta|assistant|tool|result|progress|error|usage|token|session)/i.test(key));
  for (const item of keys) {
    const tail = item.path.split('.').at(-1).toLowerCase();
    if (tail.includes('delta')) hints.add('DELTA_LIKE_KEY');
    if (tail.includes('assistant')) hints.add('ASSISTANT_LIKE_KEY');
    if (tail.includes('tool')) hints.add('TOOL_LIKE_KEY');
    if (tail.includes('result')) hints.add('RESULT_LIKE_KEY');
    if (tail.includes('progress')) hints.add('PROGRESS_LIKE_KEY');
    if (tail.includes('error')) hints.add('ERROR_LIKE_KEY');
    if (tail.includes('usage') || tail.includes('token')) hints.add('USAGE_LIKE_KEY');
    if (tail.includes('session')) hints.add('SESSION_LIKE_KEY');
  }
  return { hints: [...hints], matchingKeys: keys.map((x) => x.path) };
}

export function analyzeLine(line, metadata = {}) {
  const sanitized = redactString(line);
  const trimmed = sanitized.trim();
  const base = { sequence: metadata.sequence ?? null, stream: metadata.stream ?? 'stdout', atMs: metadata.atMs ?? null, eofFlush: Boolean(metadata.eofFlush), rawSanitized: sanitized };
  if (!trimmed) return { ...base, classification: 'EMPTY_LINE', eventTypeHint: null, semanticHints: [], sessionCandidates: [] };
  if (!trimmed.startsWith('{') && !trimmed.startsWith('[')) {
    return { ...base, classification: 'TEXT_LINE', eventTypeHint: null, semanticHints: [], sessionCandidates: extractSessionCandidatesFromText(sanitized) };
  }
  try {
    const parsed = JSON.parse(trimmed);
    let eventTypeHint = null;
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      for (const key of ['type', 'event', 'kind', 'name']) if (typeof parsed[key] === 'string') { eventTypeHint = parsed[key]; break; }
    }
    const semantic = semanticHintsFromJson(parsed);
    return { ...base, classification: 'JSON_EVENT', eventTypeHint, semanticHints: semantic.hints, matchingKeys: semantic.matchingKeys, sessionCandidates: extractSessionCandidatesFromJson(parsed), parsed: redact(parsed) };
  } catch (error) {
    return { ...base, classification: 'MALFORMED_JSON', eventTypeHint: null, semanticHints: [], sessionCandidates: extractSessionCandidatesFromText(sanitized), parseError: redactString(String(error)) };
  }
}

function finalizeFrames(frameRecords) {
  const perStream = new Map();
  for (const record of frameRecords) {
    if (!perStream.has(record.stream)) perStream.set(record.stream, []);
    perStream.get(record.stream).push(record);
  }
  const sanitizedBySequence = new Map();
  for (const records of perStream.values()) {
    const whole = records.map((r) => r.decodedText).join('');
    const masked = maskSecretsPreserveLength(whole);
    let offset = 0;
    for (const record of records) {
      const length = record.decodedText.length;
      sanitizedBySequence.set(record.sequence, masked.slice(offset, offset + length));
      offset += length;
    }
  }
  return frameRecords.map((record) => ({ sequence: record.sequence, stream: record.stream, atMs: record.atMs, byteLength: record.byteLength, sha256: record.sha256, decoderFlush: Boolean(record.decoderFlush), sanitizedText: sanitizedBySequence.get(record.sequence) ?? '' }));
}

function buildLineAnalysis(frameRecords) {
  const buffers = new Map([['stdout', ''], ['stderr', '']]);
  const lineEvents = [];
  let sequence = 0;
  for (const frame of frameRecords.sort((a, b) => a.sequence - b.sequence)) {
    const current = (buffers.get(frame.stream) ?? '') + frame.decodedText;
    const pieces = current.split(/\r?\n/);
    buffers.set(frame.stream, pieces.pop() ?? '');
    for (const line of pieces) lineEvents.push(analyzeLine(line, { sequence: ++sequence, stream: frame.stream, atMs: frame.atMs }));
  }
  for (const [stream, remainder] of buffers.entries()) if (remainder) lineEvents.push(analyzeLine(remainder, { sequence: ++sequence, stream, atMs: null, eofFlush: true }));
  return lineEvents;
}

export function classifyFailure(run) {
  if (run?.runtimeFound === false || run?.classification === 'RUNTIME_NOT_FOUND') return { category: 'RUNTIME_NOT_FOUND', source: 'launch', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
  if (run?.timedOut) return { category: 'TIMEOUT', source: 'process', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
  if (run?.cancelled) return { category: 'INTERRUPTED', source: 'process', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
  const text = `${run?.stdout ?? ''}\n${run?.stderr ?? ''}`.toLowerCase();
  const malformed = (run?.lineEvents ?? []).some((event) => event.classification === 'MALFORMED_JSON');
  if (/--resume requires a valid session|does not match any session|session (?:id )?(?:was )?not found|invalid session/.test(text)) return { category: 'INVALID_SESSION', source: 'output', retryability: 'NOT_APPLICABLE', userActionRequired: 'YES' };
  if (/429|too many requests|rate.?limit|quota.*exceed/.test(text)) return { category: 'RATE_LIMITED', source: 'output', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
  if (/unauthori[sz]ed|forbidden|authentication|invalid api.?key|credential/.test(text)) return { category: 'AUTH_FAILURE', source: 'output', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
  if (/model[^\n]*(not found|invalid|unavailable|unsupported)/.test(text)) return { category: 'MODEL_UNAVAILABLE', source: 'output', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
  if (/provider[^\n]*(not found|invalid|unavailable|unsupported|busy|overload)/.test(text)) return { category: /busy|overload/.test(text) ? 'PROVIDER_BUSY_OR_OVERLOADED' : 'PROVIDER_UNAVAILABLE', source: 'output', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
  if (/econnrefused|connection refused|fcc[^\n]*(unavailable|not running|connection)/.test(text)) return { category: 'FCC_UNAVAILABLE', source: 'output', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
  if (malformed) return { category: 'MALFORMED_STREAM', source: 'stream-parser', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
  if (run?.signal && run?.exitCode == null) return { category: 'PROCESS_CRASH', source: 'process', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
  if (run?.exitCode != null && run.exitCode !== 0) return { category: 'NONZERO_EXIT', source: 'process', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
  if (run?.exitCode === 0) return { category: 'SUCCESS', source: 'process', retryability: 'NOT_APPLICABLE', userActionRequired: 'NO' };
  return { category: 'UNKNOWN_FAILURE', source: 'unknown', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' };
}

export async function captureProcess(executable, args, options = {}) {
  const started = Date.now();
  const launch = resolveSpawn(executable, args);
  if (launch.wrapperError) return { runtimeFound: true, executable, args: redact(args), exitCode: null, signal: null, launchError: launch.wrapperError, stdout: '', stderr: '', rawFrames: [], lineEvents: [], classification: 'LAUNCH_ERROR', failure: { category: 'UNKNOWN_FAILURE', source: 'launch', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' } };
  let child;
  try {
    child = spawn(launch.file, launch.args, { cwd: options.cwd, env: options.env ?? process.env, windowsHide: true, shell: false, detached: process.platform !== 'win32', stdio: ['ignore', 'pipe', 'pipe'] });
  } catch (error) {
    return { runtimeFound: true, executable, args: redact(args), exitCode: null, signal: null, launchError: redactString(String(error)), stdout: '', stderr: '', rawFrames: [], lineEvents: [], classification: 'LAUNCH_ERROR', failure: { category: 'UNKNOWN_FAILURE', source: 'launch', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' } };
  }
  const decoders = { stdout: new StringDecoder('utf8'), stderr: new StringDecoder('utf8') };
  const rawFramesInternal = [];
  let globalSequence = 0;
  let totalChars = 0;
  let outputTruncated = false;
  const record = (stream, buffer, decoderFlush = false) => {
    const decodedText = decoderFlush ? String(buffer ?? '') : decoders[stream].write(buffer);
    const bytes = decoderFlush ? Buffer.alloc(0) : Buffer.from(buffer);
    if (totalChars + decodedText.length > MAX_STREAM_CHARS) { outputTruncated = true; return; }
    totalChars += decodedText.length;
    rawFramesInternal.push({ sequence: ++globalSequence, stream, atMs: Date.now() - started, byteLength: bytes.length, sha256: createHash('sha256').update(bytes).digest('hex'), decodedText, decoderFlush });
  };
  child.stdout.on('data', (chunk) => record('stdout', chunk));
  child.stderr.on('data', (chunk) => record('stderr', chunk));
  const exitPromise = new Promise((resolve) => {
    child.once('error', (error) => resolve({ exitCode: null, signal: null, launchError: String(error) }));
    child.once('exit', (exitCode, signal) => resolve({ exitCode, signal, launchError: null }));
  });
  await new Promise((resolve) => setTimeout(resolve, options.snapshotDelayMs ?? 150));
  const observedProcessByPid = new Map();
  observedProcessByPid.set(child.pid, { pid: child.pid, ppid: null, name: path.basename(launch.file), role: launch.wrapper ? 'launcher-wrapper' : 'launcher' });
  const recordOwnedSnapshot = (snapshot) => {
    for (const row of descendantsOf(snapshot, child.pid)) {
      if (!observedProcessByPid.has(row.pid)) observedProcessByPid.set(row.pid, { ...row, role: 'descendant' });
    }
  };
  recordOwnedSnapshot(processSnapshot());
  let timedOut = false;
  let cancelled = false;
  let gracefulInterruptAttempted = false;
  let forcedTerminationAttempted = false;
  let forcedTerminationSucceeded = false;
  const timeoutTimer = options.timeoutMs != null ? setTimeout(() => { timedOut = true; }, options.timeoutMs) : null;
  const cancelTimer = options.cancelAfterMs != null ? setTimeout(() => { cancelled = true; }, options.cancelAfterMs) : null;
  let escalationRunning = false;
  const watcher = setInterval(async () => {
    if ((timedOut || cancelled) && !escalationRunning && child.exitCode == null) {
      escalationRunning = true;
      recordOwnedSnapshot(processSnapshot());
      gracefulInterruptAttempted = true;
      try { child.kill('SIGINT'); } catch {}
      await new Promise((r) => setTimeout(r, options.gracefulWaitMs ?? 700));
      if (child.exitCode == null) {
        forcedTerminationAttempted = true;
        if (process.platform === 'win32') {
          const killed = runSync('taskkill.exe', ['/PID', String(child.pid), '/T', '/F'], { timeoutMs: 7000 });
          forcedTerminationSucceeded = killed.exitCode === 0;
        } else {
          try { process.kill(-child.pid, 'SIGKILL'); forcedTerminationSucceeded = true; }
          catch { try { child.kill('SIGKILL'); forcedTerminationSucceeded = true; } catch {} }
        }
      }
    }
  }, 50);
  const exit = await exitPromise;
  clearInterval(watcher);
  if (timeoutTimer) clearTimeout(timeoutTimer);
  if (cancelTimer) clearTimeout(cancelTimer);
  const stdoutTail = decoders.stdout.end();
  const stderrTail = decoders.stderr.end();
  if (stdoutTail) record('stdout', stdoutTail, true);
  if (stderrTail) record('stderr', stderrTail, true);
  const stdoutRaw = rawFramesInternal.filter((x) => x.stream === 'stdout').map((x) => x.decodedText).join('');
  const stderrRaw = rawFramesInternal.filter((x) => x.stream === 'stderr').map((x) => x.decodedText).join('');
  const lineEvents = buildLineAnalysis(rawFramesInternal.map((x) => ({ ...x })));
  const rawFrames = finalizeFrames(rawFramesInternal);
  const observedProcessTree = [...observedProcessByPid.values()];
  const observedPids = new Set(observedProcessTree.map((x) => x.pid));
  let finalSnapshot = processSnapshot();
  let remainingOwnedProcesses = (finalSnapshot.processes ?? []).filter((x) => observedPids.has(x.pid));
  const cleanupDeadline = Date.now() + (options.cleanupWaitMs ?? 1800);
  while (remainingOwnedProcesses.length && Date.now() < cleanupDeadline) {
    await new Promise((resolve) => setTimeout(resolve, 100));
    finalSnapshot = processSnapshot();
    remainingOwnedProcesses = (finalSnapshot.processes ?? []).filter((x) => observedPids.has(x.pid));
  }
  const sessionCandidates = [];
  for (const event of lineEvents) sessionCandidates.push(...(event.sessionCandidates ?? []));
  sessionCandidates.push(...extractSessionCandidatesFromText(`${stdoutRaw}\n${stderrRaw}`));
  const dedupedSessionCandidates = [];
  const sessionSeen = new Set();
  for (const item of sessionCandidates) {
    const key = `${item.value}|${item.path ?? ''}`;
    if (!sessionSeen.has(key)) { sessionSeen.add(key); dedupedSessionCandidates.push(item); }
  }
  const result = {
    runtimeFound: true, executable, args: redact(args), wrapper: launch.wrapper, pid: child.pid, cwd: options.cwd ?? process.cwd(),
    exitCode: exit.exitCode, signal: exit.signal, launchError: exit.launchError ? redactString(exit.launchError) : null,
    durationMs: Date.now() - started, timedOut, cancelled, gracefulInterruptAttempted, forcedTerminationAttempted, forcedTerminationSucceeded,
    observedProcessTree, remainingOwnedProcesses, processTreeCleanupObserved: remainingOwnedProcesses.length === 0,
    outputTruncated, stdout: redactString(stdoutRaw), stderr: redactString(stderrRaw), rawFrames, lineEvents, sessionCandidates: dedupedSessionCandidates,
  };
  result.failure = classifyFailure(result);
  result.classification = result.failure.category;
  return redact(result);
}

export async function captureMissingRuntime(executable) {
  return {
    runtimeFound: false, executable: path.resolve(executable), exitCode: null, signal: null, stdout: '', stderr: '', rawFrames: [], lineEvents: [], sessionCandidates: [],
    classification: 'RUNTIME_NOT_FOUND', failure: { category: 'RUNTIME_NOT_FOUND', source: 'launch', retryability: 'UNKNOWN', userActionRequired: 'UNKNOWN' },
  };
}
