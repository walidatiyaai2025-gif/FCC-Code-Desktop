#!/usr/bin/env node
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { randomUUID } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { discoverCapabilityHints, inferPromptArgs, parseArgsTemplate, probeExecutable, redact, redactString, runSync } from './common.mjs';
import { captureMissingRuntime, captureProcess } from './stream-capture.mjs';

export { maskSecretsPreserveLength, redactString } from './common.mjs';
export { analyzeLine, captureMissingRuntime, captureProcess, classifyFailure, extractSessionCandidatesFromJson, extractSessionCandidatesFromText } from './stream-capture.mjs';
export const EXIT = Object.freeze({ PASS: 0, ERROR: 1, BLOCKED_OR_INCOMPLETE: 2, USAGE: 64 });

function resolveInvocation(runtime, args, prompt) {
  const helpText = `${runtime.help?.stdout ?? ''}\n${runtime.help?.stderr ?? ''}`;
  const hints = discoverCapabilityHints(helpText);
  const explicit = parseArgsTemplate(args.streamArgsJson ?? args.cliArgsJson, { prompt }, args.streamArgsJson ? '--stream-args-json' : '--cli-args-json');
  const inferred = explicit ? null : inferPromptArgs(helpText, prompt);
  return {
    helpText: redactString(helpText), hints, strategy: explicit ? 'EXPLICIT_OBSERVED_TEMPLATE' : inferred.strategy, args: explicit ?? inferred.args,
    structuredStreamingRequested: Boolean(args.streamArgsJson),
    note: explicit ? 'Explicit target-observed template supplied.' : 'No structured-streaming flags are guessed; generic prompt syntax is used only if safely inferred from help.',
  };
}

export function hasExactSuccessfulResult(run, expected) {
  return (run?.lineEvents ?? []).some((event) => event.classification === 'JSON_EVENT'
    && event.parsed?.type === 'result'
    && event.parsed?.subtype === 'success'
    && event.parsed?.is_error === false
    && String(event.parsed?.result ?? '').trim() === expected);
}

function authoritativeSessionCandidate(run) {
  return (run?.sessionCandidates ?? []).find((candidate) => candidate.source === 'json-key' && /session.*id/i.test(candidate.path ?? ''))
    ?? (run?.sessionCandidates ?? [])[0]
    ?? null;
}

async function probeLoopbackHealth(url) {
  if (!url) return { status: 'NOT_REQUESTED' };
  let parsed;
  try { parsed = new URL(url); } catch { return { status: 'INVALID_URL', url: redactString(url) }; }
  if (!['127.0.0.1', 'localhost', '::1'].includes(parsed.hostname)) return { status: 'REFUSED_NON_LOOPBACK', url: parsed.toString() };
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 4000);
  try {
    const response = await fetch(parsed, { signal: controller.signal, headers: { Accept: 'application/json,text/plain;q=0.8,*/*;q=0.1' } });
    const body = redactString((await response.text()).slice(0, 12000));
    return { status: response.ok ? 'HEALTHY' : 'UNHEALTHY', url: parsed.toString(), httpStatus: response.status, body };
  } catch (error) {
    return { status: 'UNREACHABLE', url: parsed.toString(), error: redactString(String(error)) };
  } finally { clearTimeout(timer); }
}

async function buildStreamingProbe(args, runtime) {
  const result = { status: 'TARGET_UNVERIFIED', runtimeFound: runtime.found, livePromptAllowed: args.allowLivePrompt, invocation: null, run: null, observations: {} };
  if (!runtime.found) { result.status = 'BLOCKED_RUNTIME_NOT_FOUND'; return result; }
  const prompt = args.prompt ?? `Reply briefly. Include this nonce exactly once: STREAM_${randomUUID()}`;
  result.invocation = resolveInvocation(runtime, args, prompt);
  if (!result.invocation.args) { result.status = 'BLOCKED_NO_SAFE_INVOCATION'; return result; }
  if (!args.allowLivePrompt) { result.status = 'BLOCKED_LIVE_PROMPT_NOT_AUTHORIZED'; return result; }
  const cwd = args.workspaceRoot ? path.resolve(args.workspaceRoot) : fs.mkdtempSync(path.join(os.tmpdir(), 'fcc-stream-target-'));
  const owns = !args.workspaceRoot;
  try {
    result.run = await captureProcess(runtime.paths[0], result.invocation.args, { cwd, timeoutMs: args.timeoutMs });
    const counts = {};
    for (const event of result.run.lineEvents ?? []) counts[event.classification] = (counts[event.classification] ?? 0) + 1;
    result.observations = {
      lineClassifications: counts,
      eventTypeHints: [...new Set((result.run.lineEvents ?? []).map((e) => e.eventTypeHint).filter(Boolean))],
      semanticHints: [...new Set((result.run.lineEvents ?? []).flatMap((e) => e.semanticHints ?? []))],
      sessionCandidates: result.run.sessionCandidates ?? [],
      stdoutFrameCount: (result.run.rawFrames ?? []).filter((x) => x.stream === 'stdout').length,
      stderrFrameCount: (result.run.rawFrames ?? []).filter((x) => x.stream === 'stderr').length,
      malformedFrameCount: (result.run.lineEvents ?? []).filter((x) => x.classification === 'MALFORMED_JSON').length,
      outputTruncated: Boolean(result.run.outputTruncated),
    };
    result.status = result.run.exitCode === 0 ? 'OBSERVED_ON_EXECUTION_HOST' : 'OBSERVED_FAILURE_ON_EXECUTION_HOST';
    return result;
  } finally {
    if (owns) try { fs.rmSync(cwd, { recursive: true, force: true }); } catch {}
  }
}

async function buildSessionProbe(args, runtime, streaming) {
  const result = {
    status: 'TARGET_UNVERIFIED', helpSessionOptionHints: streaming?.invocation?.hints?.sessionOptions ?? discoverCapabilityHints(`${runtime.help?.stdout ?? ''}\n${runtime.help?.stderr ?? ''}`).sessionOptions,
    firstTurnCommand: streaming?.invocation?.args ?? null, firstTurnExpectedMarker: args.firstTurnExpectedMarker ?? null,
    firstTurnMarkerConfirmed: false, initialSessionCandidates: streaming?.run?.sessionCandidates ?? [], selectedSessionId: null, sessionIdSource: null,
    firstProcessPid: streaming?.run?.pid ?? null, firstProcessExitCode: streaming?.run?.exitCode ?? null,
    firstProcessExited: streaming?.run ? streaming.run.exitCode != null || streaming.run.signal != null : null,
    continueCommand: null, continueRun: null, continueConfirmed: false,
    resumeCommand: null, resumeRun: null, resumedSessionId: null, sameSessionIdExposed: false, continuationConfirmed: false,
    invalidSessionCommand: null, invalidSessionRun: null, invalidSessionRejected: false,
    postInvalidResumeCommand: null, postInvalidResumeRun: null, validSessionIntactAfterInvalid: false,
    cwdProjectBehavior: null, processCleanupObserved: false, duplicateResumeRun: null, limitations: [],
  };
  if (!runtime.found) { result.status = 'BLOCKED_RUNTIME_NOT_FOUND'; return result; }
  if (!args.allowLivePrompt) { result.status = 'BLOCKED_LIVE_PROMPT_NOT_AUTHORIZED'; return result; }
  if (!streaming?.run) { result.status = 'BLOCKED_INITIAL_RUN_MISSING'; return result; }
  result.firstTurnMarkerConfirmed = hasExactSuccessfulResult(streaming.run, args.firstTurnExpectedMarker);
  if (!result.firstTurnMarkerConfirmed || streaming.run.exitCode !== 0) { result.status = 'OBSERVED_BUT_FIRST_TURN_NOT_CONFIRMED'; return result; }
  const candidates = streaming.run.sessionCandidates ?? [];
  if (!candidates.length) { result.status = 'BLOCKED_SESSION_ID_NOT_OBSERVED'; result.limitations.push('No session identifier was observed; resume syntax is not guessed.'); return result; }
  const authoritativeCandidate = authoritativeSessionCandidate(streaming.run);
  result.selectedSessionId = authoritativeCandidate.value;
  result.sessionIdSource = authoritativeCandidate;
  if (!args.resumeArgsJson) { result.status = 'BLOCKED_RESUME_TEMPLATE_NOT_SUPPLIED'; result.limitations.push('Candidate resume/session options may appear in help, but no resume command is executed without an explicit target-observed template.'); return result; }
  const memoryNonce = args.sessionMemoryNonce;
  if (!memoryNonce) { result.status = 'BLOCKED_SESSION_CONTINUITY_NONCE_MISSING'; result.limitations.push('Initial session probe did not include a continuity nonce.'); return result; }
  const continueExpected = `CONTINUE_OK:${memoryNonce}`;
  const resumeExpected = `RESUME_OK:${memoryNonce}`;
  const postInvalidExpected = `POST_INVALID_OK:${memoryNonce}`;
  const initialCwd = streaming.run.cwd;
  const resumeCwd = args.sessionResumeCwd ?? initialCwd;
  fs.mkdirSync(resumeCwd, { recursive: true });
  if (args.continueArgsJson) {
    const continuePrompt = 'Continue the most recent conversation for this working directory. Recover the exact token from the prior turn without being shown it again. Reply with CONTINUE_OK: followed immediately by that token, and nothing else.';
    const continueArgs = parseArgsTemplate(args.continueArgsJson, { prompt: continuePrompt }, '--continue-args-json');
    result.continueCommand = redact(continueArgs);
    result.continueRun = await captureProcess(runtime.paths[0], continueArgs, { cwd: initialCwd, timeoutMs: args.timeoutMs });
    result.continueConfirmed = hasExactSuccessfulResult(result.continueRun, continueExpected);
  } else {
    result.limitations.push('--continue was exposed by help but no explicit observed template was supplied, so it was not executed.');
  }
  const resumePrompt = 'Resume the specified session in a new process and different working directory. Recover the exact token from the first turn without being shown it again. Reply with RESUME_OK: followed immediately by that token, and nothing else.';
  const resumeArgs = parseArgsTemplate(args.resumeArgsJson, { sessionId: result.selectedSessionId, prompt: resumePrompt }, '--resume-args-json');
  result.resumeCommand = redact(resumeArgs);
  result.resumeRun = await captureProcess(runtime.paths[0], resumeArgs, { cwd: resumeCwd, timeoutMs: args.timeoutMs });
  result.continuationConfirmed = hasExactSuccessfulResult(result.resumeRun, resumeExpected);
  const resumedCandidate = authoritativeSessionCandidate(result.resumeRun);
  result.resumedSessionId = resumedCandidate?.value ?? null;
  result.sameSessionIdExposed = result.resumedSessionId === result.selectedSessionId;
  const invalidId = randomUUID();
  const invalidArgs = parseArgsTemplate(args.resumeArgsJson, { sessionId: invalidId, prompt: 'This nonexistent UUID must not resume any valid session.' }, '--resume-args-json');
  result.invalidSessionCommand = redact(invalidArgs);
  result.invalidSessionRun = await captureProcess(runtime.paths[0], invalidArgs, { cwd: resumeCwd, timeoutMs: args.timeoutMs });
  result.invalidSessionRejected = result.invalidSessionRun.exitCode !== 0 && result.invalidSessionRun.classification === 'INVALID_SESSION';
  const postInvalidPrompt = 'Resume the valid session after the separate invalid-session attempt. Recover the exact token from the first turn without being shown it again. Reply with POST_INVALID_OK: followed immediately by that token, and nothing else.';
  const postInvalidArgs = parseArgsTemplate(args.resumeArgsJson, { sessionId: result.selectedSessionId, prompt: postInvalidPrompt }, '--resume-args-json');
  result.postInvalidResumeCommand = redact(postInvalidArgs);
  result.postInvalidResumeRun = await captureProcess(runtime.paths[0], postInvalidArgs, { cwd: resumeCwd, timeoutMs: args.timeoutMs });
  result.validSessionIntactAfterInvalid = hasExactSuccessfulResult(result.postInvalidResumeRun, postInvalidExpected);
  if (args.exerciseDuplicateResume) result.duplicateResumeRun = await captureProcess(runtime.paths[0], resumeArgs, { cwd: resumeCwd, timeoutMs: args.timeoutMs });
  result.cwdProjectBehavior = {
    initialWorkingDirectory: initialCwd, resumeWorkingDirectory: resumeCwd,
    workingDirectoryChanged: path.resolve(initialCwd) !== path.resolve(resumeCwd),
    continuityAcrossDifferentWorkingDirectory: result.continuationConfirmed,
  };
  const ownedRuns = [streaming.run, result.continueRun, result.resumeRun, result.invalidSessionRun, result.postInvalidResumeRun].filter(Boolean);
  result.processCleanupObserved = ownedRuns.every((run) => run.processTreeCleanupObserved === true);
  const continueRequirementPassed = !args.continueArgsJson || result.continueConfirmed;
  const passed = result.firstTurnMarkerConfirmed && result.firstProcessExited && result.continuationConfirmed
    && result.invalidSessionRejected && result.validSessionIntactAfterInvalid && result.processCleanupObserved && continueRequirementPassed;
  result.status = passed ? 'VERIFIED_SESSION_CONTINUITY_ON_WINDOWS_TARGET' : 'OBSERVED_BUT_CONTINUATION_NOT_CONFIRMED';
  result.limitations.push('FCC server restart and provider/model changes were not required by the task-local closure contract and were not forced.');
  return result;
}

async function buildFailureProbe(args, runtime, streaming) {
  const result = {
    status: 'TARGET_UNVERIFIED', unavailableRuntime: await captureMissingRuntime(path.join(os.tmpdir(), `missing-fcc-${randomUUID()}`, 'fcc-claude')),
    liveCancellation: null, observedFailureFromStreaming: streaming?.run?.failure ?? null, rateLimit: 'NOT_OBSERVED_ON_TARGET',
    unsafeNegativeTestsSkipped: ['real credential revocation/auth corruption', 'real configuration corruption', 'artificial provider load to force rate limiting', 'forced invalid provider/model unless safely supplied by target environment'],
  };
  if (!runtime.found) { result.status = 'BLOCKED_RUNTIME_NOT_FOUND'; return result; }
  if (!args.allowLivePrompt) { result.status = 'BLOCKED_LIVE_PROMPT_NOT_AUTHORIZED'; return result; }
  const prompt = args.cancelPrompt ?? 'Produce a detailed but harmless explanation of software contract testing. This run may be interrupted by the probe.';
  const invocation = resolveInvocation(runtime, args, prompt);
  if (!invocation.args) { result.status = 'BLOCKED_NO_SAFE_INVOCATION'; return result; }
  const cwd = args.workspaceRoot ? path.resolve(args.workspaceRoot) : fs.mkdtempSync(path.join(os.tmpdir(), 'fcc-failure-target-'));
  const owns = !args.workspaceRoot;
  try {
    result.liveCancellation = await captureProcess(runtime.paths[0], invocation.args, { cwd, timeoutMs: args.timeoutMs, cancelAfterMs: args.cancelAfterMs });
    const classifications = [streaming?.run?.classification, result.liveCancellation?.classification].filter(Boolean);
    if (classifications.includes('RATE_LIMITED')) result.rateLimit = 'OBSERVED_ON_EXECUTION_HOST';
    result.status = 'OBSERVED_ON_EXECUTION_HOST';
    return result;
  } finally {
    if (owns) try { fs.rmSync(cwd, { recursive: true, force: true }); } catch {}
  }
}

function parseCliArgs(argv) {
  const args = { mode: 'all', json: null, fccClaude: null, fccHealthUrl: null, allowLivePrompt: false, cliArgsJson: null, streamArgsJson: null, resumeArgsJson: null, continueArgsJson: null, workspaceRoot: null, sessionResumeCwd: null, timeoutMs: 45000, cancelAfterMs: 2000, exerciseDuplicateResume: false, prompt: null, cancelPrompt: null };
  const take = (index, flag) => { if (index + 1 >= argv.length) throw new Error(`Missing value for ${flag}`); return argv[index + 1]; };
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    if (flag === '--mode') { args.mode = take(i, flag); i++; }
    else if (flag === '--json') { args.json = take(i, flag); i++; }
    else if (flag === '--fcc-claude') { args.fccClaude = take(i, flag); i++; }
    else if (flag === '--fcc-health-url') { args.fccHealthUrl = take(i, flag); i++; }
    else if (flag === '--cli-args-json') { args.cliArgsJson = take(i, flag); i++; }
    else if (flag === '--stream-args-json') { args.streamArgsJson = take(i, flag); i++; }
    else if (flag === '--resume-args-json') { args.resumeArgsJson = take(i, flag); i++; }
    else if (flag === '--continue-args-json') { args.continueArgsJson = take(i, flag); i++; }
    else if (flag === '--workspace-root') { args.workspaceRoot = take(i, flag); i++; }
    else if (flag === '--timeout-ms') { args.timeoutMs = Number(take(i, flag)); i++; }
    else if (flag === '--cancel-after-ms') { args.cancelAfterMs = Number(take(i, flag)); i++; }
    else if (flag === '--prompt') { args.prompt = take(i, flag); i++; }
    else if (flag === '--cancel-prompt') { args.cancelPrompt = take(i, flag); i++; }
    else if (flag === '--allow-live-prompt') args.allowLivePrompt = true;
    else if (flag === '--exercise-duplicate-resume') args.exerciseDuplicateResume = true;
    else if (flag === '--help' || flag === '-h') args.help = true;
    else throw new Error(`Unknown argument: ${flag}`);
  }
  if (!['all', 'streaming', 'session', 'failure'].includes(args.mode)) throw new Error(`Invalid --mode ${args.mode}`);
  if (!Number.isFinite(args.timeoutMs) || args.timeoutMs < 500) throw new Error('--timeout-ms must be >= 500');
  if (!Number.isFinite(args.cancelAfterMs) || args.cancelAfterMs < 100) throw new Error('--cancel-after-ms must be >= 100');
  return args;
}

function usage() {
  console.log(`FCC P00 streaming/session/failure contract probe\n\nUsage:\n  node probe.mjs [options]\n\nOptions:\n  --mode all|streaming|session|failure\n  --json <file>\n  --fcc-claude <explicit path>\n  --fcc-health-url <loopback URL>\n  --allow-live-prompt\n  --cli-args-json <json-array>       Generic observed prompt syntax with {prompt}\n  --stream-args-json <json-array>    Observed structured-stream syntax with {prompt}\n  --resume-args-json <json-array>    Observed resume syntax with {sessionId} and {prompt}\n  --continue-args-json <json-array>  Observed continue syntax with {prompt}\n  --workspace-root <safe disposable root>\n  --timeout-ms <ms>\n  --cancel-after-ms <ms>\n  --exercise-duplicate-resume\n\nNo resume, continue, or structured-stream flags are guessed. Synthetic self-tests are SELF_TEST_ONLY and are not FCC evidence.`);
}

function overallExit(output, mode) {
  const selected = [];
  if (mode === 'all' || mode === 'streaming') selected.push(output.streaming);
  if (mode === 'all' || mode === 'session') selected.push(output.session);
  if (mode === 'all' || mode === 'failure') selected.push(output.failure);
  const targetComplete = selected.every((item) => item && /(OBSERVED|VERIFIED)/.test(item.status) && !/^BLOCKED/.test(item.status));
  if (!targetComplete) return EXIT.BLOCKED_OR_INCOMPLETE;
  if (output.session && output.session.status === 'OBSERVED_BUT_CONTINUATION_NOT_CONFIRMED') return EXIT.BLOCKED_OR_INCOMPLETE;
  return EXIT.PASS;
}

export async function runProbe(args) {
  const runtime = probeExecutable('fcc-claude', args.fccClaude);
  const shaProbe = runSync('git', ['rev-parse', 'HEAD'], { timeoutMs: 5000 });
  let ownedSessionRoot = null;
  if ((args.mode === 'all' || args.mode === 'session') && !args.workspaceRoot) {
    ownedSessionRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'fcc-p00-004-session-'));
    args.workspaceRoot = path.join(ownedSessionRoot, 'initial project path with spaces');
    args.sessionResumeCwd = path.join(ownedSessionRoot, 'different resume path');
    fs.mkdirSync(args.workspaceRoot, { recursive: true });
    fs.mkdirSync(args.sessionResumeCwd, { recursive: true });
  }
  const output = {
    schemaVersion: 1, probe: 'fcc-p00-stream-session-failure', probeId: randomUUID(), capturedAtUtc: new Date().toISOString(),
    testedSourceSha: shaProbe.exitCode === 0 ? shaProbe.stdout.trim() : null,
    host: { platform: process.platform, arch: process.arch, osType: os.type(), osRelease: os.release(), node: process.version },
    evidenceStatus: process.platform === 'win32' ? 'EXECUTION_HOST_WINDOWS' : 'EXECUTION_HOST_NOT_PROJECT_TARGET',
    fccServerHealth: await probeLoopbackHealth(args.fccHealthUrl), runtime: redact(runtime), providerStatus: 'NOT_OBSERVED', streaming: null, session: null, failure: null,
  };
  if (args.mode === 'all' || args.mode === 'session') {
    args.sessionMemoryNonce = `FCCD_P00_004_MEMORY_${randomUUID()}`;
    args.firstTurnExpectedMarker = `FIRST_TURN_OK:${args.sessionMemoryNonce}`;
    const prefix = args.prompt ? `${args.prompt}\n\n` : '';
    args.prompt = `${prefix}Remember this exact token for later turns: ${args.sessionMemoryNonce}. Reply with exactly ${args.firstTurnExpectedMarker} and nothing else.`;
  }
  try {
    let streaming = null;
    if (args.mode === 'all' || args.mode === 'streaming' || args.mode === 'session' || args.mode === 'failure') streaming = await buildStreamingProbe(args, runtime);
    if (args.mode === 'all' || args.mode === 'streaming' || args.mode === 'session') output.streaming = streaming;
    if (args.mode === 'all' || args.mode === 'session') output.session = await buildSessionProbe(args, runtime, streaming);
    if (args.mode === 'all' || args.mode === 'failure') output.failure = await buildFailureProbe(args, runtime, streaming);
    if (output.session?.firstTurnMarkerConfirmed) output.providerStatus = 'AVAILABLE_PROVIDER_BACKED_COMPLETION';
    else if (streaming?.run) output.providerStatus = `OBSERVED_${streaming.run.classification}`;
    return redact(output);
  } finally {
    if (ownedSessionRoot) try { fs.rmSync(ownedSessionRoot, { recursive: true, force: true }); } catch {}
  }
}

async function main() {
  let args;
  try { args = parseCliArgs(process.argv.slice(2)); }
  catch (error) { console.error(`Usage error: ${redactString(error.message)}`); usage(); process.exit(EXIT.USAGE); }
  if (args.help) { usage(); process.exit(EXIT.PASS); }
  try {
    const output = await runProbe(args);
    if (args.json) {
      const destination = path.resolve(args.json);
      fs.mkdirSync(path.dirname(destination), { recursive: true });
      fs.writeFileSync(destination, `${JSON.stringify(output, null, 2)}\n`, { encoding: 'utf8', mode: 0o600 });
    }
    console.log(JSON.stringify({ runtimeFound: output.runtime.found, streaming: output.streaming?.status ?? 'NOT_REQUESTED', session: output.session?.status ?? 'NOT_REQUESTED', failure: output.failure?.status ?? 'NOT_REQUESTED', outputFile: args.json ? path.resolve(args.json) : null }, null, 2));
    process.exit(overallExit(output, args.mode));
  } catch (error) {
    console.error(`Probe failed: ${redactString(String(error))}`);
    process.exit(EXIT.ERROR);
  }
}

const isDirect = process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url));
if (isDirect) await main();
