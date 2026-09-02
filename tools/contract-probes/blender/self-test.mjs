#!/usr/bin/env node
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import {
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

const root = fs.mkdtempSync(path.join(os.tmpdir(), 'fccd-blender-selftest-'));
let pass = 0;
let fail = 0;

const test = async (name, fn) => {
  try {
    await fn();
    console.log(`PASS ${name}`);
    pass++;
  } catch (e) {
    console.error(`FAIL ${name}: ${e}`);
    fail++;
  }
};

const pidIsAlive = (pid) => {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
};

await test('explicit missing executable is classified', () =>
  assert.equal(discoverBlender(path.join(root, 'missing', 'blender.exe')).found, false));

await test('argument builder preserves spaces Arabic and Unicode exactly', () => {
  const input = {
    script: path.join(root, 'script folder', 'fixture عربي.py'),
    result: path.join(root, 'result folder', 'نتيجة Ω.json'),
    scene: path.join(root, 'scene folder', 'مشهد Ω.blend'),
    render: path.join(root, 'render folder', 'صورة Ω.png'),
    exported: path.join(root, 'export folder', 'هندسة Ω.obj')
  };
  assert.deepEqual(buildArgs(input), [
    '--background',
    '--factory-startup',
    '--python',
    input.script,
    '--',
    '--result',
    input.result,
    '--scene',
    input.scene,
    '--render',
    input.render,
    '--export',
    input.exported
  ]);
});

await test('redaction removes key-shaped fields', () =>
  assert.equal(redact({ apiKey: 'secret-value' }).apiKey, '[REDACTED]'));

await test('redaction removes bearer values', () =>
  assert(!redact('Bearer ABCDEFGHIJK').includes('ABCDEFGHIJK')));

await test('redaction removes opaque authorization header values', () => {
  const secret = 'dXNlcjpwYXNzd29yZA==';
  const r = redact(`Authorization: Basic ${secret}`);
  assert(!r.includes(secret));
  assert(r.includes('[REDACTED]'));
});

await test('redaction removes bearer authorization header values', () => {
  const secret = 'eyJhbGciOiJIUzI1NiJ9.payload.signature';
  const r = redact(`Authorization: Bearer ${secret}`);
  assert(!r.includes(secret));
  assert(r.includes('[REDACTED]'));
});

await test('redaction removes secret assignments embedded in paths logs and errors', () => {
  const secret = 'dont-persist-this-value';
  const r = redact(`C:\\probe\\token=${secret}\\failure.log credential:${secret}`);
  assert(!r.includes(secret));
  assert(r.includes('[REDACTED]'));
});

await test('disposable root stays below requested root', () => {
  const disposable = disposableRoot(root);
  assert(disposable.startsWith(root + path.sep));
  assert.notEqual(disposable, root);
});

await test('fixture script is nonempty', () =>
  assert(writeFixtureScript(path.join(root, 'x', 'fixture.py')).size > 0));

await test('missing blend rejected', () =>
  assert.equal(validateArtifact(path.join(root, 'none.blend'), 'blend').valid, false));

await test('valid Blender header accepted', () => {
  const p = path.join(root, 'ok.blend');
  fs.writeFileSync(p, 'BLENDER-v300');
  assert(validateArtifact(p, 'blend').valid);
});

await test('modern 17-byte Blender header accepted', () => {
  const p = path.join(root, 'modern.blend');
  fs.writeFileSync(p, 'BLENDER17-01v0520');
  assert(validateArtifact(p, 'blend').valid);
});

await test('malformed Blender header rejected', () => {
  const p = path.join(root, 'bad-header.blend');
  fs.writeFileSync(p, 'BLENDERgarbage');
  assert.equal(validateArtifact(p, 'blend').valid, false);
});

await test('full PNG signature accepted', () => {
  const p = path.join(root, 'ok.png');
  fs.writeFileSync(p, Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    Buffer.from('payload')
  ]));
  assert(validateArtifact(p, 'png').valid);
});

await test('truncated PNG signature rejected', () => {
  const p = path.join(root, 'truncated.png');
  fs.writeFileSync(p, Buffer.from([137, 80, 78, 71, 1]));
  assert.equal(validateArtifact(p, 'png').valid, false);
});

await test('empty PNG rejected', () => {
  const p = path.join(root, 'empty.png');
  fs.writeFileSync(p, '');
  assert.equal(validateArtifact(p, 'png').valid, false);
});

await test('OBJ geometry accepted', () => {
  const p = path.join(root, 'ok.obj');
  fs.writeFileSync(p, 'o Cube\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n');
  assert(validateArtifact(p, 'obj').valid);
});

await test('OBJ without geometry rejected', () => {
  const p = path.join(root, 'bad.obj');
  fs.writeFileSync(p, '# comment\no Cube\n');
  assert.equal(validateArtifact(p, 'obj').valid, false);
});

await test('OBJ with vertices but no face rejected', () => {
  const p = path.join(root, 'vertices-only.obj');
  fs.writeFileSync(p, 'v 0 0 0\nv 1 0 0\nv 0 1 0\n');
  assert.equal(validateArtifact(p, 'obj').valid, false);
});

await test('structured JSON result accepted only with expected artifact paths', () => {
  const p = path.join(root, 'result-ok.json');
  const expected = {
    scene: path.join(root, 'scene.blend'),
    render: path.join(root, 'render.png'),
    exported: path.join(root, 'mesh.obj')
  };
  fs.writeFileSync(p, JSON.stringify({
    success: true,
    blender: '4.5.0',
    object: 'FCCD_Probe_Cube',
    scene: expected.scene,
    render: expected.render,
    export: expected.exported
  }));
  assert(validateStructuredResult(p, expected).valid);
});

await test('malformed structured JSON result rejected', () => {
  const p = path.join(root, 'result-malformed.json');
  fs.writeFileSync(p, '{"success":true');
  const r = validateStructuredResult(p);
  assert.equal(r.valid, false);
  assert.equal(r.classification, 'JSON_RESULT_MALFORMED');
});

await test('structured JSON success without required fields rejected', () => {
  const p = path.join(root, 'result-incomplete.json');
  fs.writeFileSync(p, JSON.stringify({ success: true }));
  assert.equal(validateStructuredResult(p).valid, false);
});

await test('controlled error requires a real nonzero integer exit', () => {
  assert.equal(isControlledNonZeroExit(2), true);
  assert.equal(isControlledNonZeroExit(0), false);
  assert.equal(isControlledNonZeroExit(null), false);
  assert.equal(isControlledNonZeroExit(undefined), false);
});

await test('controlled Blender Python failure requests explicit nonzero exit', () => {
  const source = fs.readFileSync(new URL('./probe.mjs', import.meta.url), 'utf8');
  assert(source.includes("'--python-exit-code', '17'"));
});

await test('owned cancellation verifies root exit and preserves unrelated process', async () => {
  const unrelated = spawn(process.execPath, ['-e', 'setTimeout(() => {}, 60000)'], {
    shell: false,
    stdio: 'ignore'
  });
  try {
    const result = await runOwned(
      process.execPath,
      ['-e', 'setTimeout(() => {}, 60000)'],
      { cwd: root, timeoutMs: 10000, cancelAfterMs: 100 }
    );
    assert.equal(result.cancelled, true);
    assert.equal(result.cleanupVerification?.rootPidGone, true);
    assert.equal(pidIsAlive(unrelated.pid), true);
    assert.match(result.termination?.strategy ?? '', /TASKKILL_PID_TREE|OWNED_PROCESS_GROUP/);
  } finally {
    if (pidIsAlive(unrelated.pid)) unrelated.kill('SIGKILL');
  }
});

await test('fixture uses factory startup', () =>
  assert(buildArgs({ script: 's', result: 'r', scene: 'b', render: 'p', exported: 'o' }).includes('--factory-startup')));

await test('fixture uses background mode', () =>
  assert.equal(buildArgs({ script: 's', result: 'r', scene: 'b', render: 'p', exported: 'o' })[0], '--background'));

await test('fixture script covers save render export', () => {
  const t = fs.readFileSync(path.join(root, 'x', 'fixture.py'), 'utf8');
  assert(t.includes('save_as_mainfile') && t.includes('compress=False') && t.includes('render(write_still=True)') && t.includes('obj_export'));
});

await test('cancellation implementation contains no kill-by-name', () => {
  const source = fs.readFileSync(new URL('./lib.mjs', import.meta.url), 'utf8');
  assert(!/\btaskkill(?:\.exe)?['"]?\s*,?\s*\[[^\]]*\/IM/i.test(source));
  assert(!/\bpkill\b|\bkillall\b/i.test(source));
  assert(source.includes("'/PID'"));
  assert(source.includes("'/T'"));
});

fs.rmSync(root, { recursive: true, force: true });
console.log(fail ? `SELF_TEST_FAILED ${pass}/${pass + fail}` : `SELF_TEST_VERIFIED ${pass}/${pass + fail}`);
process.exit(fail ? 1 : 0);
