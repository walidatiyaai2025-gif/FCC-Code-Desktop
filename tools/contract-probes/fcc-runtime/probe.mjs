#!/usr/bin/env node
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { randomUUID } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { discoverCapabilityHints, inferPromptArgs, parseArgsTemplate, probeExecutable, redact, redactString } from './common.mjs';
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
    initialSessionCandidates: streaming?.run?.sessionCandidates ?? [], selectedSessionId: null, sessionIdSource: null,
    processExited: streaming?.run ? streaming.run.exitCode != null || streaming.run.signal != null : null,
    resumeCommand: null, resumeRun: null, continuationConfirmed: false, invalidSessionRun: null, duplicateResumeRun: null, limitations: [],
  };
  if (!runtime.found) { result.status = 'BLOCKED_RUNTIME_NOT_FOUND'; return result; }
  if (!args.allowLivePrompt) { result.status = 'BLOCKED_LIVE_PROMPT_NOT_AUTHORIZED'; return result; }
  if (!streaming?.run) { result.status = 'BLOCKED_INITIAL_RUN_MISSING'; return result; }
  const candidates = streaming.run.sessionCandidates ?? [];
  if (!candidates.length) { result.status = 'BLOCKED_SESSION_ID_NOT_OBSERVED'; result.limitations.push('No session identifier was observed; resume syntax is not guessed.'); return result; }
  result.selectedSessionId = candidates[0].value;
  result.sessionIdSource = candidates[0];
  if (!args.resumeArgsJson) { result.status = 'BLOCKED_RESUME_TEMPLATE_NOT_SUPPLIED'; result.limitations.push('Candidate resume/session options may appear in help, but no resume command is executed without an explicit target-observed template.'); return result; }
  const memoryNonce = args.sessionMemoryNonce;
  if (!memoryNonce) { result.status = 'BLOCKED_SESSION_CONTINUITY_NONCE_MISSING'; result.limitations.push('Initial session probe did not include a continuity nonce.'); return result; }
  const resumePrompt = 'Continue the existing session. Reply with exactly the nonce I asked you to remember in the previous turn, and nothing else.';
  const resumeArgs = parseArgsTemplate(args.resumeArgsJson, { sessionId: result.selectedSessionId, prompt: resumePrompt }, '--resume-args-json');
  result.resumeCommand = redact(resumeArgs);
  const cwd = args.workspaceRoot ? path.resolve(args.workspaceRoot) : fs.mkdtempSync(path.join(os.tmpdir(), 'fcc-session-target-'));
  const owns = !args.workspaceRoot;
  try {
    result.resumeRun = await captureProcess(runtime.paths[0], resumeArgs, { cwd, timeoutMs: args.timeoutMs });
    result.continuationConfirmed = `${result.resumeRun.stdout}\n${result.resumeRun.stderr}`.includes(memoryNonce) && result.resumeRun.exitCode === 0;
    const invalidId = `invalid-${randomUUID()}`;
    const invalidArgs = parseArgsTemplate(args.resumeArgsJson, { sessionId: invalidId, prompt: 'This invalid session probe should not silently resume an existing session.' }, '--resume-args-json');
    result.invalidSessionRun = await captureProcess(runtime.paths[0], invalidArgs, { cwd, timeoutMs: args.timeoutMs });
    if (args.exerciseDuplicateResume) result.duplicateResumeRun = await captureProcess(runtime.paths[0], resumeArgs, { cwd, timeoutMs: args.timeoutMs });
    result.status = result.continuationConfirmed ? 'OBSERVED_CONTINUATION_ON_EXECUTION_HOST' : 'OBSERVED_BUT_CONTINUATION_NOT_CONFIRMED';
    result.limitations.push('FCC restart, provider reconnect, and provider/model changes are not forced by this probe.');
    return result;
  } finally {
    if (owns) try { fs.rmSync(cwd, { recursive: true, force: true }); } catch {}
  }
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
  const args = { mode: 'all', json: null, fccClaude: null, allowLivePrompt: false, cliArgsJson: null, streamArgsJson: null, resumeArgsJson: null, workspaceRoot: null, timeoutMs: 45000, cancelAfterMs: 2000, exerciseDuplicateResume: false, prompt: null, cancelPrompt: null };
  const take = (index, flag) => { if (index + 1 >= argv.length) throw new Error(`Missing value for ${flag}`); return argv[index + 1]; };
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    if (flag === '--mode') { args.mode = take(i, flag); i++; }
    else if (flag === '--json') { args.json = take(i, flag); i++; }
    else if (flag === '--fcc-claude') { args.fccClaude = take(i, flag); i++; }
    else if (flag === '--cli-args-json') { args.cliArgsJson = take(i, flag); i++; }
    else if (flag === '--stream-args-json') { args.streamArgsJson = take(i, flag); i++; }
    else if (flag === '--resume-args-json') { args.resumeArgsJson = take(i, flag); i++; }
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
  console.log(`FCC P00 streaming/session/failure contract probe\n\nUsage:\n  node probe.mjs [options]\n\nOptions:\n  --mode all|streaming|session|failure\n  --json <file>\n  --fcc-claude <explicit path>\n  --allow-live-prompt\n  --cli-args-json <json-array>       Generic observed prompt syntax with {prompt}\n  --stream-args-json <json-array>    Observed structured-stream syntax with {prompt}\n  --resume-args-json <json-array>    Observed resume syntax with {sessionId} and {prompt}\n  --workspace-root <safe disposable root>\n  --timeout-ms <ms>\n  --cancel-after-ms <ms>\n  --exercise-duplicate-resume\n\nNo resume or structured-stream flags are guessed. Synthetic self-tests are SELF_TEST_ONLY and are not FCC evidence.`);
}

function overallExit(output, mode) {
  const selected = [];
  if (mode === 'all' || mode === 'streaming') selected.push(output.streaming);
  if (mode === 'all' || mode === 'session') selected.push(output.session);
  if (mode === 'all' || mode === 'failure') selected.push(output.failure);
  const targetComplete = selected.every((item) => item && /OBSERVED/.test(item.status) && !/^BLOCKED/.test(item.status));
  if (!targetComplete) return EXIT.BLOCKED_OR_INCOMPLETE;
  if (output.session && output.session.status === 'OBSERVED_BUT_CONTINUATION_NOT_CONFIRMED') return EXIT.BLOCKED_OR_INCOMPLETE;
  return EXIT.PASS;
}

export async function runProbe(args) {
  const runtime = probeExecutable('fcc-claude', args.fccClaude);
  const output = {
    schemaVersion: 1, probe: 'fcc-p00-stream-session-failure', probeId: randomUUID(), capturedAtUtc: new Date().toISOString(),
    host: { platform: process.platform, arch: process.arch, osType: os.type(), osRelease: os.release(), node: process.version },
    evidenceStatus: process.platform === 'win32' ? 'EXECUTION_HOST_WINDOWS' : 'EXECUTION_HOST_NOT_PROJECT_TARGET', runtime: redact(runtime), streaming: null, session: null, failure: null,
  };
  if (args.mode === 'all' || args.mode === 'session') {
    args.sessionMemoryNonce = `SESSION_MEMORY_${randomUUID()}`;
    const prefix = args.prompt ? `${args.prompt}\n\n` : '';
    args.prompt = `${prefix}For this session-continuity probe, remember this nonce for the next turn: ${args.sessionMemoryNonce}. Reply only READY.`;
  }
  let streaming = null;
  if (args.mode === 'all' || args.mode === 'streaming' || args.mode === 'session' || args.mode === 'failure') streaming = await buildStreamingProbe(args, runtime);
  if (args.mode === 'all' || args.mode === 'streaming') output.streaming = streaming;
  if (args.mode === 'all' || args.mode === 'session') output.session = await buildSessionProbe(args, runtime, streaming);
  if (args.mode === 'all' || args.mode === 'failure') output.failure = await buildFailureProbe(args, runtime, streaming);
  return redact(output);
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
