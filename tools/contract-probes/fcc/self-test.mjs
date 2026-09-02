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
const opaqueBearer = 'eyJhbGciOiJIUzI1NiJ9.payload.signature';
const basicCredential = 'dXNlcjpwYXNzd29yZA==';

try {
  const run = spawnSync(process.execPath, [probe, '--mode', 'all', '--fcc-claude', missing, '--json', out], {
    encoding: 'utf8',
    // Keep this deterministic even on a target machine with FCC/Claude tools installed.
    // The probe under test still launches through the absolute Node executable.
    env: { ...process.env, PATH: path.dirname(process.execPath), FCC_API_KEY: fakeKey, ANTHROPIC_AUTH_TOKEN: fakeToken },
    timeout: 15000,
  });
  if (run.status !== 2) throw new Error(`Expected exit 2 for explicit missing runtime, got ${run.status}. error=${run.error?.message ?? 'none'} stderr=${run.stderr}`);
  const raw = fs.readFileSync(out, 'utf8');
  if (raw.includes(fakeKey) || raw.includes('SELF_TEST_TOKEN_MUST_NOT_APPEAR')) throw new Error('Secret redaction failed.');
  const parsed = JSON.parse(raw);
  if (parsed.discovery.executables.fccClaude.found !== false) throw new Error('Explicit missing runtime was not classified as missing.');
  if (parsed.cli.fallbackAssessment !== 'BLOCKED_RUNTIME_NOT_FOUND') throw new Error('CLI fallback missing-runtime classification mismatch.');
  const env = Object.fromEntries(parsed.discovery.environmentVariablePresence.map((x) => [x.name, x.value]));
  if (env.FCC_API_KEY !== '[REDACTED]' || env.ANTHROPIC_AUTH_TOKEN !== '[REDACTED]') throw new Error('Secret environment values were not redacted.');

  const fixture = path.join(root, 'authorization-fixture.mjs');
  const redactionOut = path.join(root, 'authorization-result.json');
  fs.writeFileSync(
    fixture,
    `process.stdout.write(${JSON.stringify(`Authorization: Bearer ${opaqueBearer}\n`)});\n` +
      `process.stderr.write(${JSON.stringify(`Authorization: Basic ${basicCredential}\n`)});\n` +
      `setTimeout(() => process.exit(0), 350);\n`,
    'utf8',
  );
  const cliArgsJson = JSON.stringify([fixture, '{prompt}']);
  const redactionRun = spawnSync(process.execPath, [
    probe,
    '--mode', 'cli',
    '--fcc-claude', process.execPath,
    '--allow-live-prompt',
    '--cli-args-json', cliArgsJson,
    '--timeout-ms', '3000',
    '--cancel-after-ms', '250',
    '--json', redactionOut,
  ], {
    encoding: 'utf8',
    env: { ...process.env, PATH: path.dirname(process.execPath) },
    timeout: 30000,
  });
  if (![0, 2].includes(redactionRun.status)) {
    throw new Error(`Authorization redaction fixture hit probe infrastructure failure: status=${redactionRun.status} error=${redactionRun.error?.message ?? 'none'} stderr=${redactionRun.stderr}`);
  }
  const redactionRaw = fs.readFileSync(redactionOut, 'utf8');
  if (redactionRaw.includes(opaqueBearer) || redactionRaw.includes(basicCredential)) {
    throw new Error('Opaque Authorization credential leaked into persisted FCC probe evidence.');
  }
  const redactionParsed = JSON.parse(redactionRaw);
  if (!Array.isArray(redactionParsed.cli?.workspaceCases) || redactionParsed.cli.workspaceCases.length !== 3) {
    throw new Error('Authorization redaction fixture did not exercise all workspace cases.');
  }
  const persistedRuns = [...redactionParsed.cli.workspaceCases.map((x) => x.run), redactionParsed.cli.cancellationCase].filter(Boolean);
  if (!persistedRuns.length || persistedRuns.some((item) => JSON.stringify(item).includes(opaqueBearer) || JSON.stringify(item).includes(basicCredential))) {
    throw new Error('Authorization redaction failed in persisted run/event evidence.');
  }
  if (!persistedRuns.some((item) => `${item.stdout ?? ''}\n${item.stderr ?? ''}\n${JSON.stringify(item.events ?? [])}`.includes('[REDACTED]'))) {
    throw new Error('Authorization fixture did not prove redaction markers in captured output.');
  }

  console.log('PASS self-test: deterministic missing-runtime classification + opaque authorization redaction.');
} finally {
  fs.rmSync(root, { recursive: true, force: true });
}
