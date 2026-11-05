export const dictationFiles = ['moonshine-base-en', 'ggml-silero-v6.2.0.bin', 'qwen2.5-1.5b-instruct-q4_k_m.gguf'];
const inProgress = new Set(['queued', 'downloading', 'verifying', 'cancelling']);
const arrow = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3v12m-5-5 5 5 5-5M5 16v4h14v-4"/></svg>';
const check = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m5 12 4 4L19 6"/></svg>';

// One transfer at a time matches the native downloader. Repeated clicks and
// Download all share this queue, so they cannot start duplicate transfers.
export class ModelDownloads {
  constructor({download, cancel, installed, changed = () => {}, failed = () => {}}) {
    Object.assign(this, {download, cancelTransfer: cancel, installed, changed, failed});
    this.states = new Map(); this.pending = []; this.active = null; this.stopping = false; this.task = null;
  }
  get busy() { return this.active !== null || this.pending.length > 0 || this.stopping; }
  ready(file) { const status = this.states.get(file); return status ? status.state === 'complete' : this.installed(file); }
  enqueue(files) {
    if (this.stopping) return this.task;
    for (const file of files) {
      if (this.ready(file) || file === this.active || this.pending.includes(file)) continue;
      this.pending.push(file); this.states.set(file, {file, state: 'queued'});
    }
    this.changed();
    if (!this.task && this.pending.length) {
      this.task = Promise.resolve().then(() => this.drain()).finally(() => {
        this.task = null; this.stopping = false; this.changed();
      });
    }
    return this.task;
  }
  async drain() {
    while (this.pending.length) {
      const file = this.active = this.pending.shift();
      this.update({file, state: 'downloading'});
      try { await this.download(file); this.update({file, state: 'complete'}); }
      catch (error) {
        this.update({file, state: this.stopping ? 'cancelled' : 'failed', error: this.stopping ? null : error.message});
        if (!this.stopping) this.failed(error);
      }
      finally { this.active = null; this.changed(); }
    }
  }
  update(progress) {
    if (progress.file === this.active && this.stopping && inProgress.has(progress.state)) progress = {...progress, state: 'cancelling'};
    const progressOnly = this.states.get(progress.file)?.state === progress.state;
    this.states.set(progress.file, progress); this.changed(progressOnly ? progress : null);
  }
  async cancel() {
    this.stopping = true;
    for (const file of this.pending) this.states.set(file, {file, state: 'cancelled'});
    this.pending = [];
    if (this.active) this.update({file: this.active, state: 'cancelling'});
    try { if (this.active) await this.cancelTransfer(); }
    catch (error) { this.failed(error); }
    finally { if (!this.task) this.stopping = false; this.changed(); }
  }
}

export function downloadProgress(d, {escape, size, busy}) {
  if (!d || d.state === 'complete') return '';
  if (d.state === 'queued') return '<span class="muted">Queued — your download will start next.</span>';
  if (d.state === 'cancelled') return '<span class="muted">Paused. Use the download button to resume.</span>';
  if (d.state === 'failed') return `<span class="error-text">${escape(d.error || 'Download failed. Please try again.')}</span>`;
  const total = Number.isFinite(d.total) && d.total > 0 ? d.total : 0;
  const received = Math.max(0, Math.min(total, Number.isFinite(d.received) ? d.received : 0));
  const label = d.state === 'verifying' ? 'Checking download…' : d.state === 'cancelling' ? 'Pausing…' : 'Downloading…';
  const amount = total ? `<span class="download-amount">${Math.floor(received / total * 100)}% · ${size(received)} of ${size(total)}</span>` : '';
  return `<div class="model-progress-heading">${busy(label, 'connecting')}${amount}</div><progress aria-label="Download progress" ${total ? `max="${total}" value="${received}"` : ''}></progress>`;
}

export function modelSetupMarkup(models, queue, helpers, maintenance = false) {
  const {escape, size, name} = helpers;
  const parts = dictationFiles.map(file => models.find(m => m.file === file)).filter(Boolean);
  const missing = parts.filter(m => !queue.ready(m.file));
  const available = missing.some(m => !inProgress.has(queue.states.get(m.file)?.state));
  const rows = parts.map(m => {
    const d = queue.states.get(m.file), ready = queue.ready(m.file), waiting = inProgress.has(d?.state);
    const label = `${d?.state === 'failed' ? 'Retry downloading' : 'Download'} ${name(m.file)}`;
    const status = downloadProgress(d, helpers);
    return `<div class="model-download-row" data-model-row="${escape(m.file)}">
      <div class="model-details"><strong>${escape(name(m.file))}</strong><p class="model-meta">${size(m.bytes)} · ${ready ? 'Ready to use' : 'Download once'}</p></div>
      ${ready ? `<span class="model-ready" role="img" aria-label="${escape(name(m.file))} is ready">${check}</span>` : `<button class="model-download-button" data-action="modelDownload" data-file="${escape(m.file)}" aria-label="${escape(label)}" title="${escape(label)}" ${waiting || maintenance || queue.stopping ? 'disabled' : ''}>${arrow}</button>`}
      <div class="model-progress" data-download="${escape(m.file)}" role="status" ${status ? '' : 'hidden'}>${status}</div>
    </div>`;
  }).join('');
  return `<div class="model-download-list">${rows || '<p class="muted">Dictation components could not be loaded. Reopen settings to try again.</p>'}</div>
    <div class="model-download-footer"><button class="primary model-download-all" data-action="downloadRecommended" ${!available || maintenance || queue.stopping ? 'disabled' : ''}>${arrow}<span>${missing.length ? 'Download all' : parts.length ? 'All downloaded' : 'Download all'}</span></button>
    ${queue.busy ? `<button class="quiet model-download-cancel" data-action="modelCancel" ${queue.stopping ? 'disabled' : ''}>${queue.stopping ? 'Pausing…' : 'Pause downloads'}</button>` : ''}</div>`;
}
