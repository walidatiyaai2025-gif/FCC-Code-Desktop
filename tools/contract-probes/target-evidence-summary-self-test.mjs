#!/usr/bin/env node
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { buildTargetEvidenceSummary } from './target-evidence-summary.mjs';

function assert(condition, message) { if (!condition) throw new Error(message); }
function writeJson(root, relative, value) { const file = path.join(root, relative); fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, JSON.stringify(value, null, 2)); return file; }

const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'fcc-p00-summary-selftest-'));
const repoRoot = path.join(temp, 'repo with spaces مسار');
fs.mkdirSync(repoRoot, { recursive: true });
try {
  const fcc = writeJson(repoRoot, 'evidence/phases/P00/target/fcc.json', {
    discovery: { host: { platform: 'win32', node: 'v20.11.1', git: { stdout: 'git version 2.47.0\n' }, dotnet: { stdout: '10.0.400\n' }, python: { stdout: 'Python 3.12.5\n' }, powershell: { stdout: '5.1.19041.6456\n' } }, executables: {
      fcc: { found: true, paths: ['C:\\Tools\\fcc.exe'], version: { stdout: 'free-claude-code 5.18.11\n' } },
      fccClaude: { found: true, paths: ['C:\\Tools\\fcc-claude.exe'], version: { stdout: '2.1.251 (Claude Code)\r\n' } },
      fccServer: { found: true, paths: ['C:\\Tools\\fcc-server.exe'], version: { stdout: 'free-claude-code 5.18.11\n' } },
      claude: { found: true, paths: ['C:\\Tools\\claude.exe'], version: { stdout: '2.1.251 (Claude Code)\n' } },
    } },
    cli: { fallbackAssessment: 'VERIFIED_FOR_TESTED_RUNTIME', livePromptAllowed: true, workspaceCases: [{ run: { classification: 'SUCCESS' } }], cancellationCase: { classification: 'CANCELLED' }, summary: { runtimeLaunch: 'PASS', promptTransmission: 'PASS' } },
  });
  const runtime = writeJson(repoRoot, 'evidence/phases/P00/target/runtime.json', {
    host: { platform: 'win32' }, evidenceStatus: 'EXECUTION_HOST_WINDOWS',
    runtime: { found: true, paths: ['C:\\Tools\\fcc-claude.exe'], version: { stdout: '2.1.251 (Claude Code)\n' } },
    streaming: { status: 'OBSERVED_FAILURE_ON_EXECUTION_HOST' },
    session: { status: 'BLOCKED_INITIAL_RUN_MISSING' },
    failure: { status: 'OBSERVED_ON_EXECUTION_HOST', rateLimit: 'NOT_OBSERVED_ON_TARGET' },
  });
  const unity = writeJson(repoRoot, 'evidence/phases/P00/target/unity.json', {
    host: { platform: 'win32' }, evidenceState: 'VERIFIED_ON_AVAILABLE_UNITY_HOST', overallStatus: 'PASS',
    discovery: { editors: { found: true, editors: [{ path: 'C:\\Unity\\Unity.exe', version: '6000.5.8f1' }] } },
    fixture: { selectedEditorVersion: '6000.5.8f1', cleanup: 'REMOVED' },
  });
  const blender = writeJson(repoRoot, 'evidence/phases/P00/target/blender.json', {
    host: { platform: 'win32' }, evidenceState: 'TARGET_UNVERIFIED', overallStatus: 'BLOCKED_BLENDER_NOT_FOUND',
    discovery: { found: false, candidates: [] },
  });

  const summary = buildTargetEvidenceSummary({ repoRoot, authoritativeTarget: true, fccFile: fcc, fccExit: 0, runtimeFile: runtime, runtimeExit: 2, unityFile: unity, unityExit: 0, blenderFile: blender, blenderExit: 2 });
  assert(summary.schemaVersion === 2, 'Summary schema version must be 2.');
  assert(summary.contracts.fccDiscoveryCli.status === 'PASS', 'FCC status must preserve exit-code PASS.');
  assert(summary.contracts.fccDiscoveryCli.tools.find((x) => x.name === 'fcc-claude')?.version === '2.1.251 (Claude Code)', 'FCC version must be surfaced.');
  assert(summary.contracts.fccDiscoveryCli.tools.find((x) => x.name === '.NET SDK')?.version === '10.0.400', 'Detected host tool versions must be surfaced.');
  assert(summary.contracts.fccDiscoveryCli.artifactPath === 'evidence/phases/P00/target/fcc.json', 'Artifact paths must be repo-relative.');
  assert(summary.contracts.fccStreamingSessionFailure.status === 'BLOCKED', 'Runtime exit 2 must remain BLOCKED.');
  assert(summary.contracts.fccStreamingSessionFailure.reason.includes('rateLimit=NOT_OBSERVED_ON_TARGET'), 'Natural rate-limit non-observation must remain explicit.');
  assert(summary.contracts.fccStreamingSessionFailure.executedOnAuthoritativeTarget === true, 'Windows runtime result should identify target execution only when authorized.');
  assert(summary.contracts.unity.targetBehaviorObserved === true, 'Verified Unity target behavior must remain observed.');
  assert(summary.contracts.blender.resultState === 'NOT_INSTALLED', 'Missing Blender must remain distinguishable from generic BLOCKED.');
  assert(summary.contracts.blender.executedOnAuthoritativeTarget === true && summary.contracts.blender.targetBehaviorObserved === false, 'Target execution and unobserved Blender automation must remain distinct.');

  const unauthorized = buildTargetEvidenceSummary({ repoRoot, authoritativeTarget: false, fccFile: fcc, fccExit: 0, runtimeFile: runtime, runtimeExit: 2, unityFile: unity, unityExit: 0, blenderFile: blender, blenderExit: 2 });
  assert(Object.values(unauthorized.contracts).every((x) => x.executedOnAuthoritativeTarget === false), 'Standalone/cloud summary must never self-promote to target evidence.');

  const missing = buildTargetEvidenceSummary({ repoRoot, authoritativeTarget: true, fccFile: path.join(repoRoot, 'missing.json'), fccExit: 1, runtimeFile: runtime, runtimeExit: 2, unityFile: unity, unityExit: 0, blenderFile: blender, blenderExit: 2 });
  assert(missing.contracts.fccDiscoveryCli.status === 'FAIL' && missing.contracts.fccDiscoveryCli.reason === 'EVIDENCE_FILE_MISSING', 'Missing evidence must be a controlled FAIL reason.');

  const scriptPath = path.join(path.dirname(fileURLToPath(import.meta.url)), 'target-evidence-summary.mjs');
  const cliOutput = path.join(repoRoot, 'evidence', 'phases', 'P00', 'target', 'P00_TARGET_CONTRACT_SUMMARY.json');
  const cli = spawnSync(process.execPath, [
    scriptPath, '--repo-root', repoRoot, '--authoritative-target',
    '--fcc-file', fcc, '--fcc-exit', '0',
    '--runtime-file', runtime, '--runtime-exit', '2',
    '--unity-file', unity, '--unity-exit', '0',
    '--blender-file', blender, '--blender-exit', '2',
    '--output', cliOutput,
  ], { cwd: repoRoot, encoding: 'utf8', shell: false, windowsHide: true, timeout: 15000 });
  assert(cli.status === 0 && fs.existsSync(cliOutput), `CLI summary invocation failed: ${cli.stderr || cli.stdout}`);
  const cliSummary = JSON.parse(fs.readFileSync(cliOutput, 'utf8'));
  assert(cliSummary.schemaVersion === 2 && cliSummary.contracts.blender.resultState === 'NOT_INSTALLED', 'CLI output must preserve schema and result-state semantics.');

  console.log(JSON.stringify({ status: 'SELF_TEST_VERIFIED', schemaVersion: summary.schemaVersion, assertions: 14, cliInvocation: 'PASS', unicodeSpacePath: 'PASS', targetEvidenceClaimed: false }, null, 2));
} finally {
  fs.rmSync(temp, { recursive: true, force: true });
}
