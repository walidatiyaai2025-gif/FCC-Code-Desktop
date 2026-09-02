#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import os from 'node:os';
import {
  artifact,
  buildArgs,
  discoverBlender,
  disposableRoot,
  isControlledNonZeroExit,
  redact,
  runOwned,
  validateArtifact,
  validateStructuredResult,
  writeFixtureScript
} from './lib.mjs';

const EXIT = { PASS: 0, FAIL: 1, BLOCKED: 2, USAGE: 64 };

function parse(argv) {
  const a = {
    mode: 'all',
    json: null,
    blender: null,
    fixtureRoot: null,
    timeoutMs: 180000,
    cancelAfterMs: 2500,
    keep: false
  };
  for (let i = 0; i < argv.length; i++) {
    const x = argv[i];
    const take = () => argv[++i];
    if (x === '--mode') a.mode = take();
    else if (x === '--json') a.json = take();
    else if (x === '--blender') a.blender = take();
    else if (x === '--fixture-root') a.fixtureRoot = take();
    else if (x === '--timeout-ms') a.timeoutMs = Number(take());
    else if (x === '--cancel-after-ms') a.cancelAfterMs = Number(take());
    else if (x === '--keep-fixture') a.keep = true;
    else throw new Error(`Unknown argument: ${x}`);
  }
  if (!['discovery', 'all'].includes(a.mode)) throw new Error(`Invalid --mode: ${a.mode}`);
  return a;
}

function write(file, data) {
  if (!file) return;
  fs.mkdirSync(path.dirname(path.resolve(file)), { recursive: true });
  fs.writeFileSync(path.resolve(file), JSON.stringify(redact(data), null, 2) + '\n', 'utf8');
}

async function main() {
  let args;
  try {
    args = parse(process.argv.slice(2));
  } catch (e) {
    console.error(String(e));
    process.exit(EXIT.USAGE);
  }

  const discovery = discoverBlender(args.blender);
  const out = {
    schemaVersion: 1,
    probe: 'FCCD_P00_009_BLENDER_CONTRACT',
    capturedAtUtc: new Date().toISOString(),
    host: {
      platform: process.platform,
      arch: process.arch,
      osRelease: os.release(),
      node: process.version
    },
    discovery,
    steps: [],
    operations: [],
    overallStatus: null,
    evidenceState: null
  };
  const step = (name, status, classification, details = {}) => out.steps.push({ name, status, classification, ...details });

  if (!discovery.found) {
    step('blender-discovery', 'BLOCKED', 'BLENDER_NOT_FOUND');
    out.overallStatus = 'BLOCKED_BLENDER_NOT_FOUND';
    out.evidenceState = 'TARGET_UNVERIFIED';
    write(args.json, out);
    console.log(out.overallStatus);
    process.exit(EXIT.BLOCKED);
  }

  const selected = discovery.candidates[0];
  if (!selected.versionObserved) {
    step('blender-discovery', 'FAIL', 'BLENDER_VERSION_UNVERIFIED');
    out.overallStatus = 'FAIL';
    out.evidenceState = 'TARGET_UNVERIFIED';
    write(args.json, out);
    console.log('BLENDER_CONTRACT_FAIL');
    process.exit(EXIT.FAIL);
  }

  step('blender-discovery', 'PASS', 'BLENDER_FOUND_AND_VERSION_OBSERVED', {
    selected: { path: selected.path, source: selected.source, version: selected.version }
  });

  if (args.mode === 'discovery') {
    out.overallStatus = 'PASS';
    out.evidenceState = 'VERIFIED_ON_AVAILABLE_BLENDER_HOST';
    write(args.json, out);
    return;
  }

  const root = disposableRoot(args.fixtureRoot ?? undefined);
  const evidence = path.join(root, 'evidence');
  const script = path.join(root, 'fixture.py');
  const result = path.join(evidence, 'نتيجة result.json');
  const scene = path.join(evidence, 'scene عربي.blend');
  const render = path.join(evidence, 'render Ω.png');
  const exported = path.join(evidence, 'mesh هندسة.obj');
  out.fixture = { root, disposable: true, cleanup: null };

  try {
    const scriptArtifact = writeFixtureScript(script);
    step('fixture-script', 'PASS', 'FIXTURE_WRITTEN', { artifact: scriptArtifact });

    const exe = selected.path;
    const argsList = buildArgs({ script, result, scene, render, exported });
    const run = await runOwned(exe, argsList, { cwd: root, timeoutMs: args.timeoutMs });
    out.operations.push({
      name: 'background-python-scene-render-export',
      args: argsList,
      run
    });

    const structured = validateStructuredResult(result, { scene, render, exported });
    const blend = validateArtifact(scene, 'blend');
    const png = validateArtifact(render, 'png');
    const obj = validateArtifact(exported, 'obj');

    step('background-headless', run.exitCode === 0 ? 'PASS' : 'FAIL', run.exitCode === 0 ? 'BACKGROUND_PASS' : 'BACKGROUND_FAIL');
    step('python-execution', structured.valid ? 'PASS' : 'FAIL', structured.classification, { artifact: structured });
    step('blend-save', blend.valid ? 'PASS' : 'FAIL', blend.classification, { artifact: blend });
    step('render', png.valid ? 'PASS' : 'FAIL', png.classification, { artifact: png });
    step('obj-export', obj.valid ? 'PASS' : 'FAIL', obj.classification, { artifact: obj });

    const bad = await runOwned(
      exe,
      ['--background', '--factory-startup', '--python', path.join(root, 'missing script عربي.py')],
      { cwd: root, timeoutMs: 30000 }
    );
    out.operations.push({ name: 'controlled-python-failure', run: bad });
    const controlledErrorObserved = isControlledNonZeroExit(bad.exitCode);
    step(
      'controlled-error',
      controlledErrorObserved ? 'PASS' : 'FAIL',
      controlledErrorObserved ? 'NONZERO_ERROR_OBSERVED' : 'ERROR_NOT_OBSERVED'
    );

    const cancel = await runOwned(
      exe,
      ['--background', '--factory-startup', '--python-expr', 'import time; time.sleep(60)'],
      { cwd: root, timeoutMs: 60000, cancelAfterMs: args.cancelAfterMs }
    );
    out.operations.push({ name: 'owned-cancellation', run: cancel });
    const cancellationVerified = cancel.cancelled === true && cancel.cleanupVerification?.rootPidGone === true;
    step(
      'cancellation',
      cancellationVerified ? 'PASS' : 'FAIL',
      cancellationVerified ? 'CANCELLED_OWNED_PROCESS_AND_VERIFIED_EXIT' : 'CANCELLATION_CLEANUP_UNVERIFIED'
    );

    out.overallStatus = out.steps.every((x) => x.status === 'PASS') ? 'PASS' : 'FAIL';
    out.evidenceState = 'VERIFIED_ON_AVAILABLE_BLENDER_HOST';
  } catch (e) {
    step('probe-orchestration', 'FAIL', 'PROBE_ABORTED', { error: String(e) });
    out.overallStatus = 'FAIL';
    out.evidenceState = 'VERIFIED_ON_AVAILABLE_BLENDER_HOST';
  } finally {
    if (!args.keep) {
      let err = null;
      for (let i = 0; i < 12; i++) {
        try {
          fs.rmSync(root, { recursive: true, force: true });
          err = null;
          break;
        } catch (e) {
          err = e;
          await new Promise((r) => setTimeout(r, 250));
        }
      }
      out.fixture.cleanup = err ? `FAILED:${err}` : 'REMOVED';
      if (err) out.overallStatus = 'FAIL';
    } else {
      out.fixture.cleanup = 'KEPT';
    }
  }

  write(args.json, out);
  console.log(`BLENDER_CONTRACT_${out.overallStatus}`);
  process.exit(out.overallStatus === 'PASS' ? EXIT.PASS : EXIT.FAIL);
}

await main();
