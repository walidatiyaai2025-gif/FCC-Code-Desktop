#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const EXIT = Object.freeze({ PASS: 0, FAIL: 1, USAGE: 64 });

function firstLine(value) {
  if (value == null) return null;
  const text = String(value).replace(/\r/g, '').split('\n').map((x) => x.trim()).find(Boolean);
  return text ? text.slice(0, 240) : null;
}

function versionFromRun(run) {
  return firstLine(run?.stdout) ?? firstLine(run?.stderr);
}

function versionFromHostProbe(probe) {
  return versionFromRun(probe?.result ?? probe);
}

function readEvidence(filePath) {
  if (!filePath) return { data: null, state: 'EVIDENCE_PATH_NOT_SUPPLIED' };
  try {
    if (!fs.existsSync(filePath)) return { data: null, state: 'EVIDENCE_FILE_MISSING' };
    return { data: JSON.parse(fs.readFileSync(filePath, 'utf8')), state: 'READ' };
  } catch {
    return { data: null, state: 'EVIDENCE_JSON_UNREADABLE' };
  }
}

function normalizedArtifactPath(filePath, repoRoot) {
  if (!filePath) return null;
  const resolved = path.resolve(filePath);
  if (!repoRoot) return resolved.replaceAll('\\', '/');
  const relative = path.relative(path.resolve(repoRoot), resolved);
  if (!relative.startsWith('..') && !path.isAbsolute(relative)) return relative.replaceAll('\\', '/');
  return path.basename(resolved);
}

function statusFromExit(exitCode) {
  if (exitCode === 0) return 'PASS';
  if (exitCode === 2) return 'BLOCKED';
  return 'FAIL';
}

function hasWindowsHost(data) {
  const platform = data?.host?.platform ?? data?.discovery?.host?.platform;
  return String(platform ?? '').toLowerCase() === 'win32';
}

function compactTool({ name, found, executablePath, version, evidence = null }) {
  return {
    name,
    found: Boolean(found),
    executablePath: executablePath || null,
    version: version || null,
    evidence,
  };
}

function fccSummary(record, exitCode, artifactPath, authoritativeTarget) {
  const data = record.data;
  const discovery = data?.discovery;
  const executables = discovery?.executables ?? {};
  const fccClaude = executables.fccClaude;
  const fccServer = executables.fccServer;
  const claude = executables.claude;
  const executedOnTarget = Boolean(authoritativeTarget && hasWindowsHost(data));
  let reason = record.state;
  let resultState = statusFromExit(exitCode);
  if (data) {
    if (!fccClaude?.found) {
      reason = 'BLOCKED_RUNTIME_NOT_FOUND';
      resultState = 'NOT_INSTALLED';
    } else {
      const assessment = data?.cli?.fallbackAssessment ?? data?.cli?.classification ?? (exitCode === 0 ? 'REQUESTED_CONTRACT_EVIDENCE_COMPLETED' : `PROBE_EXIT_${exitCode}`);
      const workspaceClassifications = (data?.cli?.workspaceCases ?? []).map((item) => item?.run?.classification).filter(Boolean);
      const cancellationClassification = data?.cli?.cancellationCase?.classification ?? null;
      const failedChecks = Object.entries(data?.cli?.summary ?? {}).filter(([, value]) => value === 'FAIL').map(([key]) => key);
      const details = [];
      if (workspaceClassifications.length) details.push(`workspace=${workspaceClassifications.join(',')}`);
      if (cancellationClassification) details.push(`cancellation=${cancellationClassification}`);
      if (failedChecks.length) details.push(`failedChecks=${failedChecks.join(',')}`);
      reason = details.length ? `${assessment};${details.join(';')}` : assessment;
    }
  }
  const targetBehaviorObserved = executedOnTarget && Boolean(fccClaude?.found);
  return {
    status: statusFromExit(exitCode),
    resultState,
    reason,
    errorSummary: reason,
    artifactPath,
    executedOnAuthoritativeTarget: executedOnTarget,
    targetBehaviorObserved,
    observationState: targetBehaviorObserved ? 'OBSERVED_ON_TARGET' : executedOnTarget ? 'NOT_OBSERVED_OR_NOT_INSTALLED_ON_TARGET' : 'TARGET_UNVERIFIED',
    observations: {
      fallbackAssessment: data?.cli?.fallbackAssessment ?? null,
      livePromptAllowed: data?.cli?.livePromptAllowed ?? null,
      workspaceClassifications: (data?.cli?.workspaceCases ?? []).map((item) => item?.run?.classification).filter(Boolean),
      cancellationClassification: data?.cli?.cancellationCase?.classification ?? null,
      checkSummary: data?.cli?.summary ?? null,
    },
    tools: [
      compactTool({ name: 'fcc', found: executables.fcc?.found, executablePath: executables.fcc?.paths?.[0], version: versionFromRun(executables.fcc?.version), evidence: 'discovery.executables.fcc' }),
      compactTool({ name: 'fcc-claude', found: fccClaude?.found, executablePath: fccClaude?.paths?.[0], version: versionFromRun(fccClaude?.version), evidence: 'discovery.executables.fccClaude' }),
      compactTool({ name: 'fcc-server', found: fccServer?.found, executablePath: fccServer?.paths?.[0], version: versionFromRun(fccServer?.version), evidence: 'discovery.executables.fccServer' }),
      compactTool({ name: 'claude', found: claude?.found, executablePath: claude?.paths?.[0], version: versionFromRun(claude?.version), evidence: 'discovery.executables.claude' }),
      compactTool({ name: 'Node.js', found: Boolean(discovery?.host?.node), executablePath: null, version: firstLine(discovery?.host?.node), evidence: 'discovery.host.node' }),
      compactTool({ name: 'Git', found: Boolean(discovery?.host?.git?.found ?? versionFromHostProbe(discovery?.host?.git)), executablePath: discovery?.host?.git?.paths?.[0] ?? null, version: versionFromHostProbe(discovery?.host?.git), evidence: 'discovery.host.git' }),
      compactTool({ name: '.NET SDK', found: Boolean(discovery?.host?.dotnet?.found ?? versionFromHostProbe(discovery?.host?.dotnet)), executablePath: discovery?.host?.dotnet?.paths?.[0] ?? null, version: versionFromHostProbe(discovery?.host?.dotnet), evidence: 'discovery.host.dotnet' }),
      compactTool({ name: 'Python', found: Boolean(discovery?.host?.python?.found ?? versionFromHostProbe(discovery?.host?.python)), executablePath: discovery?.host?.python?.paths?.[0] ?? null, version: versionFromHostProbe(discovery?.host?.python), evidence: 'discovery.host.python' }),
      compactTool({ name: 'PowerShell (FCC probe)', found: Boolean(versionFromHostProbe(discovery?.host?.powershell)), executablePath: discovery?.host?.powershell?.command?.[0] ?? null, version: versionFromHostProbe(discovery?.host?.powershell), evidence: 'discovery.host.powershell' }),
    ],
  };
}

function runtimeSummary(record, exitCode, artifactPath, authoritativeTarget) {
  const data = record.data;
  const runtime = data?.runtime;
  const executedOnTarget = Boolean(authoritativeTarget && hasWindowsHost(data) && data?.evidenceStatus === 'EXECUTION_HOST_WINDOWS');
  const states = {
    streaming: data?.streaming?.status ?? 'NOT_REQUESTED_OR_MISSING',
    session: data?.session?.status ?? 'NOT_REQUESTED_OR_MISSING',
    failure: data?.failure?.status ?? 'NOT_REQUESTED_OR_MISSING',
    rateLimit: data?.failure?.rateLimit ?? 'NOT_OBSERVED',
  };
  let reason = record.state;
  let resultState = statusFromExit(exitCode);
  if (data) {
    if (!runtime?.found) {
      reason = 'BLOCKED_RUNTIME_NOT_FOUND';
      resultState = 'NOT_INSTALLED';
    } else {
      reason = `streaming=${states.streaming};session=${states.session};failure=${states.failure};rateLimit=${states.rateLimit}`;
    }
  }
  const targetBehaviorObserved = executedOnTarget && [states.streaming, states.session, states.failure].some((x) => String(x).startsWith('OBSERVED'));
  return {
    status: statusFromExit(exitCode),
    resultState,
    reason,
    errorSummary: statusFromExit(exitCode) === 'FAIL' ? reason : null,
    artifactPath,
    executedOnAuthoritativeTarget: executedOnTarget,
    targetBehaviorObserved,
    observationState: data?.evidenceStatus ?? (executedOnTarget ? 'EXECUTION_HOST_WINDOWS' : 'TARGET_UNVERIFIED'),
    observations: states,
    tools: [compactTool({
      name: 'fcc-claude',
      found: runtime?.found,
      executablePath: runtime?.paths?.[0],
      version: versionFromRun(runtime?.version),
      evidence: 'runtime',
    })],
  };
}

function unitySummary(record, exitCode, artifactPath, authoritativeTarget) {
  const data = record.data;
  const executedOnTarget = Boolean(authoritativeTarget && hasWindowsHost(data));
  const editors = data?.discovery?.editors?.editors ?? [];
  const found = Boolean(data?.discovery?.editors?.found);
  const overall = data?.overallStatus ?? record.state;
  const resultState = !found && data ? 'NOT_INSTALLED' : statusFromExit(exitCode);
  const targetBehaviorObserved = executedOnTarget && data?.evidenceState === 'VERIFIED_ON_AVAILABLE_UNITY_HOST';
  return {
    status: statusFromExit(exitCode),
    resultState,
    reason: overall,
    errorSummary: statusFromExit(exitCode) === 'FAIL' ? overall : null,
    artifactPath,
    executedOnAuthoritativeTarget: executedOnTarget,
    targetBehaviorObserved,
    observationState: data?.evidenceState ?? (executedOnTarget ? 'TARGET_UNVERIFIED' : 'TARGET_UNVERIFIED'),
    observations: {
      overallStatus: data?.overallStatus ?? null,
      selectedEditorVersion: data?.fixture?.selectedEditorVersion ?? null,
      cleanup: data?.fixture?.cleanup ?? null,
    },
    tools: editors.map((editor) => compactTool({
      name: 'Unity Editor',
      found: true,
      executablePath: editor.path,
      version: editor.version ?? editor.versionHint ?? versionFromRun(editor.versionProbe),
      evidence: 'discovery.editors',
    })),
  };
}

function blenderSummary(record, exitCode, artifactPath, authoritativeTarget) {
  const data = record.data;
  const executedOnTarget = Boolean(authoritativeTarget && hasWindowsHost(data));
  const found = Boolean(data?.discovery?.found);
  const overall = data?.overallStatus ?? record.state;
  const resultState = !found && data ? 'NOT_INSTALLED' : statusFromExit(exitCode);
  const targetBehaviorObserved = executedOnTarget && data?.evidenceState === 'VERIFIED_ON_AVAILABLE_BLENDER_HOST';
  const candidates = data?.discovery?.candidates ?? [];
  return {
    status: statusFromExit(exitCode),
    resultState,
    reason: overall,
    errorSummary: statusFromExit(exitCode) === 'FAIL' ? overall : null,
    artifactPath,
    executedOnAuthoritativeTarget: executedOnTarget,
    targetBehaviorObserved,
    observationState: data?.evidenceState ?? 'TARGET_UNVERIFIED',
    observations: { overallStatus: data?.overallStatus ?? null },
    tools: candidates.map((candidate) => compactTool({
      name: 'Blender',
      found: true,
      executablePath: candidate.path,
      version: candidate.version ?? versionFromRun(candidate.versionProbe),
      evidence: 'discovery.candidates',
    })),
  };
}

export function buildTargetEvidenceSummary(options) {
  const repoRoot = options.repoRoot ? path.resolve(options.repoRoot) : null;
  const lane = (key) => ({
    record: readEvidence(options[`${key}File`]),
    exitCode: Number(options[`${key}Exit`]),
    artifactPath: normalizedArtifactPath(options[`${key}File`], repoRoot),
  });
  const fcc = lane('fcc');
  const runtime = lane('runtime');
  const unity = lane('unity');
  const blender = lane('blender');
  for (const [name, item] of Object.entries({ fcc, runtime, unity, blender })) {
    if (!Number.isInteger(item.exitCode)) throw new Error(`${name} exit code is required and must be an integer.`);
  }
  return {
    schemaVersion: 2,
    generatedBy: 'tools/contract-probes/target-evidence-summary.mjs',
    targetAuthorizationDeclared: Boolean(options.authoritativeTarget),
    contracts: {
      fccDiscoveryCli: fccSummary(fcc.record, fcc.exitCode, fcc.artifactPath, options.authoritativeTarget),
      fccStreamingSessionFailure: runtimeSummary(runtime.record, runtime.exitCode, runtime.artifactPath, options.authoritativeTarget),
      unity: unitySummary(unity.record, unity.exitCode, unity.artifactPath, options.authoritativeTarget),
      blender: blenderSummary(blender.record, blender.exitCode, blender.artifactPath, options.authoritativeTarget),
    },
  };
}

function parseArgs(argv) {
  const args = { authoritativeTarget: false };
  const valueFlags = new Set([
    '--repo-root', '--fcc-file', '--fcc-exit', '--runtime-file', '--runtime-exit',
    '--unity-file', '--unity-exit', '--blender-file', '--blender-exit', '--output',
  ]);
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    if (flag === '--authoritative-target') { args.authoritativeTarget = true; continue; }
    if (!valueFlags.has(flag)) throw new Error(`Unknown argument: ${flag}`);
    if (i + 1 >= argv.length) throw new Error(`Missing value for ${flag}`);
    const value = argv[++i];
    const key = flag.slice(2).replace(/-([a-z])/g, (_, c) => c.toUpperCase());
    args[key] = value;
  }
  for (const key of ['fccExit', 'runtimeExit', 'unityExit', 'blenderExit']) {
    if (args[key] == null || !/^-?\d+$/.test(String(args[key]))) throw new Error(`--${key.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`)} is required.`);
    args[key] = Number(args[key]);
  }
  return args;
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  const summary = buildTargetEvidenceSummary(args);
  const json = `${JSON.stringify(summary, null, 2)}\n`;
  if (args.output) {
    const destination = path.resolve(args.output);
    fs.mkdirSync(path.dirname(destination), { recursive: true });
    fs.writeFileSync(destination, json, 'utf8');
    console.log(`TARGET_EVIDENCE_SUMMARY_WRITTEN:${destination}`);
  } else {
    process.stdout.write(json);
  }
}

const isDirect = process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url));
if (isDirect) {
  try { main(); process.exit(EXIT.PASS); }
  catch (error) { console.error(`TARGET_EVIDENCE_SUMMARY_FAIL:${error?.message ?? error}`); process.exit(EXIT.FAIL); }
}
