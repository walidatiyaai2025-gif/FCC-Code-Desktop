import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawn, spawnSync } from 'node:child_process';
import { createHash, randomUUID } from 'node:crypto';

const SECRET = /(token|secret|password|api[_-]?key|authorization|bearer|credential)/i;
export function redact(value, key = '') {
  if (value == null) return value;
  if (SECRET.test(key)) return '[REDACTED]';
  if (Array.isArray(value)) return value.map((x) => redact(x));
  if (typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, redact(v, k)]));
  if (typeof value !== 'string') return value;
  return value
    .replace(/\bsk-[A-Za-z0-9_-]{8,}\b/g, '[REDACTED]')
    .replace(/(Authorization\s*[:=]\s*)[^\r\n,;]+/gi, '$1[REDACTED]')
    .replace(/(Bearer\s+)[A-Za-z0-9._~+/=-]{8,}/gi, '$1[REDACTED]')
    .replace(/\b(token|secret|password|api[_-]?key|credential)\s*[:=]\s*[^\s,;]+/gi, '$1=[REDACTED]');
}

export function sha256(file) {
  const h = createHash('sha256');
  h.update(fs.readFileSync(file));
  return h.digest('hex');
}

export function artifact(file) {
  try {
    const s = fs.statSync(file);
    return {
      path: file,
      exists: true,
      kind: s.isFile() ? 'file' : 'directory',
      size: s.size,
      sha256: s.isFile() ? sha256(file) : null
    };
  } catch {
    return { path: file, exists: false, kind: null, size: null, sha256: null };
  }
}

export function validateArtifact(file, kind) {
  const a = artifact(file);
  let valid = a.exists && a.kind === 'file' && a.size > 0;

  if (valid && kind === 'blend') {
    const headerBytes = fs.readFileSync(file).subarray(0, 17);
    const legacyHeader = headerBytes.subarray(0, 12).toString('ascii');
    const modernHeader = headerBytes.toString('ascii');

    const legacyValid = /^BLENDER[_-][vV][0-9]{3}$/.test(legacyHeader);
    const modernValid = /^BLENDER17-01v[0-9]{4}$/.test(modernHeader);

    valid = legacyValid || modernValid;
  }

  if (valid && kind === 'png') {
    const signature = fs.readFileSync(file).subarray(0, 8);
    const expected = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
    valid = signature.length === expected.length && signature.equals(expected);
  }

  if (valid && kind === 'obj') {
    const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/).map((x) => x.trim()).filter(Boolean);
    const vertices = lines.filter((line) => /^v\s+[-+0-9.eE]+\s+[-+0-9.eE]+\s+[-+0-9.eE]+(?:\s+[-+0-9.eE]+)?$/.test(line));
    const hasFace = lines.some((line) => /^f\s+\S+\s+\S+\s+\S+(?:\s+\S+)*$/.test(line));
    valid = vertices.length >= 3 && hasFace;
  }

  return {
    ...a,
    valid,
    classification: valid ? `${kind.toUpperCase()}_VALID` : `${kind.toUpperCase()}_INVALID`
  };
}

export function validateStructuredResult(file, expected = {}) {
  const a = artifact(file);
  if (!a.exists || a.kind !== 'file' || a.size <= 0) {
    return { ...a, valid: false, classification: 'JSON_RESULT_MISSING', data: null };
  }

  let data;
  try {
    data = JSON.parse(fs.readFileSync(file, 'utf8'));
  } catch {
    return { ...a, valid: false, classification: 'JSON_RESULT_MALFORMED', data: null };
  }

  const baseValid = data
    && data.success === true
    && typeof data.blender === 'string'
    && data.blender.trim().length > 0
    && typeof data.object === 'string'
    && data.object.length > 0
    && typeof data.scene === 'string'
    && typeof data.render === 'string'
    && typeof data.export === 'string';

  const expectedValid = [
    ['scene', expected.scene],
    ['render', expected.render],
    ['export', expected.exported]
  ].every(([key, value]) => value == null || data?.[key] === value);

  const valid = Boolean(baseValid && expectedValid);
  return {
    ...a,
    valid,
    classification: valid ? 'JSON_RESULT_VALID' : 'JSON_RESULT_INVALID',
    data: valid
      ? {
          success: true,
          blender: data.blender,
          object: data.object,
          scene: data.scene,
          render: data.render,
          export: data.export
        }
      : null
  };
}

function which(name) {
  const exts = process.platform === 'win32' ? ['', '.exe'] : [''];
  for (const dir of (process.env.PATH ?? '').split(path.delimiter)) {
    if (!dir) continue;
    for (const ext of exts) {
      const p = path.join(dir, name + ext);
      try {
        if (fs.statSync(p).isFile()) return p;
      } catch {}
    }
  }
  return null;
}

export function discoverBlender(explicit = null) {
  const candidates = [];
  const add = (p, source) => {
    if (p) candidates.push({ path: path.resolve(p), source });
  };

  if (explicit) {
    // An explicitly requested executable is authoritative. Do not silently
    // fall through to another installed Blender when the supplied path is
    // missing or invalid; exact target attribution depends on this.
    add(explicit, 'explicit');
  } else {
    add(process.env.BLENDER_PATH, 'environment');
    add(which('blender'), 'PATH');

    if (process.platform === 'win32') {
      for (const root of [
        process.env.ProgramFiles,
        process.env.LOCALAPPDATA && path.join(process.env.LOCALAPPDATA, 'Programs')
      ].filter(Boolean)) {
        try {
          for (const d of fs.readdirSync(root, { withFileTypes: true })) {
            if (!/^Blender/i.test(d.name)) continue;
            const base = path.join(root, d.name);
            if (/Foundation/i.test(d.name)) {
              for (const v of fs.readdirSync(base, { withFileTypes: true })) {
                if (v.isDirectory()) add(path.join(base, v.name, 'blender.exe'), 'standard-location');
              }
            } else {
              add(path.join(base, 'blender.exe'), 'standard-location');
            }
          }
        } catch {}
      }
    }
  }

  const unique = [...new Map(candidates.map((x) => [path.normalize(x.path).toLowerCase(), x])).values()]
    .filter((x) => {
      try {
        return fs.statSync(x.path).isFile();
      } catch {
        return false;
      }
    });

  for (const item of unique) {
    const r = spawnSync(item.path, ['--version'], {
      encoding: 'utf8',
      timeout: 15000,
      windowsHide: true,
      shell: false
    });
    item.version = `${r.stdout ?? ''}\n${r.stderr ?? ''}`.match(/Blender\s+([0-9][^\s]*)/i)?.[1] ?? null;
    item.versionProbe = {
      exitCode: r.status,
      error: r.error ? String(r.error) : null,
      stdout: redact(r.stdout ?? ''),
      stderr: redact(r.stderr ?? '')
    };
    item.versionObserved = r.status === 0 && item.version != null;
  }

  return { found: unique.length > 0, candidates: unique };
}

export function buildArgs({ script, result, scene, render, exported }) {
  return [
    '--background',
    '--factory-startup',
    '--python',
    script,
    '--',
    '--result',
    result,
    '--scene',
    scene,
    '--render',
    render,
    '--export',
    exported
  ];
}

export function writeFixtureScript(file) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, `import bpy, json, os, sys
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]
def val(k): return args[args.index(k)+1]
result,scene,render,exported=val('--result'),val('--scene'),val('--render'),val('--export')
for p in (result,scene,render,exported): os.makedirs(os.path.dirname(os.path.abspath(p)),exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_cube_add(location=(0,0,0)); cube=bpy.context.object; cube.name='FCCD_Probe_Cube'
mat=bpy.data.materials.new('FCCD_Probe_Material'); mat.diffuse_color=(0.08,0.32,0.8,1); cube.data.materials.append(mat)
bpy.ops.object.camera_add(location=(5,-5,4)); cam=bpy.context.object; bpy.context.scene.camera=cam
def track(o,p): o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
track(cam,(0,0,0)); bpy.ops.object.light_add(type='AREA',location=(3,-2,5)); bpy.context.object.data.energy=1200
bpy.context.scene.render.engine='BLENDER_WORKBENCH'; bpy.context.scene.render.resolution_x=96; bpy.context.scene.render.resolution_y=96; bpy.context.scene.render.resolution_percentage=100; bpy.context.scene.render.filepath=render
bpy.ops.wm.save_as_mainfile(filepath=scene, compress=False); bpy.ops.wm.obj_export(filepath=exported,export_selected_objects=False); bpy.ops.render.render(write_still=True)
with open(result,'w',encoding='utf-8') as f: json.dump({'success':True,'blender':bpy.app.version_string,'object':cube.name,'scene':scene,'render':render,'export':exported},f)
print('FCCD_BLENDER_PROBE_OK')
`, 'utf8');
  return artifact(file);
}

function pidAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return null;
  if (process.platform === 'win32') {
    const r = spawnSync('tasklist.exe', ['/FI', `PID eq ${pid}`, '/FO', 'CSV', '/NH'], {
      encoding: 'utf8',
      windowsHide: true,
      shell: false,
      timeout: 5000
    });
    if (r.error || r.status !== 0) return null;
    return new RegExp(`"${pid}"`).test(r.stdout ?? '');
  }

  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    if (error?.code === 'ESRCH') return false;
    if (error?.code === 'EPERM') return true;
    return null;
  }
}

async function waitForPidGone(pid, timeoutMs = 5000) {
  const deadline = Date.now() + timeoutMs;
  let last = pidAlive(pid);
  while (last === true && Date.now() < deadline) {
    await new Promise((resolve) => setTimeout(resolve, 50));
    last = pidAlive(pid);
  }
  return last === false ? true : last === true ? false : null;
}

export function isControlledNonZeroExit(exitCode) {
  return Number.isInteger(exitCode) && exitCode !== 0;
}

export function runOwned(exe, args, { cwd, timeoutMs = 180000, cancelAfterMs = null } = {}) {
  return new Promise((resolve) => {
    const started = Date.now();
    const child = spawn(exe, args, {
      cwd,
      shell: false,
      windowsHide: true,
      detached: process.platform !== 'win32',
      stdio: ['ignore', 'pipe', 'pipe']
    });

    let stdout = '';
    let stderr = '';
    let cancelled = false;
    let timedOut = false;
    let settled = false;
    let termination = null;
    let timeoutTimer = null;
    let cancelTimer = null;

    child.stdout?.on('data', (d) => stdout += d.toString());
    child.stderr?.on('data', (d) => stderr += d.toString());

    const finish = async (code, signal, error = null) => {
      if (settled) return;
      settled = true;
      if (timeoutTimer) clearTimeout(timeoutTimer);
      if (cancelTimer) clearTimeout(cancelTimer);
      const rootPidGone = child.pid ? await waitForPidGone(child.pid) : null;
      resolve(redact({
        pid: child.pid,
        exitCode: code,
        signal,
        error,
        stdout,
        stderr,
        cancelled,
        timedOut,
        durationMs: Date.now() - started,
        termination,
        cleanupVerification: {
          rootPid: child.pid ?? null,
          rootPidGone
        }
      }));
    };

    child.on('error', (e) => void finish(null, null, String(e)));
    child.on('exit', (c, s) => void finish(c, s));

    const stop = (reason) => {
      if (child.exitCode != null || child.signalCode != null || settled) return;
      if (reason === 'cancel') cancelled = true;
      else timedOut = true;

      if (process.platform === 'win32') {
        const killed = spawnSync('taskkill.exe', ['/PID', String(child.pid), '/T', '/F'], {
          encoding: 'utf8',
          windowsHide: true,
          shell: false,
          timeout: 15000
        });
        termination = {
          strategy: 'TASKKILL_PID_TREE',
          targetPid: child.pid,
          exitCode: killed.status,
          error: killed.error ? String(killed.error) : null,
          stdout: killed.stdout ?? '',
          stderr: killed.stderr ?? ''
        };
      } else {
        let error = null;
        try {
          process.kill(-child.pid, 'SIGKILL');
        } catch (e) {
          error = String(e);
        }
        termination = {
          strategy: 'OWNED_PROCESS_GROUP',
          targetPid: child.pid,
          signal: 'SIGKILL',
          error
        };
      }
    };

    timeoutTimer = setTimeout(() => stop('timeout'), timeoutMs);
    if (cancelAfterMs != null) cancelTimer = setTimeout(() => stop('cancel'), cancelAfterMs);
  });
}

export function disposableRoot(base = os.tmpdir()) {
  const p = path.join(base, `fcc blender probe مسار ${randomUUID()}`);
  fs.mkdirSync(p, { recursive: true });
  return p;
}
