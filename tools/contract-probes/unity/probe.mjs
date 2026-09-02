#!/usr/bin/env node
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import {
  buildBuildArgs, buildCreateProjectArgs, buildExecuteMethodArgs, buildTestArgs, classifyCompile,
  describeArtifact, detectUnityProject, discoverUnityEditors, discoverUnityHub, EVIDENCE, EXIT,
  makeDisposableFixtureRoot, operationRecord, readUnityLog, resolveEditorForProject, runOwnedProcess,
  sanitizeForPersistence, validateBuildArtifacts, validateJsonResult, validateTestResultXml, waitForFile,
  writeDisposableFixtureSources,
} from './lib.mjs';

function parseArgs(argv) {
  const args = { mode: 'all', json: null, unity: null, hub: null, project: null, fixtureRoot: null, keepFixture: false, timeoutMs: 300000, cancelAfterMs: 3000 };
  const take = (i, flag) => { if (i + 1 >= argv.length) throw new Error(`Missing value for ${flag}`); return argv[i + 1]; };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--mode') args.mode = take(i++, a); else if (a === '--json') args.json = take(i++, a); else if (a === '--unity') args.unity = take(i++, a); else if (a === '--hub') args.hub = take(i++, a); else if (a === '--project') args.project = take(i++, a); else if (a === '--fixture-root') args.fixtureRoot = take(i++, a); else if (a === '--timeout-ms') args.timeoutMs = Number(take(i++, a)); else if (a === '--cancel-after-ms') args.cancelAfterMs = Number(take(i++, a)); else if (a === '--keep-fixture') args.keepFixture = true; else if (a === '--help' || a === '-h') args.help = true; else throw new Error(`Unknown argument: ${a}`);
  }
  if (!['all', 'discovery', 'project'].includes(args.mode)) throw new Error(`Invalid --mode: ${args.mode}`);
  if (!Number.isFinite(args.timeoutMs) || args.timeoutMs < 1000) throw new Error('--timeout-ms must be >= 1000');
  if (!Number.isFinite(args.cancelAfterMs) || args.cancelAfterMs < 250) throw new Error('--cancel-after-ms must be >= 250');
  return args;
}
function usage() {
  console.log(`Unity P00 contract probe\n\nUsage:\n  node probe.mjs [options]\n\nOptions:\n  --mode discovery|project|all\n  --json <output.json>\n  --unity <explicit Unity Editor executable>\n  --hub <explicit Unity Hub executable>\n  --project <existing project to detect only; never mutated by this probe>\n  --fixture-root <safe disposable fixture parent>\n  --keep-fixture\n  --timeout-ms <ms>\n  --cancel-after-ms <ms>\n\nExit codes:\n  0 = requested real Unity contract evidence completed\n  1 = probe infrastructure or observed contract failure\n  2 = Unity unavailable or required target evidence incomplete\n  64 = usage error\n`);
}
function writeJson(filePath, value) { if (!filePath) return; const p = path.resolve(filePath); fs.mkdirSync(path.dirname(p), { recursive: true }); fs.writeFileSync(p, JSON.stringify(sanitizeForPersistence(value), null, 2) + '\n', 'utf8'); }
function addStep(manifest, step) { manifest.steps.push(sanitizeForPersistence(step)); return step; }
async function runOp({ manifest, id, editor, projectPath, args, logPath, timeoutMs, cancelAfterMs = null, classifier, artifacts = [], testResultPath = null, buildOutputPath = null }) {
  const processResult = await runOwnedProcess(editor.path, args, { cwd: projectPath, timeoutMs, cancelAfterMs, operationId: id });
  const logResult = readUnityLog(logPath, processResult); const classification = classifier(processResult, logResult);
  manifest.operations.push(operationRecord({ id, unityVersion: editor.version, executablePath: editor.path, projectPath, args, processResult, logPath, testResultPath, buildOutputPath, artifacts, classification, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }));
  return { processResult, logResult, classification };
}
function statusFromClassification(classification, passSet) { return passSet.includes(classification) ? 'PASS' : 'FAIL'; }

async function main() {
  let args;
  try { args = parseArgs(process.argv.slice(2)); } catch (error) { console.error(String(error)); usage(); process.exit(EXIT.USAGE); }
  if (args.help) { usage(); return; }
  const manifest = {
    schemaVersion: 1, probe: 'FCCD_P00_008_UNITY_CONTRACT', capturedAtUtc: new Date().toISOString(),
    host: { platform: process.platform, arch: process.arch, osRelease: os.release(), node: process.version },
    evidenceState: EVIDENCE.NOT_OBSERVED, discovery: null, suppliedProject: null, versionResolution: null,
    fixture: null, steps: [], operations: [], targetUnverifiedItems: [], overallStatus: 'BLOCKED',
  };

  const hub = discoverUnityHub({ explicitHub: args.hub });
  const editorDiscovery = discoverUnityEditors({ explicitEditor: args.unity, probeVersion: true });
  manifest.discovery = { hub, editors: editorDiscovery };
  addStep(manifest, { name: 'unity-hub-discovery', status: hub.found ? 'PASS' : 'NOT_OBSERVED', classification: hub.found ? 'UNITY_HUB_FOUND' : 'UNITY_HUB_NOT_FOUND', evidenceState: hub.found ? EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST : EVIDENCE.NOT_OBSERVED });
  addStep(manifest, { name: 'unity-editor-discovery', status: editorDiscovery.found ? 'PASS' : 'BLOCKED', classification: editorDiscovery.found ? 'UNITY_EDITOR_FOUND' : 'BLOCKED_UNITY_NOT_FOUND', evidenceState: editorDiscovery.found ? EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST : EVIDENCE.NOT_OBSERVED });

  if (args.project) {
    manifest.suppliedProject = detectUnityProject(args.project);
    manifest.versionResolution = resolveEditorForProject(manifest.suppliedProject, editorDiscovery.editors ?? []);
    addStep(manifest, { name: 'supplied-project-detection', status: manifest.suppliedProject.valid ? 'PASS' : 'FAIL', classification: manifest.suppliedProject.classification, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST });
    addStep(manifest, { name: 'supplied-project-version-resolution', status: manifest.versionResolution.compatibilityStatus === 'EXACT_MATCH' ? 'PASS' : 'BLOCKED', classification: manifest.versionResolution.compatibilityStatus, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST });
  }

  if (!editorDiscovery.found) {
    manifest.targetUnverifiedItems = ['Unity CLI launch', 'log capture', 'compile', 'EditMode tests', 'PlayMode tests', 'executeMethod', 'build', 'project-lock behavior', 'Unity-specific cancellation/process cleanup', 'failure behavior'];
    manifest.overallStatus = 'BLOCKED_UNITY_NOT_FOUND'; manifest.evidenceState = EVIDENCE.TARGET_UNVERIFIED;
    writeJson(args.json, manifest); console.log('BLOCKED_UNITY_NOT_FOUND'); process.exit(EXIT.BLOCKED_OR_INCOMPLETE);
  }
  if (args.mode === 'discovery') { manifest.overallStatus = 'PASS'; manifest.evidenceState = EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST; writeJson(args.json, manifest); console.log('UNITY_DISCOVERY_PASS'); return; }
  if (args.mode === 'project') {
    if (!args.project) { manifest.overallStatus = 'BLOCKED_PROJECT_NOT_SUPPLIED'; manifest.evidenceState = EVIDENCE.TARGET_UNVERIFIED; writeJson(args.json, manifest); console.log('BLOCKED_PROJECT_NOT_SUPPLIED'); process.exit(EXIT.BLOCKED_OR_INCOMPLETE); }
    manifest.overallStatus = manifest.suppliedProject.valid ? 'PASS' : 'FAIL'; manifest.evidenceState = EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST; writeJson(args.json, manifest); process.exit(manifest.suppliedProject.valid ? EXIT.PASS : EXIT.ERROR);
  }

  let editor = null;
  if (args.project && manifest.suppliedProject?.valid) editor = manifest.versionResolution?.selectedEditor ?? null;
  if (!editor) editor = (editorDiscovery.editors ?? [])[0] ?? null;
  if (args.project && manifest.suppliedProject?.valid && !manifest.versionResolution?.selectedEditor) manifest.targetUnverifiedItems.push(`Required project editor ${manifest.suppliedProject.requiredVersion} is not installed; supplied project was not opened or upgraded.`);
  if (!editor) { manifest.overallStatus = 'BLOCKED_UNITY_NOT_FOUND'; writeJson(args.json, manifest); process.exit(EXIT.BLOCKED_OR_INCOMPLETE); }

  const fixtureParent = args.fixtureRoot ? path.resolve(args.fixtureRoot) : os.tmpdir(); fs.mkdirSync(fixtureParent, { recursive: true });
  const fixture = makeDisposableFixtureRoot(fixtureParent); const projectPath = path.join(fixture, 'Unity Project عربي Ω'); const evidenceDir = path.join(fixture, 'evidence'); fs.mkdirSync(evidenceDir, { recursive: true });
  manifest.fixture = { root: fixture, projectPath, disposable: true, kept: args.keepFixture, selectedEditorVersion: editor.version, selectedEditorPath: editor.path };
  let hardFailure = false, incomplete = false;
  try {
    const createLog = path.join(evidenceDir, 'create-project.log'); const createArgs = buildCreateProjectArgs({ projectPath, logPath: createLog });
    const create = await runOp({ manifest, id: 'unity-create-project', editor, projectPath: fixture, args: createArgs, logPath: createLog, timeoutMs: args.timeoutMs, classifier: (pr) => pr.timedOut ? 'TIMEOUT' : pr.launchError ? 'UNITY_STARTUP_FAILURE' : (pr.exitCode === 0 && fs.existsSync(path.join(projectPath, 'ProjectSettings', 'ProjectVersion.txt'))) ? 'PROJECT_CREATE_PASS' : 'PROJECT_CREATE_FAILURE' });
    addStep(manifest, { name: 'disposable-project-create', status: statusFromClassification(create.classification, ['PROJECT_CREATE_PASS']), classification: create.classification, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST });
    if (create.classification !== 'PROJECT_CREATE_PASS') { hardFailure = true; throw new Error('Disposable Unity project creation failed; destructive probe steps were not attempted.'); }

    const detected = detectUnityProject(projectPath);
    addStep(manifest, { name: 'fixture-project-detection', status: detected.valid ? 'PASS' : 'FAIL', classification: detected.classification, details: detected, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST });
    if (!detected.valid) { hardFailure = true; throw new Error('Created fixture is not a valid Unity project.'); }
    const fixtureResolution = resolveEditorForProject(detected, editorDiscovery.editors ?? []);
    addStep(manifest, { name: 'fixture-version-resolution', status: fixtureResolution.compatibilityStatus === 'EXACT_MATCH' ? 'PASS' : 'FAIL', classification: fixtureResolution.compatibilityStatus, details: fixtureResolution, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST });
    if (fixtureResolution.compatibilityStatus !== 'EXACT_MATCH') { hardFailure = true; throw new Error('Created fixture did not resolve to the creating Unity Editor.'); }

    const invalidProject = path.join(fixture, 'definitely-not-a-project'); fs.mkdirSync(invalidProject, { recursive: true }); const invalidDetection = detectUnityProject(invalidProject);
    addStep(manifest, { name: 'invalid-project-classification', status: !invalidDetection.valid ? 'PASS' : 'FAIL', classification: invalidDetection.classification, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST });
    const sources = writeDisposableFixtureSources(projectPath);
    addStep(manifest, { name: 'fixture-bootstrap', status: sources.every((a) => a.exists && a.size > 0) ? 'PASS' : 'FAIL', classification: 'FIXTURE_SOURCES_WRITTEN', artifacts: sources, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST });

    const compileResultPath = path.join(evidenceDir, 'compile-result.json'); const compileLog = path.join(evidenceDir, 'compile.log'); const compileArgs = buildExecuteMethodArgs({ projectPath, logPath: compileLog, method: 'FccUnityProbe.WriteCompileResult', extraArgs: ['--fcc-result', compileResultPath] });
    const compile = await runOp({ manifest, id: 'unity-compile-positive', editor, projectPath, args: compileArgs, logPath: compileLog, timeoutMs: args.timeoutMs, classifier: (pr, lr) => classifyCompile({ processResult: pr, logResult: lr, resultArtifact: validateJsonResult(compileResultPath, (v) => v?.success === true && v?.operation === 'compile-marker') }) });
    const compileArtifact = validateJsonResult(compileResultPath, (v) => v?.success === true && v?.operation === 'compile-marker');
    addStep(manifest, { name: 'compile-positive', status: compile.classification === 'COMPILE_PASS' && compileArtifact.valid ? 'PASS' : 'FAIL', classification: compile.classification, artifact: compileArtifact, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }); if (compile.classification !== 'COMPILE_PASS') hardFailure = true;

    const badFile = path.join(projectPath, 'Assets', 'FccProbeExpectedCompileError.cs'); fs.writeFileSync(badFile, 'public class FccProbeExpectedCompileError { this is intentionally invalid C#; }\n', 'utf8');
    const negativeResultPath = path.join(evidenceDir, 'negative-compile-marker.json'); const negativeLog = path.join(evidenceDir, 'compile-negative.log'); const negativeArgs = buildExecuteMethodArgs({ projectPath, logPath: negativeLog, method: 'FccUnityProbe.WriteCompileResult', extraArgs: ['--fcc-result', negativeResultPath] });
    const negative = await runOp({ manifest, id: 'unity-compile-negative', editor, projectPath, args: negativeArgs, logPath: negativeLog, timeoutMs: args.timeoutMs, classifier: (pr, lr) => classifyCompile({ processResult: pr, logResult: lr, resultArtifact: validateJsonResult(negativeResultPath) }) });
    addStep(manifest, { name: 'compile-negative-controlled', status: negative.classification === 'COMPILE_ERROR' && !fs.existsSync(negativeResultPath) ? 'PASS' : 'FAIL', classification: negative.classification, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }); if (negative.classification !== 'COMPILE_ERROR') hardFailure = true; fs.rmSync(badFile, { force: true });

    const recoveryResult = path.join(evidenceDir, 'compile-recovery.json'); const recoveryLog = path.join(evidenceDir, 'compile-recovery.log'); const recoveryArgs = buildExecuteMethodArgs({ projectPath, logPath: recoveryLog, method: 'FccUnityProbe.WriteCompileResult', extraArgs: ['--fcc-result', recoveryResult] });
    const recovery = await runOp({ manifest, id: 'unity-compile-recovery', editor, projectPath, args: recoveryArgs, logPath: recoveryLog, timeoutMs: args.timeoutMs, classifier: (pr, lr) => classifyCompile({ processResult: pr, logResult: lr, resultArtifact: validateJsonResult(recoveryResult, (v) => v?.success === true) }) });
    addStep(manifest, { name: 'compile-recovery-after-negative', status: recovery.classification === 'COMPILE_PASS' ? 'PASS' : 'FAIL', classification: recovery.classification, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }); if (recovery.classification !== 'COMPILE_PASS') hardFailure = true;

    for (const platform of ['EditMode', 'PlayMode']) {
      const resultsPath = path.join(evidenceDir, `${platform.toLowerCase()}-results.xml`); const logPath = path.join(evidenceDir, `${platform.toLowerCase()}-tests.log`); const testArgs = buildTestArgs({ projectPath, logPath, resultsPath, platform });
      const test = await runOp({ manifest, id: `unity-${platform.toLowerCase()}-tests`, editor, projectPath, args: testArgs, logPath, timeoutMs: args.timeoutMs, testResultPath: resultsPath, classifier: (pr) => { if (pr.timedOut) return 'TIMEOUT'; if (pr.launchError) return 'UNITY_STARTUP_FAILURE'; const v = validateTestResultXml(resultsPath); return v.valid ? 'TEST_PASS' : v.classification; } });
      const validation = validateTestResultXml(resultsPath); addStep(manifest, { name: `${platform.toLowerCase()}-tests`, status: validation.valid ? 'PASS' : 'FAIL', classification: test.classification, artifactValidation: validation, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }); if (!validation.valid) hardFailure = true;
    }

    const automationPath = path.join(evidenceDir, 'automation-marker.json'); const automationLog = path.join(evidenceDir, 'automation.log'); const automationArgs = buildExecuteMethodArgs({ projectPath, logPath: automationLog, method: 'FccUnityProbe.AutomationMarker', extraArgs: ['--fcc-result', automationPath] });
    const automation = await runOp({ manifest, id: 'unity-execute-method', editor, projectPath, args: automationArgs, logPath: automationLog, timeoutMs: args.timeoutMs, classifier: (pr, lr) => { if (pr.timedOut) return 'TIMEOUT'; if (lr.parsed.categories.exception > 0 || pr.exitCode !== 0) return 'EXECUTE_METHOD_FAILURE'; return validateJsonResult(automationPath, (v) => v?.success === true && v?.operation === 'automation-marker').valid ? 'EXECUTE_METHOD_PASS' : 'EXECUTE_METHOD_ARTIFACT_FAILURE'; } });
    const automationArtifact = validateJsonResult(automationPath, (v) => v?.success === true && v?.operation === 'automation-marker'); addStep(manifest, { name: 'editor-automation-execute-method', status: automation.classification === 'EXECUTE_METHOD_PASS' && automationArtifact.valid ? 'PASS' : 'FAIL', classification: automation.classification, artifact: automationArtifact, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }); if (automation.classification !== 'EXECUTE_METHOD_PASS') hardFailure = true;

    const throwLog = path.join(evidenceDir, 'execute-method-failure.log'); const throwArgs = buildExecuteMethodArgs({ projectPath, logPath: throwLog, method: 'FccUnityProbe.ThrowExpectedFailure' });
    const thrown = await runOp({ manifest, id: 'unity-execute-method-negative', editor, projectPath, args: throwArgs, logPath: throwLog, timeoutMs: args.timeoutMs, classifier: (pr, lr) => (pr.exitCode !== 0 || lr.parsed.categories.exception > 0) ? 'EXPECTED_EXECUTE_METHOD_FAILURE_OBSERVED' : 'EXPECTED_EXECUTE_METHOD_FAILURE_NOT_OBSERVED' });
    addStep(manifest, { name: 'execute-method-negative', status: thrown.classification === 'EXPECTED_EXECUTE_METHOD_FAILURE_OBSERVED' ? 'PASS' : 'FAIL', classification: thrown.classification, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }); if (thrown.classification !== 'EXPECTED_EXECUTE_METHOD_FAILURE_OBSERVED') hardFailure = true;

    const buildOutput = path.join(evidenceDir, 'build output عربي', 'FccProbe.exe'); fs.mkdirSync(path.dirname(buildOutput), { recursive: true }); const buildResult = path.join(evidenceDir, 'build-result.json'); const buildLog = path.join(evidenceDir, 'build.log'); const buildArgs = buildBuildArgs({ projectPath, logPath: buildLog, buildOutputPath: buildOutput, buildResultPath: buildResult });
    const build = await runOp({ manifest, id: 'unity-build', editor, projectPath, args: buildArgs, logPath: buildLog, timeoutMs: args.timeoutMs, buildOutputPath: buildOutput, classifier: (pr) => { if (pr.timedOut) return 'TIMEOUT'; if (pr.launchError) return 'UNITY_STARTUP_FAILURE'; return validateBuildArtifacts({ buildResultPath: buildResult, expectedExecutablePath: buildOutput }).valid ? 'BUILD_PASS' : 'BUILD_ARTIFACT_VALIDATION_FAILURE'; } });
    const buildValidation = validateBuildArtifacts({ buildResultPath: buildResult, expectedExecutablePath: buildOutput }); addStep(manifest, { name: 'build-and-artifact-validation', status: buildValidation.valid ? 'PASS' : 'FAIL', classification: build.classification, artifactValidation: buildValidation, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }); if (!buildValidation.valid) hardFailure = true;

    const holdReady = path.join(evidenceDir, 'hold-ready.json'); const holdLog = path.join(evidenceDir, 'hold.log'); const holdArgs = buildExecuteMethodArgs({ projectPath, logPath: holdLog, method: 'FccUnityProbe.HoldOpen', extraArgs: ['--fcc-result', holdReady, '--fcc-hold-ms', '60000'], quit: true });
    const holdPromise = runOwnedProcess(editor.path, holdArgs, { cwd: projectPath, timeoutMs: Math.min(args.timeoutMs, 90000), cancelAfterMs: 45000, operationId: 'unity-project-lock-holder' }); const ready = await waitForFile(holdReady, Math.min(args.timeoutMs, 60000));
    if (ready) {
      const collisionResult = path.join(evidenceDir, 'collision-result.json'); const collisionLog = path.join(evidenceDir, 'collision.log'); const collisionArgs = buildExecuteMethodArgs({ projectPath, logPath: collisionLog, method: 'FccUnityProbe.AutomationMarker', extraArgs: ['--fcc-result', collisionResult] });
      const collision = await runOp({ manifest, id: 'unity-project-lock-collision', editor, projectPath, args: collisionArgs, logPath: collisionLog, timeoutMs: Math.min(args.timeoutMs, 30000), classifier: (pr, lr) => (lr.parsed.categories.projectLock > 0 && !fs.existsSync(collisionResult)) ? 'PROJECT_LOCK_COLLISION_OBSERVED' : (pr.timedOut ? 'PROJECT_LOCK_COLLISION_TIMEOUT' : 'PROJECT_LOCK_BEHAVIOR_UNKNOWN') });
      const lockPass = collision.classification === 'PROJECT_LOCK_COLLISION_OBSERVED'; addStep(manifest, { name: 'same-project-concurrency', status: lockPass ? 'PASS' : 'BLOCKED', classification: collision.classification, evidenceState: lockPass ? EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST : EVIDENCE.TARGET_UNVERIFIED }); if (!lockPass) { incomplete = true; manifest.targetUnverifiedItems.push('Same-project concurrency behavior was not conclusively classified from this Unity host.'); }
    } else { addStep(manifest, { name: 'same-project-concurrency', status: 'BLOCKED', classification: 'LOCK_HOLDER_READY_ARTIFACT_NOT_OBSERVED', evidenceState: EVIDENCE.TARGET_UNVERIFIED }); incomplete = true; manifest.targetUnverifiedItems.push('Project lock holder did not reach ready state; concurrency contract remains unverified.'); }
    const holdResult = await holdPromise; manifest.operations.push(operationRecord({ id: 'unity-project-lock-holder', unityVersion: editor.version, executablePath: editor.path, projectPath, args: holdArgs, processResult: holdResult, logPath: holdLog, artifacts: [describeArtifact(holdReady)], classification: holdResult.cancelled ? 'HOLDER_CANCELLED_AFTER_COLLISION_PROBE' : holdResult.timedOut ? 'HOLDER_TIMED_OUT' : 'HOLDER_EXITED', evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }));

    const cancelReady = path.join(evidenceDir, 'cancel-ready.json'); const cancelLog = path.join(evidenceDir, 'cancel.log'); const cancelArgs = buildExecuteMethodArgs({ projectPath, logPath: cancelLog, method: 'FccUnityProbe.HoldOpen', extraArgs: ['--fcc-result', cancelReady, '--fcc-hold-ms', '60000'], quit: true });
    const cancelProcess = await runOwnedProcess(editor.path, cancelArgs, { cwd: projectPath, timeoutMs: Math.min(args.timeoutMs, 90000), cancelAfterMs: args.cancelAfterMs, forceAfterMs: 3000, operationId: 'unity-cancellation' }); const cancelClassification = cancelProcess.cancelled ? 'CANCELLED_OWNED_PROCESS_EXITED' : cancelProcess.timedOut ? 'TIMEOUT' : 'CANCELLATION_NOT_TRIGGERED';
    manifest.operations.push(operationRecord({ id: 'unity-cancellation', unityVersion: editor.version, executablePath: editor.path, projectPath, args: cancelArgs, processResult: cancelProcess, logPath: cancelLog, artifacts: [describeArtifact(cancelReady)], classification: cancelClassification, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }));
    addStep(manifest, { name: 'cancellation-owned-process-tree', status: cancelProcess.cancelled ? 'PASS' : 'FAIL', classification: cancelClassification, processCleanup: cancelProcess.processCleanup, evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }); if (!cancelProcess.cancelled) hardFailure = true;

    manifest.evidenceState = EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST; manifest.overallStatus = hardFailure ? 'FAIL' : incomplete ? 'BLOCKED' : 'PASS';
  } catch (error) {
    addStep(manifest, { name: 'probe-orchestration', status: 'FAIL', classification: 'PROBE_ABORTED', error: String(error), evidenceState: EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST }); manifest.overallStatus = 'FAIL'; manifest.evidenceState = EVIDENCE.VERIFIED_ON_AVAILABLE_UNITY_HOST;
  } finally {
    if (!args.keepFixture) {
      let cleanupError = null;
      let cleanupAttempts = 0;
      for (let attempt = 0; attempt < 60; attempt++) {
        cleanupAttempts = attempt + 1;
        try {
          fs.rmSync(fixture, { recursive: true, force: true });
          cleanupError = null;
          break;
        } catch (error) {
          cleanupError = error;
          const retryable = ['EBUSY', 'EPERM', 'ENOTEMPTY'].includes(error?.code);
          if (!retryable) break;
          await new Promise((resolve) => setTimeout(resolve, 500));
        }
      }
      manifest.fixture.cleanupAttempts = cleanupAttempts;
      if (cleanupError) { manifest.fixture.cleanup = `FAILED:${String(cleanupError)}`; manifest.overallStatus = 'FAIL'; }
      else manifest.fixture.cleanup = 'REMOVED';
    } else manifest.fixture.cleanup = 'KEPT_BY_EXPLICIT_OPTION';
  }
  writeJson(args.json, manifest); console.log(`UNITY_CONTRACT_${manifest.overallStatus}`); if (manifest.overallStatus === 'PASS') process.exit(EXIT.PASS); if (manifest.overallStatus === 'BLOCKED') process.exit(EXIT.BLOCKED_OR_INCOMPLETE); process.exit(EXIT.ERROR);
}

await main();
