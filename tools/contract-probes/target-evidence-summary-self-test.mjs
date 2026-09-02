#!/usr/bin/env node
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { buildTargetEvidenceSummary } from './target-evidence-summary.mjs';

let assertions = 0;
function assert(condition, message) { assertions++; if (!condition) throw new Error(message); }
function writeJson(root, relative, value) { const file = path.join(root, relative); fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, JSON.stringify(value, null, 2)); return file; }

const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'fcc-p00-summary-selftest-'));
const repoRoot = path.join(temp, 'repo with spaces مسار');
fs.mkdirSync(repoRoot, { recursive: true });
try {
  const fcc = writeJson(repoRoot, 'evidence/phases/P00/target/fcc.json', {
    discovery: { host: { platform: 'win32', node: 'v20.11.1', git: { found: true, paths: ['C:\\Git\\git.exe'], result: { stdout: 'git version 2.47.0\n' } }, dotnet: { found: true, paths: ['C:\\dotnet\\dotnet.exe'], result: { stdout: '10.0.400\n' } }, python: { found: true, paths: ['C:\\Python\\python.exe'], result: { stdout: 'Python 3.12.5\n' } }, powershell: { command: ['C:\\PowerShell\\pwsh.exe'], stdout: '7.6.4\n' } }, executables: {
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
    streaming: { status: 'OBSERVED_ON_EXECUTION_HOST' },
    session: { status: 'OBSERVED_ON_EXECUTION_HOST' },
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
  const integratedFailure = writeJson(repoRoot, 'evidence/phases/P00/failure/fcc-failure-target-exact-head.json', {
    testedSourceSha: '015ffd8c0e2a6e725e33ed153441ff51e7952556',
    host: { platform: 'win32' }, evidenceStatus: 'EXECUTION_HOST_WINDOWS', providerStatus: 'OBSERVED_SUCCESS',
    failure: {
      status: 'OBSERVED_ON_EXECUTION_HOST', rateLimit: 'NOT_OBSERVED_ON_TARGET',
      liveCancellation: { classification: 'INTERRUPTED', cancelled: true, gracefulInterruptAttempted: true, processTreeCleanupObserved: true, remainingOwnedProcesses: [] },
    },
  });

  const base = { repoRoot, authoritativeTarget: true, fccFile: fcc, fccExit: 0, runtimeFile: runtime, runtimeExit: 0, unityFile: unity, unityExit: 0, blenderFile: blender, blenderExit: 2, integratedFailureFile: integratedFailure, integratedFailureSourceIsAncestor: true };
  const summary = buildTargetEvidenceSummary(base);
  assert(summary.schemaVersion === 2, 'Summary schema version must remain 2.');
  assert(summary.contracts.fccDiscoveryCli.status === 'PASS', 'FCC status must preserve exit-code PASS.');
  assert(summary.contracts.fccDiscoveryCli.tools.find((x) => x.name === 'fcc-claude')?.version === '2.1.251 (Claude Code)', 'FCC version must be surfaced.');
  assert(summary.contracts.fccDiscoveryCli.tools.find((x) => x.name === '.NET SDK')?.version === '10.0.400', 'Nested host probe versions must be surfaced.');
  assert(summary.contracts.fccDiscoveryCli.artifactPath === 'evidence/phases/P00/target/fcc.json', 'Artifact paths must be repo-relative.');
  assert(summary.contracts.fccStreamingSessionFailure.status === 'PASS', 'Runtime evidence can PASS while rate-limit remains naturally unobserved.');
  assert(summary.contracts.fccStreamingSessionFailure.observations.rateLimit === 'NOT_OBSERVED_ON_TARGET', 'NOT_OBSERVED_ON_TARGET must remain explicit and never be rewritten as PASS.');
  assert(summary.p00Readiness.p00_005.status === 'PASS', 'P00-005 must be satisfied from integrated exact-head evidence.');
  assert(summary.p00Readiness.p00_005.testedSourceSha === '015ffd8c0e2a6e725e33ed153441ff51e7952556', 'P00-005 tested source SHA must be preserved.');
  assert(summary.p00Readiness.p00_005.sourceIsAncestorOfRepoHead === true, 'Integrated exact-head evidence must require ancestry validation.');
  assert(summary.p00Readiness.p00_005.artifactPath === 'evidence/phases/P00/failure/fcc-failure-target-exact-head.json', 'P00-005 supporting evidence link must be preserved.');
  assert(summary.p00Readiness.pg_002.status === 'RESOLVED', 'PG-002 safe policy must be reflected as resolved.');
  assert(summary.p00Readiness.pg_002.observationState === 'NOT_OBSERVED_ON_TARGET', 'PG-002 observation state must remain distinct from policy resolution.');
  assert(summary.p00Readiness.pg_002.actual429Observed === false, 'Safe closure must not claim an actual 429.');
  assert(summary.p00Readiness.pg_002.policyState === 'SAFE_NON_OBSERVATION_ACCEPTED', 'Safe non-observation policy must be explicit.');
  assert(summary.contracts.blender.status === 'BLOCKED' && summary.contracts.blender.resultState === 'NOT_INSTALLED', 'Missing Blender must never be represented as PASS.');
  assert(summary.p00Readiness.p00_009.status === 'BLOCKED', 'P00-009 must remain blocked without real Blender success.');
  assert(summary.p00Readiness.finalTargetManifestCanSupportP00_009Closure === false, 'Final manifest must not support P00-009 closure while Blender is unavailable.');
  assert(summary.p00Readiness.p00TargetValidationComplete === false, 'P00 target validation must remain incomplete while Blender is unavailable.');

  const blenderExitZeroButMissing = buildTargetEvidenceSummary({ ...base, blenderExit: 0 });
  assert(blenderExitZeroButMissing.contracts.blender.status === 'BLOCKED', 'BLOCKED_BLENDER_NOT_FOUND evidence must override an inconsistent zero exit code.');
  assert(blenderExitZeroButMissing.p00Readiness.finalTargetManifestCanSupportP00_009Closure === false, 'Inconsistent zero exit must not unlock P00-009.');

  const blenderPassFile = writeJson(repoRoot, 'evidence/phases/P00/target/blender-pass.json', {
    host: { platform: 'win32' }, evidenceState: 'VERIFIED_ON_AVAILABLE_BLENDER_HOST', overallStatus: 'PASS',
    discovery: { found: true, candidates: [{ path: 'C:\\Blender\\blender.exe', version: '4.5.2' }] },
  });
  const blenderPass = buildTargetEvidenceSummary({ ...base, blenderFile: blenderPassFile, blenderExit: 0 });
  assert(blenderPass.contracts.blender.targetBehaviorObserved === true, 'Real verified Blender target behavior must be observed.');
  assert(blenderPass.p00Readiness.p00_009.status === 'READY_FOR_CLOSURE_RECONCILIATION', 'Only real Blender success may make P00-009 closure-supporting.');
  assert(blenderPass.p00Readiness.finalTargetManifestCanSupportP00_009Closure === true, 'Real Blender success must unlock closure support.');
  assert(blenderPass.p00Readiness.p00TargetValidationComplete === true, 'Target validation readiness may complete only with P00-005 evidence and real Blender success.');

  const wrongAncestry = buildTargetEvidenceSummary({ ...base, integratedFailureSourceIsAncestor: false });
  assert(wrongAncestry.p00Readiness.p00_005.status === 'FAIL', 'Integrated evidence outside current HEAD ancestry must fail closed.');
  assert(wrongAncestry.p00Readiness.pg_002.status === 'UNSATISFIED', 'PG-002 policy evidence must fail closed when P00-005 provenance is invalid.');

  const unauthorized = buildTargetEvidenceSummary({ ...base, authoritativeTarget: false });
  assert(Object.values(unauthorized.contracts).every((x) => x.executedOnAuthoritativeTarget === false), 'Standalone/cloud summary must never self-promote to target evidence.');
  assert(unauthorized.p00Readiness.p00_009.status === 'BLOCKED', 'Unauthorized summary must not support Blender closure.');

  const noPath = buildTargetEvidenceSummary({ repoRoot, authoritativeTarget: true, fccExit: 0, runtimeExit: 0, unityExit: 0, blenderExit: 0, integratedFailureFile: integratedFailure, integratedFailureSourceIsAncestor: true });
  assert(Object.values(noPath.contracts).every((x) => x.status === 'FAIL' && x.reason === 'EVIDENCE_PATH_NOT_SUPPLIED'), 'Missing mandatory lane paths must force FAIL across all lanes.');

  const missingPath = path.join(repoRoot, 'missing.json');
  const missing = buildTargetEvidenceSummary({ ...base, blenderFile: missingPath, blenderExit: 0 });
  assert(missing.contracts.blender.status === 'FAIL' && missing.contracts.blender.reason === 'EVIDENCE_FILE_MISSING', 'Missing mandatory evidence must force FAIL even with zero exit.');

  const unreadablePath = path.join(repoRoot, 'evidence', 'phases', 'P00', 'target', 'unreadable.json');
  fs.writeFileSync(unreadablePath, '{not valid json', 'utf8');
  const unreadable = buildTargetEvidenceSummary({ ...base, blenderFile: unreadablePath, blenderExit: 0 });
  assert(unreadable.contracts.blender.status === 'FAIL' && unreadable.contracts.blender.reason === 'EVIDENCE_JSON_UNREADABLE', 'Unreadable mandatory evidence must force FAIL.');

  const malformedFailure = writeJson(repoRoot, 'evidence/phases/P00/failure/malformed.json', { testedSourceSha: 'bad', host: { platform: 'win32' } });
  const malformed = buildTargetEvidenceSummary({ ...base, integratedFailureFile: malformedFailure });
  assert(malformed.p00Readiness.p00_005.status === 'FAIL', 'Malformed mandatory integrated P00-005 evidence must fail closed.');
  assert(malformed.p00Readiness.p00TargetValidationComplete === false, 'Malformed P00-005 evidence must keep target readiness incomplete.');

  const scriptPath = path.join(path.dirname(fileURLToPath(import.meta.url)), 'target-evidence-summary.mjs');
  const cliOutput = path.join(repoRoot, 'evidence', 'phases', 'P00', 'target', 'P00_TARGET_CONTRACT_SUMMARY.json');
  const cli = spawnSync(process.execPath, [
    scriptPath, '--repo-root', repoRoot, '--authoritative-target', '--integrated-failure-source-is-ancestor',
    '--fcc-file', fcc, '--fcc-exit', '0',
    '--runtime-file', runtime, '--runtime-exit', '0',
    '--unity-file', unity, '--unity-exit', '0',
    '--blender-file', blender, '--blender-exit', '2',
    '--integrated-failure-file', integratedFailure,
    '--output', cliOutput,
  ], { cwd: repoRoot, encoding: 'utf8', shell: false, windowsHide: true, timeout: 15000 });
  assert(cli.status === 0 && fs.existsSync(cliOutput), `CLI summary invocation failed: ${cli.stderr || cli.stdout}`);
  const cliSummary = JSON.parse(fs.readFileSync(cliOutput, 'utf8'));
  assert(cliSummary.p00Readiness.p00_005.status === 'PASS' && cliSummary.p00Readiness.p00_009.status === 'BLOCKED', 'CLI output must preserve P00 readiness semantics.');

  console.log(JSON.stringify({ status: 'SELF_TEST_VERIFIED', schemaVersion: summary.schemaVersion, assertions, cliInvocation: 'PASS', unicodeSpacePath: 'PASS', failClosedEvidence: 'PASS', pg002SafePolicy: 'PASS', blenderClosureGate: 'PASS', integratedP00005: 'PASS', targetEvidenceClaimed: false }, null, 2));
} finally {
  fs.rmSync(temp, { recursive: true, force: true });
}
