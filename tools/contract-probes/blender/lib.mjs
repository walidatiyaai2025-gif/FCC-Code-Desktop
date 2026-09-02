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
  return value.replace(/\bsk-[A-Za-z0-9_-]{8,}\b/g, '[REDACTED]').replace(/(Authorization\s*[:=]\s*)[^\r\n,;]+/gi, '$1[REDACTED]').replace(/(Bearer\s+)[A-Za-z0-9._~+/=-]{8,}/gi, '$1[REDACTED]');
}

export function sha256(file) { const h = createHash('sha256'); h.update(fs.readFileSync(file)); return h.digest('hex'); }
export function artifact(file) { try { const s = fs.statSync(file); return { path: file, exists: true, kind: s.isFile() ? 'file' : 'directory', size: s.size, sha256: s.isFile() ? sha256(file) : null }; } catch { return { path: file, exists: false, kind: null, size: null, sha256: null }; } }
export function validateArtifact(file, kind) {
  const a = artifact(file); let valid = a.exists && a.kind === 'file' && a.size > 0;
  if (valid && kind === 'blend') valid = fs.readFileSync(file).subarray(0, 7).toString() === 'BLENDER';
  if (valid && kind === 'png') valid = fs.readFileSync(file).subarray(1, 4).toString() === 'PNG';
  if (valid && kind === 'obj') valid = /(^|\n)(v|o|f)\s+/m.test(fs.readFileSync(file, 'utf8'));
  return { ...a, valid, classification: valid ? `${kind.toUpperCase()}_VALID` : `${kind.toUpperCase()}_INVALID` };
}

function which(name) { const exts = process.platform === 'win32' ? ['', '.exe'] : ['']; for (const dir of (process.env.PATH ?? '').split(path.delimiter)) for (const ext of exts) { const p = path.join(dir, name + ext); try { if (fs.statSync(p).isFile()) return p; } catch {} } return null; }
export function discoverBlender(explicit = null) {
  const candidates = [];
  const add = (p, source) => { if (p) candidates.push({ path: path.resolve(p), source }); };
  add(explicit, 'explicit'); add(process.env.BLENDER_PATH, 'environment'); add(which('blender'), 'PATH');
  if (process.platform === 'win32') {
    for (const root of [process.env.ProgramFiles, process.env.LOCALAPPDATA && path.join(process.env.LOCALAPPDATA, 'Programs')].filter(Boolean)) {
      try { for (const d of fs.readdirSync(root, { withFileTypes: true })) if (/^Blender/i.test(d.name)) { const base = path.join(root, d.name); if (/Foundation/i.test(d.name)) { for (const v of fs.readdirSync(base, { withFileTypes: true })) if (v.isDirectory()) add(path.join(base, v.name, 'blender.exe'), 'standard-location'); } else add(path.join(base, 'blender.exe'), 'standard-location'); } } catch {}
    }
  }
  const unique = [...new Map(candidates.map((x) => [path.normalize(x.path).toLowerCase(), x])).values()].filter((x) => { try { return fs.statSync(x.path).isFile(); } catch { return false; } });
  for (const item of unique) { const r = spawnSync(item.path, ['--version'], { encoding: 'utf8', timeout: 15000, windowsHide: true, shell: false }); item.version = `${r.stdout ?? ''}\n${r.stderr ?? ''}`.match(/Blender\s+([0-9][^\s]*)/i)?.[1] ?? null; item.versionProbe = { exitCode: r.status, error: r.error ? String(r.error) : null, stdout: redact(r.stdout ?? ''), stderr: redact(r.stderr ?? '') }; }
  return { found: unique.length > 0, candidates: unique };
}

export function buildArgs({ script, result, scene, render, exported }) { return ['--background', '--factory-startup', '--python', script, '--', '--result', result, '--scene', scene, '--render', render, '--export', exported]; }
export function writeFixtureScript(file) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, `import bpy, json, os, sys\nfrom mathutils import Vector\nargs=sys.argv[sys.argv.index('--')+1:]\ndef val(k): return args[args.index(k)+1]\nresult,scene,render,exported=val('--result'),val('--scene'),val('--render'),val('--export')\nfor p in (result,scene,render,exported): os.makedirs(os.path.dirname(os.path.abspath(p)),exist_ok=True)\nbpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)\nbpy.ops.mesh.primitive_cube_add(location=(0,0,0)); cube=bpy.context.object; cube.name='FCCD_Probe_Cube'\nmat=bpy.data.materials.new('FCCD_Probe_Material'); mat.diffuse_color=(0.08,0.32,0.8,1); cube.data.materials.append(mat)\nbpy.ops.object.camera_add(location=(5,-5,4)); cam=bpy.context.object; bpy.context.scene.camera=cam\ndef track(o,p): o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()\ntrack(cam,(0,0,0)); bpy.ops.object.light_add(type='AREA',location=(3,-2,5)); bpy.context.object.data.energy=1200\nbpy.context.scene.render.engine='BLENDER_WORKBENCH'; bpy.context.scene.render.resolution_x=96; bpy.context.scene.render.resolution_y=96; bpy.context.scene.render.resolution_percentage=100; bpy.context.scene.render.filepath=render\nbpy.ops.wm.save_as_mainfile(filepath=scene); bpy.ops.wm.obj_export(filepath=exported,export_selected_objects=False); bpy.ops.render.render(write_still=True)\nwith open(result,'w',encoding='utf-8') as f: json.dump({'success':True,'blender':bpy.app.version_string,'object':cube.name,'scene':scene,'render':render,'export':exported},f)\nprint('FCCD_BLENDER_PROBE_OK')\n`, 'utf8');
  return artifact(file);
}

export function runOwned(exe, args, { cwd, timeoutMs = 180000, cancelAfterMs = null } = {}) {
  return new Promise((resolve) => { const started = Date.now(); const child = spawn(exe, args, { cwd, shell: false, windowsHide: true, detached: process.platform !== 'win32', stdio: ['ignore', 'pipe', 'pipe'] }); let stdout = '', stderr = '', cancelled = false, timedOut = false; child.stdout.on('data', (d) => stdout += d.toString()); child.stderr.on('data', (d) => stderr += d.toString()); const finish = (code, signal, error = null) => resolve(redact({ pid: child.pid, exitCode: code, signal, error, stdout, stderr, cancelled, timedOut, durationMs: Date.now() - started })); child.on('error', (e) => finish(null, null, String(e))); child.on('exit', (c, s) => finish(c, s)); const stop = (reason) => { if (child.exitCode != null) return; if (reason === 'cancel') cancelled = true; else timedOut = true; if (process.platform === 'win32') spawnSync('taskkill.exe', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true }); else { try { process.kill(-child.pid, 'SIGKILL'); } catch {} } }; setTimeout(() => stop('timeout'), timeoutMs); if (cancelAfterMs != null) setTimeout(() => stop('cancel'), cancelAfterMs); });
}
export function disposableRoot(base = os.tmpdir()) { const p = path.join(base, `fcc blender probe مسار ${randomUUID()}`); fs.mkdirSync(p, { recursive: true }); return p; }
