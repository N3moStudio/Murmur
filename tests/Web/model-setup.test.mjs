import {readFile} from 'node:fs/promises';
import test from 'node:test';
import assert from 'node:assert/strict';
const source = await readFile(new URL('../../src/Murmur/web/model-setup.js', import.meta.url), 'utf8');
const {ModelDownloads, dictationFiles, downloadProgress, modelSetupMarkup} = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
const helpers = {
  escape: value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])),
  size: bytes => `${Math.ceil(bytes / 1e6)} MB`, name: file => file,
  busy: label => `<span class="busy">${label}</span>`
};
const deferred = () => { let resolve, reject; const promise = new Promise((a,b) => {resolve=a;reject=b;}); return {promise,resolve,reject}; };
const tick = () => new Promise(resolve => setImmediate(resolve));

test('individual button requests only its component', async () => {
  const calls = [], q = new ModelDownloads({download: async file => calls.push(file), installed: () => false});
  await q.enqueue([dictationFiles[2]]);
  assert.deepEqual(calls, [dictationFiles[2]]); assert.equal(q.ready(dictationFiles[2]), true); assert.equal(q.busy, false);
});
test('download all skips installed files and deduplicates overlapping clicks', async () => {
  const calls = [], gates = [deferred(),deferred()]; let concurrent = 0, max = 0;
  const q = new ModelDownloads({installed: file => file === dictationFiles[1], download: async file => {
    calls.push(file); max = Math.max(max, ++concurrent); await gates[calls.length-1].promise; concurrent--;
  }});
  const done = q.enqueue([dictationFiles[0]]); await tick();
  assert.equal(q.enqueue(dictationFiles), done); q.enqueue(dictationFiles);
  assert.deepEqual(calls, [dictationFiles[0]]); gates[0].resolve(); await tick();
  assert.deepEqual(calls, [dictationFiles[0],dictationFiles[2]]); gates[1].resolve(); await done;
  assert.equal(max, 1); assert.equal(q.busy, false);
});
test('one failure does not discard remaining downloads and can be retried', async () => {
  const calls = [], failures = []; let fail = true;
  const q = new ModelDownloads({installed: () => false, failed: e => failures.push(e.message), download: async file => {
    calls.push(file); if (file === dictationFiles[0] && fail) throw new Error('Connection interrupted');
  }});
  await q.enqueue(dictationFiles); assert.equal(q.states.get(dictationFiles[0]).state, 'failed');
  assert.equal(q.ready(dictationFiles[2]), true); fail = false; await q.enqueue(dictationFiles);
  assert.deepEqual(calls, [...dictationFiles,dictationFiles[0]]); assert.deepEqual(failures,['Connection interrupted']);
});
test('pause cancels active transfer, clears queued work, and permits resume', async () => {
  const gate = deferred(), calls = []; let paused = 0;
  const q = new ModelDownloads({installed: () => false, download: async file => { calls.push(file); if (calls.length === 1) await gate.promise; }, cancel: async () => {paused++;gate.reject(new Error('Cancelled'));}});
  const done = q.enqueue(dictationFiles); await tick(); await q.cancel(); await done;
  assert.equal(paused,1); assert.deepEqual(calls,[dictationFiles[0]]); assert.equal(q.busy,false);
  assert.ok(dictationFiles.every(file=>q.states.get(file).state==='cancelled'));
  await q.enqueue(dictationFiles); assert.ok(dictationFiles.every(file=>q.ready(file)));
});
test('pause before transfer begins does not start a download', async () => {
  const calls = []; const q = new ModelDownloads({installed:()=>false,download:async f=>calls.push(f),cancel:async()=>{throw new Error('Should not cancel a nonexistent transfer');}});
  const done=q.enqueue(dictationFiles); await q.cancel(); await done; assert.deepEqual(calls,[]); assert.equal(q.busy,false);
});
test('progress handles startup, zero totals, and out-of-range bytes', () => {
  for (const d of [{state:'downloading'},{state:'downloading',total:0,received:NaN},{state:'verifying',total:100,received:200}]) {
    const html=downloadProgress(d,helpers); assert.doesNotMatch(html,/NaN|Infinity|undefined|200%/);
  }
  assert.match(downloadProgress({state:'verifying',total:100,received:200},helpers),/100%/);
});
test('errors are escaped and cancelled transfers offer resume', () => {
  assert.doesNotMatch(downloadProgress({state:'failed',error:'<script>bad</script> & retry'},helpers),/<script>/);
  assert.match(downloadProgress({state:'cancelled'},helpers),/resume/);
});
test('three individual controls precede Download all; installed controls cannot redownload', () => {
  const models=[...dictationFiles].reverse().map(file=>({file,bytes:1000000,installed:false}));
  const q=new ModelDownloads({installed:()=>false,download:async()=>{}});
  const html=modelSetupMarkup(models,q,helpers);
  assert.equal((html.match(/data-action="modelDownload"/g)||[]).length,3);
  assert.ok(html.indexOf('data-action="downloadRecommended"')>html.lastIndexOf('data-action="modelDownload"'));
  assert.ok(html.indexOf(dictationFiles[0])<html.indexOf(dictationFiles[1]));
  q.states.set(dictationFiles[0],{state:'complete'});
  assert.equal((modelSetupMarkup(models,q,helpers).match(/data-action="modelDownload"/g)||[]).length,2);
});
test('empty model catalog cannot falsely report ready', () => {
  const q=new ModelDownloads({installed:()=>false}); const html=modelSetupMarkup([],q,helpers);
  assert.match(html,/could not be loaded/); assert.doesNotMatch(html,/All downloaded/);
});
