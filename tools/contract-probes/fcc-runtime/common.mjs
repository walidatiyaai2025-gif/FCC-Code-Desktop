import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { spawnSync } from 'node:child_process';

const SECRET_NAME = /(token|secret|password|passwd|api[_-]?key|authorization|bearer|credential|cookie|anthropic|openai|gemini|provider[_-]?key)/i;
const SIMPLE_SECRET_PATTERNS = [
  /\bsk-[A-Za-z0-9_-]{8,}\b/g,
  /\bgh[pousr]_[A-Za-z0-9_]{8,}\b/g,
];
const PREFIXED_SECRET_PATTERNS = [
  /(Authorization\s*[:=]\s*)([^\r\n,;]+)/gi,
  /(Bearer\s+)([A-Za-z0-9._~+/=-]{8,})/gi,
  /((?:api[_-]?key|token|secret|password|credential|cookie|set-cookie)\s*[:=]\s*["']?)([^\s"',;]+)/gi,
];

export function redactString(value) {
  if (typeof value !== 'string') return value;
  let out = value;
  for (const pattern of SIMPLE_SECRET_PATTERNS) out = out.replace(pattern, '[REDACTED]');
  for (const pattern of PREFIXED_SECRET_PATTERNS) out = out.replace(pattern, (_full, prefix) => `${prefix}[REDACTED]`);
  return out;
}

export function redact(value, key = '') {
  if (value == null) return value;
  if (SECRET_NAME.test(key)) return '[REDACTED]';
  if (Array.isArray(value)) return value.map((v) => redact(v));
  if (typeof value === 'object') {
    const out = {};
    for (const [k, v] of Object.entries(value)) out[k] = redact(v, k);
    return out;
  }
  return typeof value === 'string' ? redactString(value) : value;
}

export function maskSecretsPreserveLength(value) {
  let out = String(value ?? '');
  for (const pattern of SIMPLE_SECRET_PATTERNS) out = out.replace(pattern, (full) => '*'.repeat(full.length));
  for (const pattern of PREFIXED_SECRET_PATTERNS) {
    out = out.replace(pattern, (_full, prefix, secret) => `${prefix}${'*'.repeat(secret.length)}`);
  }
  return out;
}

export function whichAll(name) {
  const dirs = (process.env.PATH ?? '').split(path.delimiter).filter(Boolean);
  const exts = process.platform === 'win32' ? (process.env.PATHEXT ?? '.COM;.EXE;.BAT;.CMD').split(';').filter(Boolean) : [''];
  const names = path.extname(name) || process.platform !== 'win32'
    ? [name]
    : [...exts.map((e) => `${name}${e.toLowerCase()}`), ...exts.map((e) => `${name}${e.toUpperCase()}`)];
  const found = [];
  for (const dir of dirs) {
    for (const candidateName of names) {
      const candidate = path.resolve(dir, candidateName);
      try { if (fs.statSync(candidate).isFile()) found.push(candidate); } catch {}
    }
  }
  return [...new Set(found.map((p) => path.normalize(p)))];
}

export function resolveSpawn(exe, args) {
  const ext = path.extname(exe).toLowerCase();
  if (process.platform === 'win32' && (ext === '.cmd' || ext === '.bat')) {
    const powershell = whichAll('pwsh')[0] ?? whichAll('powershell')[0];
    if (!powershell) return { file: exe, args, wrapper: null, wrapperError: 'PowerShell required to launch .cmd/.bat safely but was not found.' };
    const script = '$target=$args[0]; $rest=@(); if($args.Count -gt 1){$rest=$args[1..($args.Count-1)]}; & $target @rest; exit $LASTEXITCODE';
    return { file: powershell, args: ['-NoProfile', '-NonInteractive', '-Command', script, exe, ...args], wrapper: 'powershell-call-operator', wrapperError: null };
  }
  return { file: exe, args, wrapper: null, wrapperError: null };
}

export function runSync(exe, args, options = {}) {
  const launch = resolveSpawn(exe, args);
  if (launch.wrapperError) return { exitCode: null, stdout: '', stderr: '', error: launch.wrapperError, wrapper: launch.wrapper };
  try {
    const result = spawnSync(launch.file, launch.args, {
      cwd: options.cwd,
      env: options.env ?? process.env,
      encoding: 'utf8',
      timeout: options.timeoutMs ?? 7000,
      windowsHide: true,
      shell: false,
      maxBuffer: 2 * 1024 * 1024,
    });
    return redact({
      exitCode: result.status,
      signal: result.signal,
      stdout: (result.stdout ?? '').slice(0, 200000),
      stderr: (result.stderr ?? '').slice(0, 200000),
      error: result.error ? { code: result.error.code, message: result.error.message } : null,
      wrapper: launch.wrapper,
    });
  } catch (error) {
    return { exitCode: null, stdout: '', stderr: '', error: redactString(String(error)), wrapper: launch.wrapper };
  }
}

export function probeExecutable(name, explicitPath = null) {
  const candidates = explicitPath ? [path.resolve(explicitPath)] : whichAll(name);
  const paths = candidates.filter((candidate) => {
    try { return fs.statSync(candidate).isFile(); } catch { return false; }
  });
  if (!paths.length) return { name, found: false, paths: [], help: null, version: null };
  const executable = paths[0];
  let version = null;
  for (const args of [['--version'], ['version'], ['-V']]) {
    const observed = runSync(executable, args);
    if (observed.exitCode === 0 && `${observed.stdout}\n${observed.stderr}`.trim()) { version = observed; break; }
  }
  let help = null;
  for (const args of [['--help'], ['help'], ['-h']]) {
    const observed = runSync(executable, args);
    if (`${observed.stdout}\n${observed.stderr}`.trim()) { help = observed; break; }
  }
  return { name, found: true, paths, version, help };
}

export function inferPromptArgs(helpText, prompt) {
  const text = String(helpText ?? '').toLowerCase();
  if (text.includes('--print')) return { strategy: '--print', args: ['--print', prompt] };
  if (text.includes('--prompt')) return { strategy: '--prompt', args: ['--prompt', prompt] };
  if (/(^|\s)-p([,\s]|$)/m.test(text)) return { strategy: '-p', args: ['-p', prompt] };
  return { strategy: 'UNKNOWN', args: null, reason: 'No safe non-interactive prompt syntax inferred from help output.' };
}

export function discoverCapabilityHints(helpText) {
  const flags = [...new Set([...String(helpText ?? '').matchAll(/--[a-z0-9][a-z0-9-]*/gi)].map((m) => m[0]))];
  return {
    streamingOptions: flags.filter((x) => /(stream|output|json|verbose)/i.test(x)),
    sessionOptions: flags.filter((x) => /(session|resume|continue|conversation|thread)/i.test(x)),
    modelProviderOptions: flags.filter((x) => /(model|provider)/i.test(x)),
  };
}

export function parseArgsTemplate(jsonText, values, label) {
  if (!jsonText) return null;
  const parsed = JSON.parse(jsonText);
  if (!Array.isArray(parsed) || !parsed.every((x) => typeof x === 'string')) throw new Error(`${label} must be a JSON array of strings.`);
  return parsed.map((part) => {
    let out = part;
    for (const [key, value] of Object.entries(values)) out = out.replaceAll(`{${key}}`, String(value));
    return out;
  });
}
