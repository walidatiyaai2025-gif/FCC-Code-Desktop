#!/usr/bin/env node
import { spawn } from 'node:child_process';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const modeIndex = process.argv.indexOf('--mode');
const mode = modeIndex >= 0 ? process.argv[modeIndex + 1] : 'stream';
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

if (mode === 'stream') {
  process.stdout.write('{"type":"assistant_delta","delta":"hel');
  await sleep(20);
  process.stderr.write('stderr-one\n');
  process.stdout.write('lo","session_id":"123e4567-e89b-42d3-a456-426614174000"}\n');
  const arabic = Buffer.from('{"type":"text_delta","text":"مرحبا"}\n', 'utf8');
  process.stdout.write(arabic.subarray(0, arabic.length - 2));
  await sleep(10);
  process.stdout.write(arabic.subarray(arabic.length - 2));
  process.stdout.write('{"type":"mystery_event","payload":{"x":1}}\n');
  process.stdout.write('{bad-json}\n');
  process.stdout.write(`${JSON.stringify({ type: 'large_event', payload: 'x'.repeat(120000) })}\n`);
  process.stdout.write('Authorization: Bearer sk-FAKE');
  await sleep(10);
  process.stdout.write('SECRET123456789\n');
  process.stdout.write('{"type":"final_result","result":"done"}');
  process.exit(0);
}

if (mode === 'nonzero') {
  process.stderr.write('synthetic command failure\n');
  process.exit(17);
}

if (mode === 'rate-limit') {
  process.stderr.write('HTTP 429 Too Many Requests: rate limit reached\n');
  process.exit(1);
}

if (mode === 'sleep-child') {
  setInterval(() => {}, 1000);
}

if (mode === 'late-tree') {
  setTimeout(() => {
    const child = spawn(process.execPath, [fileURLToPath(import.meta.url), '--mode', 'sleep-child'], { stdio: 'ignore', windowsHide: true });
    process.stdout.write(`late_child_pid=${child.pid}\n`);
  }, 600);
  process.on('SIGINT', () => {});
  setInterval(() => {}, 1000);
}

if (mode === 'tree') {
  const child = spawn(process.execPath, [fileURLToPath(import.meta.url), '--mode', 'sleep-child'], { stdio: 'ignore', windowsHide: true });
  process.stdout.write(`child_pid=${child.pid}\n`);
  process.on('SIGINT', () => {});
  setInterval(() => {}, 1000);
}

if (mode === 'sleep') {
  process.on('SIGINT', () => {});
  setInterval(() => {}, 1000);
}
