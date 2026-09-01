#!/usr/bin/env node
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const probe = path.join(path.dirname(fileURLToPath(import.meta.url)), 'probe.mjs');
const root = fs.mkdtempSync(path.join(os.tmpdir(), 'fcc-probe-self-test-'));
const out = path.join(root, 'result.json');
const missing = path.join(root, 'not installed', 'fcc-claude.exe');
const fakeKey = 'sk-selftest-THIS_MUST_NOT_APPEAR_123456';
const fakeToken = 'Bearer SELF_TEST_TOKEN_MUST_NOT_APPEAR';

try {
  const run = spawnSync(process.execPath, [probe, '--mode', 'all', '--fcc-claude', missing, '--json', out], {
    encoding: 'utf8',
    env: { ...process.env, FCC_API_KEY: fakeKey, ANTHROPIC_AUTH_TOKEN: fakeToken },
    timeout: 15000,
  });
  if (run.status !== 2) throw new Error(`Expected exit 2 for explicit missing runtime, got ${run.status}. stderr=${run.stderr}`);
  const raw = fs.readFileSync(out, 'utf8');
  if (raw.includes(fakeKey) || raw.includes('SELF_TEST_TOKEN_MUST_NOT_APPEAR')) throw new Error('Secret redaction failed.');
  const parsed = JSON.parse(raw);
  if (parsed.discovery.executables.fccClaude.found !== false) throw new Error('Explicit missing runtime was not classified as missing.');
  if (parsed.cli.fallbackAssessment !== 'BLOCKED_RUNTIME_NOT_FOUND') throw new Error('CLI fallback missing-runtime classification mismatch.');
  const env = Object.fromEntries(parsed.discovery.environmentVariablePresence.map((x) => [x.name, x.value]));
  if (env.FCC_API_KEY !== '[REDACTED]' || env.ANTHROPIC_AUTH_TOKEN !== '[REDACTED]') throw new Error('Secret environment values were not redacted.');
  console.log('PASS self-test: deterministic missing-runtime classification + redaction.');
} finally {
  fs.rmSync(root, { recursive: true, force: true });
}
