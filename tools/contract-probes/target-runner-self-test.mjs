#!/usr/bin/env node
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const EXIT = Object.freeze({ PASS: 0, FAIL: 1 });

function run(file, args, options = {}) {
  const result = spawnSync(file, args, {
    cwd: options.cwd,
    encoding: 'utf8',
    shell: false,
    windowsHide: true,
    timeout: options.timeoutMs ?? 15000,
    env: options.env ?? process.env,
  });
  return { status: result.status, signal: result.signal, error: result.error ? String(result.error.message ?? result.error) : null, stdout: result.stdout ?? '', stderr: result.stderr ?? '' };
}
function assert(condition, message) { if (!condition) throw new Error(message); }
function git(cwd, args) { const result = run('git', args, { cwd }); if (result.error || result.status !== 0) throw new Error(`git ${args.join(' ')} failed: ${result.error ?? result.stderr.trim()}`); return result.stdout; }
function sourceDirtyEntries(cwd) { const result = run('git', ['status','--porcelain=v1','--untracked-files=all','--','.',':(exclude)evidence/phases/P00/target/**'], { cwd }); if (result.error || result.status !== 0) throw new Error(`git status pathspec check failed: ${result.error ?? result.stderr.trim()}`); return result.stdout.split(/\r?\n/).filter(Boolean); }
function write(root, relativePath, content) { const full = path.join(root, relativePath); fs.mkdirSync(path.dirname(full), { recursive: true }); fs.writeFileSync(full, content, 'utf8'); }

function assertStaticRunnerPolicy(runnerText) {
  const required = [
    "[Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT",
    "TARGET_PREREQUISITE_MISSING: git is required for exact-head target evidence.",
    "TARGET_PREREQUISITE_MISSING: node is required to execute the canonical P00 probes.",
    "WRONG_REPOSITORY_CHECKOUT:",
    "$runnerEvidenceExclude = ':(exclude)evidence/phases/P00/target/**'",
    "git status --porcelain=v1 --untracked-files=all -- . $runnerEvidenceExclude",
    "EXACT_HEAD_REQUIRED: target validation refuses source/configuration worktree changes outside repository-owned target-evidence outputs",
    "fcc-discovery-cli-target",
    "fcc-stream-session-failure-target",
    "unity-contract-target",
    "blender-contract-target",
    "target-evidence-summary-self-test",
    "target-evidence-summary.mjs",
    "P00_TARGET_CONTRACT_SUMMARY.json",
    "contractSummarySchemaVersion = $contractSummary.schemaVersion",
    "contracts = $contractSummary.contracts",
    "schemaVersion = 2",
  ];
  for (const marker of required) assert(runnerText.includes(marker), `Runner policy marker missing: ${marker}`);
}

function runGitPathspecRegression() {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'fcc-p00-runner-selftest-'));
  try {
    git(temp, ['init', '--quiet']); git(temp, ['config', 'user.email', 'selftest@example.invalid']); git(temp, ['config', 'user.name', 'FCC P00 Self Test']);
    write(temp, 'tools/contract-probes/run-target-validation.ps1', '# fixture runner\n'); write(temp, 'docs/config.txt', 'canonical\n'); write(temp, 'evidence/phases/P00/target/prior.json', '{"status":"old"}\n'); write(temp, 'evidence/phases/P00/unity/historical.md', 'historical\n'); git(temp, ['add', '.']); git(temp, ['commit', '--quiet', '-m', 'fixture baseline']);
    assert(sourceDirtyEntries(temp).length === 0, 'Clean fixture must be accepted.');
    write(temp, 'evidence/phases/P00/target/prior.json', '{"status":"rerun"}\n'); write(temp, 'evidence/phases/P00/target/nested/new-output.json', '{"status":"new"}\n'); assert(sourceDirtyEntries(temp).length === 0, 'Target evidence output dirtiness must be excluded for rerun safety.');
    write(temp, 'evidence/phases/P00/unity/historical.md', 'changed\n'); let dirty = sourceDirtyEntries(temp); assert(dirty.some((line) => line.includes('evidence/phases/P00/unity/historical.md')), 'Non-target evidence changes must remain blocking.'); git(temp, ['checkout', '--quiet', '--', 'evidence/phases/P00/unity/historical.md']);
    write(temp, 'tools/contract-probes/run-target-validation.ps1', '# changed executable input\n'); dirty = sourceDirtyEntries(temp); assert(dirty.some((line) => line.includes('tools/contract-probes/run-target-validation.ps1')), 'Tracked executable-input changes must remain blocking.'); git(temp, ['checkout', '--quiet', '--', 'tools/contract-probes/run-target-validation.ps1']);
    write(temp, 'tools/contract-probes/untracked-probe.mjs', 'console.log("untracked");\n'); dirty = sourceDirtyEntries(temp); assert(dirty.some((line) => line.includes('tools/contract-probes/untracked-probe.mjs')), 'Untracked executable-input files must remain blocking.'); fs.rmSync(path.join(temp, 'tools/contract-probes/untracked-probe.mjs'));
    write(temp, 'docs/new config with spaces.txt', 'dirty\n'); dirty = sourceDirtyEntries(temp); assert(dirty.some((line) => line.includes('docs/new config with spaces.txt')), 'Space-containing source/config paths must remain blocking.'); fs.rmSync(path.join(temp, 'docs/new config with spaces.txt'));
    assert(sourceDirtyEntries(temp).length === 0, 'Only target-evidence output dirtiness should remain accepted at end of fixture.');
    return { cleanAccepted: true, targetEvidenceModifiedAccepted: true, targetEvidenceUntrackedAccepted: true, siblingEvidenceBlocked: true, trackedSourceBlocked: true, untrackedSourceBlocked: true, spacePathBlocked: true };
  } finally { fs.rmSync(temp, { recursive: true, force: true }); }
}

function main() {
  const scriptDir = path.dirname(fileURLToPath(import.meta.url));
  const repoRoot = process.env.FCC_P00_REPO_ROOT ? path.resolve(process.env.FCC_P00_REPO_ROOT) : path.resolve(scriptDir, '..', '..');
  const runnerPath = path.join(repoRoot, 'tools', 'contract-probes', 'run-target-validation.ps1');
  const summarySelfTestPath = path.join(repoRoot, 'tools', 'contract-probes', 'target-evidence-summary-self-test.mjs');
  assert(fs.existsSync(runnerPath), `Runner not found: ${runnerPath}`);
  assert(fs.existsSync(summarySelfTestPath), `Summary self-test not found: ${summarySelfTestPath}`);
  assertStaticRunnerPolicy(fs.readFileSync(runnerPath, 'utf8'));
  const summaryResult = run(process.execPath, [summarySelfTestPath], { cwd: repoRoot });
  assert(summaryResult.status === 0 && summaryResult.stdout.includes('SELF_TEST_VERIFIED'), `Target evidence summary self-test failed: ${summaryResult.stderr || summaryResult.stdout}`);
  const mechanics = runGitPathspecRegression();
  console.log(JSON.stringify({ status: 'SELF_TEST_VERIFIED', runner: path.relative(repoRoot, runnerPath).replaceAll('\\', '/'), staticPolicyMarkers: 'PASS', targetEvidenceSummary: 'PASS', gitPathspecMechanics: mechanics, targetEvidenceClaimed: false }, null, 2));
}
try { main(); process.exit(EXIT.PASS); } catch (error) { console.error(`TARGET_RUNNER_SELF_TEST_FAIL: ${error?.stack ?? error}`); process.exit(EXIT.FAIL); }
