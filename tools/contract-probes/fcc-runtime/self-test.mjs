#!/usr/bin/env node
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';
import {
  analyzeLine,
  captureMissingRuntime,
  captureProcess,
  classifyFailure,
  extractSessionCandidatesFromJson,
  maskSecretsPreserveLength,
  redactString,
} from './probe.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const fixture = path.join(here, 'fixture-process.mjs');
const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'fcc-runtime-self-test-'));
const fakeSecret = 'sk-FAKESECRET123456789';
const checks = [];
const pass = (name) => checks.push({ name, status: 'PASS' });

try {
  assert.equal(redactString(`Authorization: Bearer ${fakeSecret}`).includes(fakeSecret), false);
  assert.equal(maskSecretsPreserveLength(`token=${fakeSecret}`).length, `token=${fakeSecret}`.length);
  pass('secret-redaction');

  const jsonEvent = analyzeLine('{"type":"assistant_delta","delta":"x","session_id":"123e4567-e89b-42d3-a456-426614174000"}');
  assert.equal(jsonEvent.classification, 'JSON_EVENT');
  assert.equal(jsonEvent.eventTypeHint, 'assistant_delta');
  assert.ok(jsonEvent.sessionCandidates.length >= 1);
  assert.equal(analyzeLine('{bad-json}').classification, 'MALFORMED_JSON');
  assert.equal(analyzeLine('plain text').classification, 'TEXT_LINE');
  assert.ok(extractSessionCandidatesFromJson({ nested: { sessionId: 'session_abcdef12' } }).length === 1);
  pass('stream-parser-and-session-extractor');

  const stream = await captureProcess(process.execPath, [fixture, '--mode', 'stream'], { cwd: temp, timeoutMs: 5000 });
  assert.equal(stream.exitCode, 0);
  assert.ok(stream.rawFrames.length >= 4);
  assert.ok(stream.lineEvents.some((x) => x.classification === 'MALFORMED_JSON'));
  assert.ok(stream.lineEvents.some((x) => x.rawSanitized.includes('مرحبا')));
  assert.ok(stream.lineEvents.some((x) => x.eventTypeHint === 'mystery_event'));
  assert.ok(stream.lineEvents.some((x) => x.rawSanitized.length > 100000));
  assert.ok(stream.lineEvents.some((x) => x.eofFlush && x.eventTypeHint === 'final_result'));
  assert.ok(stream.sessionCandidates.some((x) => x.value === '123e4567-e89b-42d3-a456-426614174000'));
  const serializedStream = JSON.stringify(stream);
  assert.equal(serializedStream.includes(fakeSecret), false);
  assert.ok(stream.rawFrames.some((x) => x.sanitizedText.includes('*')));
  pass('partial-chunks-interleaving-unicode-large-abrupt-eof-raw-capture');

  const nonzero = await captureProcess(process.execPath, [fixture, '--mode', 'nonzero'], { cwd: temp, timeoutMs: 5000 });
  assert.equal(nonzero.exitCode, 17);
  assert.equal(nonzero.classification, 'NONZERO_EXIT');
  pass('nonzero-exit-classification');

  const rate = await captureProcess(process.execPath, [fixture, '--mode', 'rate-limit'], { cwd: temp, timeoutMs: 5000 });
  assert.equal(rate.classification, 'RATE_LIMITED');
  pass('synthetic-rate-limit-classification-mechanics');

  const timeout = await captureProcess(process.execPath, [fixture, '--mode', 'sleep'], { cwd: temp, timeoutMs: 350, gracefulWaitMs: 200 });
  assert.equal(timeout.timedOut, true);
  assert.equal(timeout.classification, 'TIMEOUT');
  assert.equal(timeout.processTreeCleanupObserved, true);
  pass('timeout-and-cleanup');

  const cancelled = await captureProcess(process.execPath, [fixture, '--mode', 'tree'], { cwd: temp, timeoutMs: 5000, cancelAfterMs: 350, gracefulWaitMs: 200, snapshotDelayMs: 250 });
  assert.equal(cancelled.cancelled, true);
  assert.equal(cancelled.classification, 'INTERRUPTED');
  assert.equal(cancelled.gracefulInterruptAttempted, true);
  assert.equal(cancelled.processTreeCleanupObserved, true);
  assert.ok(cancelled.observedProcessTree.length >= 1);
  pass('graceful-forced-owned-process-tree-cancellation');

  const lateTree = await captureProcess(process.execPath, [fixture, '--mode', 'late-tree'], { cwd: temp, timeoutMs: 5000, cancelAfterMs: 1000, gracefulWaitMs: 200, snapshotDelayMs: 100 });
  assert.equal(lateTree.cancelled, true);
  assert.equal(lateTree.classification, 'INTERRUPTED');
  assert.equal(lateTree.gracefulInterruptAttempted, true);
  assert.ok(lateTree.observedProcessTree.some((x) => x.role === 'descendant'));
  assert.equal(lateTree.processTreeCleanupObserved, true);
  pass('late-spawn-owned-descendant-observation-and-cleanup');

  const missing = await captureMissingRuntime(path.join(temp, 'definitely-missing', 'fcc-claude'));
  assert.equal(missing.classification, 'RUNTIME_NOT_FOUND');
  assert.equal(classifyFailure(missing).category, 'RUNTIME_NOT_FOUND');
  pass('target-unavailable-classification');

  const outputPath = path.join(temp, 'self-test-output.json');
  fs.writeFileSync(outputPath, JSON.stringify({ checks, stream, nonzero, rate, timeout, cancelled, lateTree, missing }, null, 2));
  const raw = fs.readFileSync(outputPath, 'utf8');
  assert.equal(raw.includes(fakeSecret), false);
  pass('persisted-secret-scan');

  console.log(JSON.stringify({ status: 'PASS', fixtureEvidence: 'SELF_TEST_ONLY', checks }, null, 2));
} finally {
  try { fs.rmSync(temp, { recursive: true, force: true }); } catch {}
}
