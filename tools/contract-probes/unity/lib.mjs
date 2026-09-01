import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { spawn, spawnSync } from 'node:child_process';
import { createHash, randomUUID } from 'node:crypto';

export const EXIT = Object.freeze({ PASS: 0, ERROR: 1, BLOCKED_OR_INCOMPLETE: 2, USAGE: 64 });
export const EVIDENCE = Object.freeze({
  VERIFIED_ON_TARGET: 'VERIFIED_ON_TARGET',
  VERIFIED_ON_AVAILABLE_UNITY_HOST: 'VERIFIED_ON_AVAILABLE_UNITY_HOST',
  SELF_TEST_VERIFIED: 'SELF_TEST_VERIFIED',
  TARGET_UNVERIFIED: 'TARGET_UNVERIFIED',
  NOT_OBSERVED: 'NOT_OBSERVED',
  UNSUPPORTED: 'UNSUPPORTED',
  UNKNOWN: 'UNKNOWN',
});

const SECRET_NAME = /(token|secret|password|passwd|api[_-]?key|authorization|bearer|credential|anthropic|openai|gemini|provider[_-]?key)/i;
const SECRET_VALUE_PATTERNS = [
  /\b(sk-[A-Za-z0-9_-]{8,})\b/g,
  /\b(gh[pousr]_[A-Za-z0-9_]{8,})\b/g,
  /(Bearer\s+)([A-Za-z0-9._~+/=-]{8,})/gi,
  /(Authorization\s*[:=]\s*)([^\s,;]+)/gi,
  /((?:api[_-]?key|token|secret|password|credential)\s*[:=]\s*["']?)([^\s"',;]+)/gi,
];

export function redact(value, key = '') {
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
        return `${args[1] ?? ''}[REDACTED]`;
      }
      return '[REDACTED]';
    });
  }
  return out;
}

export function sanitizePath(input) {
  if (!input || typeof input !== 'string') return input;
  let out = input;
  for (const [needle, replacement] of [[os.homedir(), '<HOME>'], [os.tmpdir(), '<TEMP>']]) {
    if (!needle) continue;
    const normalizedNeedle = path.resolve(needle);
    const escaped = normalizedNeedle.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    out = out.replace(new RegExp(escaped, process.platform === 'win32' ? 'gi' : 'g'), replacement);
  }
  return redact(out);
}

export function sanitizeForPersistence(value, key = '') {
  const redacted = redact(value, key);
  if (typeof redacted === 'string') return sanitizePath(redacted);
  if (Array.isArray(redacted)) return redacted.map((v) => sanitizeForPersistence(v));
  if (redacted && typeof redacted === 'object') {
    const out = {};
    for (const [k, v] of Object.entries(redacted)) out[k] = sanitizeForPersistence(v, k);
    return out;
  }
  return redacted;
}

export function sha256File(filePath) {
  const hash = createHash('sha256');
  const fd = fs.openSync(filePath, 'r');
  try {
    const buf = Buffer.alloc(64 * 1024);
    let bytes = 0;
    while ((bytes = fs.readSync(fd, buf, 0, buf.length, null)) > 0) hash.update(buf.subarray(0, bytes));
  } finally { fs.closeSync(fd); }
  return hash.digest('hex');
}

export function describeArtifact(filePath) {
  try {
    const st = fs.statSync(filePath);
    if (!st.isFile()) return { path: sanitizePath(filePath), exists: true, kind: 'non-file', size: null, sha256: null };
    return { path: sanitizePath(filePath), exists: true, kind: 'file', size: st.size, sha256: sha256File(filePath) };
  } catch {
    return { path: sanitizePath(filePath), exists: false, kind: null, size: null, sha256: null };
  }
}

export function whichAll(name, env = process.env) {
  const dirs = (env.PATH ?? '').split(path.delimiter).filter(Boolean);
  const exts = process.platform === 'win32' ? (env.PATHEXT ?? '.COM;.EXE;.BAT;.CMD').split(';').filter(Boolean) : [''];
  const names = path.extname(name) || process.platform !== 'win32' ? [name] : exts.flatMap((e) => [`${name}${e.toLowerCase()}`, `${name}${e.toUpperCase()}`]);
  const found = [];
  for (const dir of dirs) for (const candidateName of names) {
    const candidate = path.resolve(dir, candidateName);
    try { if (fs.statSync(candidate).isFile()) found.push(path.normalize(candidate)); } catch {}
  }
  return [...new Set(found)];
}

export function runSync(executable, args, options = {}) {
  const started = Date.now();
  try {
    const res = spawnSync(executable, args, {
      cwd: options.cwd,
      encoding: 'utf8',
      timeout: options.timeoutMs ?? 10000,
      windowsHide: true,
      shell: false,
      env: options.env ?? process.env,
      maxBuffer: options.maxBuffer ?? 2 * 1024 * 1024,
    });
    return sanitizeForPersistence({ command: [executable, ...args], exitCode: res.status, signal: res.signal, error: res.error ? { code: res.error.code, message: res.error.message } : null, stdout: (res.stdout ?? '').slice(0, 20000), stderr: (res.stderr ?? '').slice(0, 20000), durationMs: Date.now() - started });
  } catch (error) {
    return sanitizeForPersistence({ command: [executable, ...args], exitCode: null, signal: null, error: { message: String(error) }, stdout: '', stderr: '', durationMs: Date.now() - started });
  }
}

export function parseProjectVersionText(text) {
  const result = { requiredVersion: null, revision: null, rawVersionLine: null, rawRevisionLine: null };
  for (const rawLine of String(text ?? '').split(/\r?\n/)) {
    const line = rawLine.trim();
    if (line.startsWith('m_EditorVersion:')) {
      result.rawVersionLine = line;
      result.requiredVersion = line.slice('m_EditorVersion:'.length).trim() || null;
    } else if (line.startsWith('m_EditorVersionWithRevision:')) {
      result.rawRevisionLine = line;
      const value = line.slice('m_EditorVersionWithRevision:'.length).trim();
      const match = /^(.*?)\s*\(([^()]+)\)\s*$/.exec(value);
      if (match) {
        if (!result.requiredVersion) result.requiredVersion = match[1].trim() || null;
        result.revision = match[2].trim() || null;
      } else if (!result.requiredVersion) result.requiredVersion = value || null;
    }
  }
  return result;
}

export function detectUnityProject(projectRoot) {
  const resolved = path.resolve(projectRoot);
  const markers = { assets: path.join(resolved, 'Assets'), packages: path.join(resolved, 'Packages'), projectSettings: path.join(resolved, 'ProjectSettings'), projectVersion: path.join(resolved, 'ProjectSettings', 'ProjectVersion.txt') };
  const existsDirectory = (p) => { try { return fs.statSync(p).isDirectory(); } catch { return false; } };
  const existsFile = (p) => { try { return fs.statSync(p).isFile(); } catch { return false; } };
  const presence = { root: existsDirectory(resolved), assets: existsDirectory(markers.assets), packages: existsDirectory(markers.packages), projectSettings: existsDirectory(markers.projectSettings), projectVersion: existsFile(markers.projectVersion) };
  let parsed = { requiredVersion: null, revision: null };
  let parseError = null;
  if (presence.projectVersion) {
    try { parsed = parseProjectVersionText(fs.readFileSync(markers.projectVersion, 'utf8')); } catch (error) { parseError = String(error); }
  }
  const valid = presence.root && presence.assets && presence.packages && presence.projectSettings && presence.projectVersion && !!parsed.requiredVersion && !parseError;
  let classification = 'VALID_UNITY_PROJECT';
  if (!presence.root) classification = 'PROJECT_ROOT_NOT_FOUND';
  else if (!presence.projectSettings || !presence.projectVersion) classification = 'NOT_UNITY_PROJECT';
  else if (parseError) classification = 'PROJECT_VERSION_READ_FAILURE';
  else if (!parsed.requiredVersion) classification = 'PROJECT_VERSION_MISSING';
  else if (!presence.assets || !presence.packages) classification = 'UNITY_PROJECT_INCOMPLETE';
  return sanitizeForPersistence({ projectRoot: resolved, valid, classification, requiredVersion: parsed.requiredVersion ?? null, revision: parsed.revision ?? null, markers: presence, parseError });
}

export function parseUnityVersion(version) {
  if (!version) return null;
  const text = String(version).trim();
  const match = /^(\d+)\.(\d+)\.(\d+)([abfp])(\d+)(?:[-+].*)?$/.exec(text);
  if (!match) return { raw: text, valid: false, major: null, minor: null, patch: null, stream: null, build: null };
  return { raw: text, valid: true, major: Number(match[1]), minor: Number(match[2]), patch: Number(match[3]), stream: match[4], build: Number(match[5]) };
}

const STREAM_RANK = Object.freeze({ a: 0, b: 1, f: 2, p: 3 });
export function compareUnityVersions(a, b) {
  const pa = typeof a === 'string' ? parseUnityVersion(a) : a;
  const pb = typeof b === 'string' ? parseUnityVersion(b) : b;
  if (!pa?.valid && !pb?.valid) return String(pa?.raw ?? '').localeCompare(String(pb?.raw ?? ''));
  if (!pa?.valid) return -1;
  if (!pb?.valid) return 1;
  for (const key of ['major', 'minor', 'patch']) if (pa[key] !== pb[key]) return pa[key] - pb[key];
  if (STREAM_RANK[pa.stream] !== STREAM_RANK[pb.stream]) return STREAM_RANK[pa.stream] - STREAM_RANK[pb.stream];
  return pa.build - pb.build;
}

function uniqueExistingFiles(items) {
  const seen = new Set(); const out = [];
  for (const item of items) {
    if (!item?.path) continue;
    const normalized = path.normalize(path.resolve(item.path));
    if (seen.has(normalized.toLowerCase())) continue;
    try { if (!fs.statSync(normalized).isFile()) continue; } catch { continue; }
    seen.add(normalized.toLowerCase()); out.push({ ...item, path: normalized });
  }
  return out;
}

function readPeArchitecture(executablePath) {
  if (process.platform !== 'win32') return 'UNKNOWN_NON_WINDOWS_PROBE_HOST';
  try {
    const fd = fs.openSync(executablePath, 'r');
    try {
      const dos = Buffer.alloc(64); fs.readSync(fd, dos, 0, dos.length, 0);
      if (dos.toString('ascii', 0, 2) !== 'MZ') return 'UNKNOWN';
      const peOffset = dos.readUInt32LE(0x3c); const header = Buffer.alloc(6); fs.readSync(fd, header, 0, header.length, peOffset);
      if (header.toString('ascii', 0, 4) !== 'PE\0\0') return 'UNKNOWN';
      const machine = header.readUInt16LE(4);
      if (machine === 0x8664) return 'x64'; if (machine === 0x014c) return 'x86'; if (machine === 0xaa64) return 'arm64';
      return `PE_MACHINE_0x${machine.toString(16)}`;
    } finally { fs.closeSync(fd); }
  } catch { return 'UNKNOWN'; }
}

export function discoverUnityHub({ explicitHub = null, env = process.env } = {}) {
  const candidates = [];
  if (explicitHub) candidates.push({ path: explicitHub, source: 'explicit' });
  if (env.UNITY_HUB_PATH) candidates.push({ path: env.UNITY_HUB_PATH, source: 'env:UNITY_HUB_PATH' });
  for (const p of whichAll(process.platform === 'win32' ? 'Unity Hub.exe' : 'unityhub', env)) candidates.push({ path: p, source: 'PATH' });
  if (process.platform === 'win32') for (const root of [env.ProgramFiles, env.LOCALAPPDATA].filter(Boolean)) candidates.push({ path: path.join(root, root === env.LOCALAPPDATA ? 'Programs' : '', 'Unity Hub', 'Unity Hub.exe'), source: 'standard-location' });
  const found = uniqueExistingFiles(candidates);
  return sanitizeForPersistence({ found: found.length > 0, candidates: found });
}

function editorCandidatesFromRoot(root, source) {
  const out = []; if (!root) return out;
  try {
    for (const entry of fs.readdirSync(root, { withFileTypes: true })) if (entry.isDirectory()) out.push({ path: path.join(root, entry.name, 'Editor', process.platform === 'win32' ? 'Unity.exe' : 'Unity'), source, versionHint: entry.name });
  } catch {}
  return out;
}

export function discoverUnityEditors({ explicitEditor = null, env = process.env, probeVersion = true } = {}) {
  const candidates = [];
  if (explicitEditor) candidates.push({ path: explicitEditor, source: 'explicit', versionHint: null });
  for (const name of ['UNITY_EDITOR_PATH', 'UNITY_PATH']) if (env[name]) candidates.push({ path: env[name], source: `env:${name}`, versionHint: null });
  for (const p of whichAll(process.platform === 'win32' ? 'Unity.exe' : 'Unity', env)) candidates.push({ path: p, source: 'PATH', versionHint: null });
  if (process.platform === 'win32') {
    for (const root of [env.ProgramFiles, env['ProgramFiles(x86)']].filter(Boolean)) {
      candidates.push(...editorCandidatesFromRoot(path.join(root, 'Unity', 'Hub', 'Editor'), 'hub-default-root'));
      candidates.push({ path: path.join(root, 'Unity', 'Editor', 'Unity.exe'), source: 'legacy-standard-location', versionHint: null });
      try { for (const entry of fs.readdirSync(root, { withFileTypes: true })) if (entry.isDirectory() && /^Unity/i.test(entry.name)) candidates.push({ path: path.join(root, entry.name, 'Editor', 'Unity.exe'), source: 'program-files-scan', versionHint: entry.name.replace(/^Unity\s*/i, '') || null }); } catch {}
    }
  }
  const found = uniqueExistingFiles(candidates).map((item) => {
    let version = item.versionHint && parseUnityVersion(item.versionHint)?.valid ? item.versionHint : null;
    let versionProbe = null;
    if (probeVersion) {
      versionProbe = runSync(item.path, ['-version'], { timeoutMs: 15000 });
      const match = /\b\d+\.\d+\.\d+[abfp]\d+\b/.exec(`${versionProbe.stdout ?? ''}\n${versionProbe.stderr ?? ''}`);
      if (match) version = match[0];
    }
    return { ...item, version, parsedVersion: parseUnityVersion(version), architecture: readPeArchitecture(item.path), versionProbe };
  });
  found.sort((a, b) => -compareUnityVersions(a.version ?? '', b.version ?? ''));
  return sanitizeForPersistence({ found: found.length > 0, editors: found });
}

export function resolveEditorForProject(project, editors) {
  const installed = [...(editors ?? [])].filter((e) => e?.path && e?.version); installed.sort((a, b) => -compareUnityVersions(a.version, b.version));
  const required = project?.requiredVersion ?? null; const exact = required ? installed.find((e) => e.version === required) ?? null : null; const others = installed.filter((e) => !exact || e.path !== exact.path);
  let compatibilityStatus;
  if (!project?.valid) compatibilityStatus = 'PROJECT_INVALID'; else if (!required) compatibilityStatus = 'PROJECT_VERSION_UNKNOWN'; else if (exact) compatibilityStatus = 'EXACT_MATCH'; else if (!installed.length) compatibilityStatus = 'UNITY_NOT_INSTALLED'; else compatibilityStatus = 'REQUIRED_VERSION_NOT_INSTALLED';
  return sanitizeForPersistence({ projectRequiredVersion: required, installedMatchingVersion: exact?.version ?? null, installedOtherVersions: others.map((e) => e.version), selectedEditor: exact ?? null, compatibilityStatus });
}

function ensureString(v, name) { if (typeof v !== 'string' || !v.trim()) throw new Error(`${name} is required`); return v; }
export function buildCreateProjectArgs({ projectPath, logPath }) { ensureString(projectPath, 'projectPath'); ensureString(logPath, 'logPath'); return ['-quit', '-batchmode', '-nographics', '-createProject', projectPath, '-logFile', logPath, '-timestamps']; }
export function buildExecuteMethodArgs({ projectPath, logPath, method, extraArgs = [], quit = true, nographics = true }) {
  ensureString(projectPath, 'projectPath'); ensureString(logPath, 'logPath'); ensureString(method, 'method'); if (!Array.isArray(extraArgs) || extraArgs.some((v) => typeof v !== 'string')) throw new Error('extraArgs must be a string array');
  const args = []; if (quit) args.push('-quit'); args.push('-batchmode'); if (nographics) args.push('-nographics'); args.push('-projectPath', projectPath, '-logFile', logPath, '-timestamps', '-executeMethod', method, ...extraArgs); return args;
}
export function buildTestArgs({ projectPath, logPath, resultsPath, platform }) { ensureString(projectPath, 'projectPath'); ensureString(logPath, 'logPath'); ensureString(resultsPath, 'resultsPath'); if (!['EditMode', 'PlayMode'].includes(platform)) throw new Error('platform must be EditMode or PlayMode'); return ['-batchmode', '-nographics', '-projectPath', projectPath, '-logFile', logPath, '-timestamps', '-runTests', '-testPlatform', platform, '-testResults', resultsPath]; }
export function buildBuildArgs({ projectPath, logPath, buildOutputPath, buildResultPath }) { return buildExecuteMethodArgs({ projectPath, logPath, method: 'FccUnityProbe.BuildWindows64', extraArgs: ['-buildTarget', 'StandaloneWindows64', '--fcc-build-output', buildOutputPath, '--fcc-result', ensureString(buildResultPath, 'buildResultPath')], quit: true, nographics: true }); }

function terminateOwnedProcessTree(child, pid) {
  const result = { pid, gracefulAttempted: false, forcedTreeAttempted: false, errors: [] }; if (!pid) return result;
  try { result.gracefulAttempted = child.kill('SIGTERM'); } catch (error) { result.errors.push(`graceful:${String(error)}`); } return result;
}
function forceKillOwnedTree(pid) {
  if (!pid) return { attempted: false, error: null };
  try {
    if (process.platform === 'win32') { const res = spawnSync('taskkill.exe', ['/PID', String(pid), '/T', '/F'], { windowsHide: true, shell: false, encoding: 'utf8', timeout: 10000 }); return { attempted: true, exitCode: res.status, error: res.error ? String(res.error) : null }; }
    try { process.kill(-pid, 'SIGKILL'); return { attempted: true, signal: 'SIGKILL_PROCESS_GROUP', error: null }; } catch { try { process.kill(pid, 'SIGKILL'); return { attempted: true, signal: 'SIGKILL_ROOT', error: null }; } catch (error) { return { attempted: true, error: String(error) }; } }
  } catch (error) { return { attempted: true, error: String(error) }; }
}

export function runOwnedProcess(executable, args, options = {}) {
  return new Promise((resolve) => {
    const operationId = options.operationId ?? randomUUID(); const startedAt = new Date(); const startedMs = Date.now();
    let child, settled = false, timedOut = false, cancelled = false, forceResult = null, gracefulResult = null;
    const stdout = [], stderr = []; const cap = options.captureLimit ?? 2 * 1024 * 1024; let stdoutBytes = 0, stderrBytes = 0;
    const append = (arr, chunk, which) => { const text = String(chunk); if (which === 'out') { const remain = Math.max(0, cap - stdoutBytes); if (remain > 0) arr.push(text.slice(0, remain)); stdoutBytes += Buffer.byteLength(text); } else { const remain = Math.max(0, cap - stderrBytes); if (remain > 0) arr.push(text.slice(0, remain)); stderrBytes += Buffer.byteLength(text); } };
    try { child = spawn(executable, args, { cwd: options.cwd, env: options.env ?? process.env, shell: false, windowsHide: true, detached: process.platform !== 'win32', stdio: ['ignore', 'pipe', 'pipe'] }); }
    catch (error) { resolve(sanitizeForPersistence({ operationId, executable, args, pid: null, startTime: startedAt.toISOString(), endTime: new Date().toISOString(), durationMs: Date.now() - startedMs, exitCode: null, signal: null, timedOut: false, cancelled: false, launchError: String(error), stdout: '', stderr: '', processCleanup: null })); return; }
    child.stdout?.on('data', (c) => append(stdout, c, 'out')); child.stderr?.on('data', (c) => append(stderr, c, 'err'));
    let timeoutTimer = null, cancelTimer = null, forceTimer = null;
    const requestStop = (reason) => { if (settled) return; if (reason === 'timeout') timedOut = true; if (reason === 'cancel') cancelled = true; gracefulResult = terminateOwnedProcessTree(child, child.pid); forceTimer = setTimeout(() => { if (!settled) forceResult = forceKillOwnedTree(child.pid); }, options.forceAfterMs ?? 2000); };
    if (Number.isFinite(options.timeoutMs) && options.timeoutMs > 0) timeoutTimer = setTimeout(() => requestStop('timeout'), options.timeoutMs);
    if (Number.isFinite(options.cancelAfterMs) && options.cancelAfterMs > 0) cancelTimer = setTimeout(() => requestStop('cancel'), options.cancelAfterMs);
    child.on('error', (error) => { if (settled) return; settled = true; clearTimeout(timeoutTimer); clearTimeout(cancelTimer); clearTimeout(forceTimer); resolve(sanitizeForPersistence({ operationId, executable, args, pid: child.pid ?? null, startTime: startedAt.toISOString(), endTime: new Date().toISOString(), durationMs: Date.now() - startedMs, exitCode: null, signal: null, timedOut, cancelled, launchError: String(error), stdout: stdout.join(''), stderr: stderr.join(''), processCleanup: { gracefulResult, forceResult } })); });
    child.on('close', (code, signal) => { if (settled) return; settled = true; clearTimeout(timeoutTimer); clearTimeout(cancelTimer); clearTimeout(forceTimer); resolve(sanitizeForPersistence({ operationId, executable, args, pid: child.pid ?? null, startTime: startedAt.toISOString(), endTime: new Date().toISOString(), durationMs: Date.now() - startedMs, exitCode: code, signal, timedOut, cancelled, launchError: null, stdout: stdout.join(''), stderr: stderr.join(''), processCleanup: { gracefulResult, forceResult } })); });
  });
}

export function parseUnityLogText(text) {
  const rawLines = String(text ?? '').split(/\r?\n/); const events = []; const categories = { compilerError: 0, compilerWarning: 0, projectLock: 0, exception: 0, buildFailure: 0, testFailure: 0, unknown: 0 };
  rawLines.forEach((raw, index) => { if (!raw) return; let kind = 'UNKNOWN'; if (/\berror\s+CS\d+\b/i.test(raw)) kind = 'COMPILER_ERROR'; else if (/\bwarning\s+CS\d+\b/i.test(raw)) kind = 'COMPILER_WARNING'; else if (/already.*open|project.*open.*another|another.*Unity.*project|project.*locked/i.test(raw)) kind = 'PROJECT_LOCK'; else if (/\b(Exception|NullReferenceException|InvalidOperationException|StackTrace)\b/i.test(raw)) kind = 'EXCEPTION'; else if (/BuildFailed|Build.*Failed|build result.*fail/i.test(raw)) kind = 'BUILD_FAILURE'; else if (/test.*fail|Failed tests?:/i.test(raw)) kind = 'TEST_FAILURE'; if (kind === 'UNKNOWN') categories.unknown++; else if (kind === 'COMPILER_ERROR') categories.compilerError++; else if (kind === 'COMPILER_WARNING') categories.compilerWarning++; else if (kind === 'PROJECT_LOCK') categories.projectLock++; else if (kind === 'EXCEPTION') categories.exception++; else if (kind === 'BUILD_FAILURE') categories.buildFailure++; else if (kind === 'TEST_FAILURE') categories.testFailure++; events.push({ index, kind, line: redact(raw) }); });
  return sanitizeForPersistence({ lineCount: rawLines.length, categories, events });
}
export function readUnityLog(logPath, processResult = null) { let text = '', readError = null; try { text = fs.readFileSync(logPath, 'utf8'); } catch (error) { readError = String(error); } return sanitizeForPersistence({ logPath, logExists: fs.existsSync(logPath), readError, parsed: parseUnityLogText(`${text}\n${processResult?.stdout ?? ''}\n${processResult?.stderr ?? ''}`) }); }

function parseXmlTagAttributes(tagText) {
  const attrs = {}; let i = tagText.indexOf(' '); if (i < 0) return attrs;
  while (i < tagText.length) { while (/\s/.test(tagText[i] ?? '')) i++; if (i >= tagText.length || tagText[i] === '>' || tagText[i] === '/') break; let name = ''; while (i < tagText.length && /[^\s=/>]/.test(tagText[i])) name += tagText[i++]; while (/\s/.test(tagText[i] ?? '')) i++; if (tagText[i] !== '=') { attrs[name] = ''; continue; } i++; while (/\s/.test(tagText[i] ?? '')) i++; const quote = tagText[i]; if (quote !== '"' && quote !== "'") { attrs[name] = ''; continue; } i++; let value = ''; while (i < tagText.length && tagText[i] !== quote) value += tagText[i++]; if (tagText[i] === quote) i++; attrs[name] = value; }
  return attrs;
}
function firstElementTag(xml) {
  let i = 0;
  while (i < xml.length) { const open = xml.indexOf('<', i); if (open < 0) return null; if (xml.startsWith('<?', open)) { const close = xml.indexOf('?>', open + 2); if (close < 0) return null; i = close + 2; continue; } if (xml.startsWith('<!--', open)) { const close = xml.indexOf('-->', open + 4); if (close < 0) return null; i = close + 3; continue; } if (xml.startsWith('<!', open)) { const close = xml.indexOf('>', open + 2); if (close < 0) return null; i = close + 1; continue; } const close = xml.indexOf('>', open + 1); if (close < 0) return null; const tag = xml.slice(open + 1, close).trim(); if (!tag || tag.startsWith('/')) { i = close + 1; continue; } const name = tag.split(/\s|\//, 1)[0]; return { name, attrs: parseXmlTagAttributes(tag), raw: tag }; }
  return null;
}
export function validateTestResultXml(filePath, { requireTests = true } = {}) {
  if (!fs.existsSync(filePath)) return sanitizeForPersistence({ valid: false, classification: 'TEST_RESULTS_MISSING', path: filePath, counts: null }); const st = fs.statSync(filePath); if (!st.isFile() || st.size === 0) return sanitizeForPersistence({ valid: false, classification: 'TEST_RESULTS_EMPTY', path: filePath, counts: null });
  let xml; try { xml = fs.readFileSync(filePath, 'utf8'); } catch (error) { return sanitizeForPersistence({ valid: false, classification: 'TEST_RESULTS_READ_FAILURE', path: filePath, error: String(error), counts: null }); }
  const root = firstElementTag(xml); if (!root || !['test-run', 'test-suite'].includes(root.name)) return sanitizeForPersistence({ valid: false, classification: 'TEST_RESULTS_UNEXPECTED_XML', path: filePath, root: root?.name ?? null, counts: null });
  const intAttr = (name) => { const v = root.attrs[name]; if (v == null || v === '') return null; const n = Number(v); return Number.isFinite(n) ? n : null; };
  const counts = { total: intAttr('total') ?? intAttr('testcasecount'), passed: intAttr('passed'), failed: intAttr('failed'), skipped: intAttr('skipped'), inconclusive: intAttr('inconclusive') }; const result = root.attrs.result ?? null;
  if (counts.total == null) return sanitizeForPersistence({ valid: false, classification: 'TEST_RESULTS_COUNT_MISSING', path: filePath, root: root.name, result, counts });
  if (requireTests && counts.total <= 0) return sanitizeForPersistence({ valid: false, classification: 'ZERO_TESTS', path: filePath, root: root.name, result, counts });
  if ((counts.failed ?? 0) > 0 || /failed/i.test(result ?? '')) return sanitizeForPersistence({ valid: false, classification: 'TEST_FAILURE', path: filePath, root: root.name, result, counts });
  return sanitizeForPersistence({ valid: true, classification: 'TEST_PASS', path: filePath, root: root.name, result, counts, artifact: describeArtifact(filePath) });
}
export function validateJsonResult(filePath, predicate = null, missingClassification = 'RESULT_MISSING') {
  if (!fs.existsSync(filePath)) return sanitizeForPersistence({ valid: false, classification: missingClassification, path: filePath }); const st = fs.statSync(filePath); if (!st.isFile() || st.size === 0) return sanitizeForPersistence({ valid: false, classification: 'RESULT_EMPTY', path: filePath });
  try { const value = JSON.parse(fs.readFileSync(filePath, 'utf8')); if (predicate && !predicate(value)) return sanitizeForPersistence({ valid: false, classification: 'RESULT_CONTENT_INVALID', path: filePath, value: redact(value), artifact: describeArtifact(filePath) }); return sanitizeForPersistence({ valid: true, classification: 'RESULT_VALID', path: filePath, value: redact(value), artifact: describeArtifact(filePath) }); } catch (error) { return sanitizeForPersistence({ valid: false, classification: 'RESULT_JSON_INVALID', path: filePath, error: String(error), artifact: describeArtifact(filePath) }); }
}
export function validateBuildArtifacts({ buildResultPath, expectedExecutablePath }) { const buildResult = validateJsonResult(buildResultPath, (v) => v && v.success === true && /succeeded/i.test(String(v.result ?? ''))); const exe = describeArtifact(expectedExecutablePath); const valid = buildResult.valid && exe.exists && exe.kind === 'file' && exe.size > 0; return sanitizeForPersistence({ valid, classification: valid ? 'BUILD_PASS' : 'BUILD_ARTIFACT_VALIDATION_FAILURE', buildResult, executable: exe }); }
export function classifyCompile({ processResult, logResult, resultArtifact }) { if (processResult?.timedOut) return 'TIMEOUT'; if (processResult?.cancelled) return 'CANCELLED'; if (processResult?.launchError) return 'UNITY_STARTUP_FAILURE'; const cats = logResult?.parsed?.categories ?? {}; if ((cats.projectLock ?? 0) > 0) return 'PROJECT_OPEN_FAILURE'; if ((cats.compilerError ?? 0) > 0) return 'COMPILE_ERROR'; if (resultArtifact?.valid) return 'COMPILE_PASS'; if (processResult?.exitCode !== 0) return 'UNITY_STARTUP_FAILURE'; return 'UNKNOWN_FAILURE'; }

export function writeDisposableFixtureSources(projectRoot) {
  const editorDir = path.join(projectRoot, 'Assets', 'Editor'); const editModeDir = path.join(projectRoot, 'Assets', 'Tests', 'EditMode'); const playModeDir = path.join(projectRoot, 'Assets', 'Tests', 'PlayMode'); fs.mkdirSync(editorDir, { recursive: true }); fs.mkdirSync(editModeDir, { recursive: true }); fs.mkdirSync(playModeDir, { recursive: true });
  const manifestPath = path.join(projectRoot, 'Packages', 'manifest.json');
  const manifest = fs.existsSync(manifestPath) ? JSON.parse(fs.readFileSync(manifestPath, 'utf8')) : { dependencies: {} };
  manifest.dependencies ??= {};
  // Unity 6 blank projects omit the test framework. Use the minimum version
  // declared by the tested editor so generated NUnit assemblies can compile.
  manifest.dependencies['com.unity.test-framework'] ??= '1.7.0';
  fs.mkdirSync(path.dirname(manifestPath), { recursive: true });
  fs.writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
  const editorScript = `using System;\nusing System.IO;\nusing System.Threading;\nusing UnityEditor;\nusing UnityEditor.Build.Reporting;\nusing UnityEditor.SceneManagement;\nusing UnityEngine;\nusing UnityEngine.SceneManagement;\n\npublic static class FccUnityProbe\n{\n    static string Arg(string name)\n    {\n        var args = Environment.GetCommandLineArgs();\n        for (var i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];\n        return null;\n    }\n\n    static void WriteJson(string path, string json)\n    {\n        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("result path missing");\n        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));\n        File.WriteAllText(path, json);\n    }\n\n    public static void WriteCompileResult()\n    {\n        var path = Arg("--fcc-result");\n        WriteJson(path, "{\\\"success\\\":true,\\\"operation\\\":\\\"compile-marker\\\",\\\"unityVersion\\\":\\\"" + Application.unityVersion + "\\\"}");\n    }\n\n    public static void AutomationMarker()\n    {\n        var path = Arg("--fcc-result");\n        var product = PlayerSettings.productName.Replace("\\\"", "'");\n        WriteJson(path, "{\\\"success\\\":true,\\\"operation\\\":\\\"automation-marker\\\",\\\"unityVersion\\\":\\\"" + Application.unityVersion + "\\\",\\\"productName\\\":\\\"" + product + "\\\"}");\n    }\n\n    public static void ThrowExpectedFailure()\n    {\n        throw new InvalidOperationException("FCCD_EXPECTED_EXECUTE_METHOD_FAILURE");\n    }\n\n    public static void HoldOpen()\n    {\n        var ready = Arg("--fcc-result");\n        var hold = Arg("--fcc-hold-ms");\n        var ms = string.IsNullOrWhiteSpace(hold) ? 60000 : int.Parse(hold);\n        WriteJson(ready, "{\\\"success\\\":true,\\\"operation\\\":\\\"hold-ready\\\",\\\"pid\\\":" + System.Diagnostics.Process.GetCurrentProcess().Id + "}");\n        Thread.Sleep(ms);\n    }\n\n    public static void BuildWindows64()\n    {\n        var output = Arg("--fcc-build-output");\n        var resultPath = Arg("--fcc-result");\n        if (string.IsNullOrWhiteSpace(output)) throw new ArgumentException("--fcc-build-output missing");\n        var scenePath = "Assets/FccProbeScene.unity";\n        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);\n        EditorSceneManager.SaveScene(scene, scenePath);\n        var options = new BuildPlayerOptions { scenes = new[] { scenePath }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None };\n        var report = BuildPipeline.BuildPlayer(options);\n        var ok = report.summary.result == BuildResult.Succeeded;\n        var json = "{\\\"success\\\":" + (ok ? "true" : "false") + ",\\\"result\\\":\\\"" + report.summary.result + "\\\",\\\"totalErrors\\\":" + report.summary.totalErrors + ",\\\"totalWarnings\\\":" + report.summary.totalWarnings + ",\\\"totalSize\\\":" + report.summary.totalSize + ",\\\"outputPath\\\":\\\"" + output.Replace("\\\\", "\\\\\\\\").Replace("\\\"", "'") + "\\\"}";\n        WriteJson(resultPath, json);\n        if (!ok) EditorApplication.Exit(31);\n    }\n}\n`;
  fs.writeFileSync(path.join(editorDir, 'FccUnityProbe.cs'), editorScript, 'utf8');
  const asmdef = (name, includePlatforms = []) => JSON.stringify({ name, references: [], optionalUnityReferences: ['TestAssemblies'], includePlatforms, excludePlatforms: [], allowUnsafeCode: false, overrideReferences: false, precompiledReferences: [], autoReferenced: true, defineConstraints: [], versionDefines: [], noEngineReferences: false }, null, 2) + '\n';
  fs.writeFileSync(path.join(editModeDir, 'FccProbe.EditMode.asmdef'), asmdef('FccProbe.EditMode', ['Editor']), 'utf8');
  fs.writeFileSync(path.join(editModeDir, 'FccProbeEditModeTests.cs'), `using NUnit.Framework;\npublic class FccProbeEditModeTests { [Test] public void DeterministicEditModePass() { Assert.AreEqual(4, 2 + 2); } }\n`, 'utf8');
  fs.writeFileSync(path.join(playModeDir, 'FccProbe.PlayMode.asmdef'), asmdef('FccProbe.PlayMode'), 'utf8');
  fs.writeFileSync(path.join(playModeDir, 'FccProbePlayModeTests.cs'), `using System.Collections;\nusing NUnit.Framework;\nusing UnityEngine.TestTools;\npublic class FccProbePlayModeTests { [UnityTest] public IEnumerator DeterministicPlayModePass() { yield return null; Assert.IsTrue(true); } }\n`, 'utf8');
  return [path.join(editorDir, 'FccUnityProbe.cs'), path.join(editModeDir, 'FccProbe.EditMode.asmdef'), path.join(editModeDir, 'FccProbeEditModeTests.cs'), path.join(playModeDir, 'FccProbe.PlayMode.asmdef'), path.join(playModeDir, 'FccProbePlayModeTests.cs')].map(describeArtifact);
}
export function makeDisposableFixtureRoot(baseRoot = os.tmpdir()) { const root = path.join(baseRoot, `fcc unity probe مسار ${randomUUID()}`); fs.mkdirSync(root, { recursive: true }); return root; }
export function waitForFile(filePath, timeoutMs = 60000, pollMs = 200) { return new Promise((resolve) => { const started = Date.now(); const tick = () => { if (fs.existsSync(filePath)) { resolve(true); return; } if (Date.now() - started >= timeoutMs) { resolve(false); return; } setTimeout(tick, pollMs); }; tick(); }); }
export function operationRecord({ id, unityVersion, executablePath, projectPath, args, processResult, logPath, testResultPath = null, buildOutputPath = null, artifacts = [], classification, evidenceState }) {
  return sanitizeForPersistence({ operationId: id, unityVersion, executablePath, projectPath, args, startTime: processResult?.startTime ?? null, endTime: processResult?.endTime ?? null, durationMs: processResult?.durationMs ?? null, pid: processResult?.pid ?? null, exitCode: processResult?.exitCode ?? null, timeout: !!processResult?.timedOut, cancellation: !!processResult?.cancelled, logPath, testResultPath, buildOutputPath, producedArtifacts: artifacts, processCleanup: processResult?.processCleanup ?? null, finalClassification: classification, evidenceState });
}
