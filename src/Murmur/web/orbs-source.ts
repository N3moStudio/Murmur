import { resolvePreset, MODE_FRAMES, paintFrame } from '../../../third_party/thinking-orbs/src/engine/index';

// Same engine and presets as Elsepin; one scheduler, no idle animation.
let animation = "on";
let active = true, lowPower = false, request = 0, last = 0, frames = 0;
const reduced = matchMedia('(prefers-reduced-motion: reduce)');
const items = new Map<HTMLCanvasElement, { preset: ReturnType<typeof resolvePreset>, size: 20|32|64, visible: boolean, paused: boolean }>();
const io = new IntersectionObserver(entries => { for (const entry of entries) { const item = items.get(entry.target as HTMLCanvasElement); if (item) item.visible = entry.isIntersecting; } schedule(); });
const isPaused = () => !active || animation === "off" || (animation === "system" && reduced.matches) || document.hidden;
function paint(canvas: HTMLCanvasElement, item: typeof items extends Map<any, infer V> ? V : never, time: number) {
  const dpr = Math.min(devicePixelRatio || 1, 2), px = Math.round(item.size * dpr);
  if (canvas.width !== px) { canvas.width = px; canvas.height = px; }
  const ctx = canvas.getContext('2d'); if (!ctx) return;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0); ctx.clearRect(0, 0, item.size, item.size);
  const p = item.preset; const frame = MODE_FRAMES[p.mode](item.size, time * p.speed, p.opts);
  const dark = canvas.closest('#recording-strip, .warm') !== null || document.documentElement.dataset.theme === 'dark';
  paintFrame(ctx, frame, dark); frames++;
}
function tick(now: number) {
  request = 0;
  if (isPaused()) return;
  if (now - last >= 1000/(lowPower ? 15 : 30)) { last = now; for (const [canvas, item] of items) if (item.visible && !item.paused && canvas.isConnected) paint(canvas, item, now/1000); }
  if ([...items.values()].some(i => i.visible && !i.paused)) request = requestAnimationFrame(tick);
}
function schedule() {
  if (request) cancelAnimationFrame(request); request = 0;
  if (isPaused()) { for (const [canvas,item] of items) if (item.visible) paint(canvas,item,.6); return; }
  if ([...items.values()].some(i => i.visible && !i.paused)) request = requestAnimationFrame(tick);
}
export function refreshOrbs() {
  for (const canvas of items.keys()) if (!canvas.isConnected) { io.unobserve(canvas); items.delete(canvas); }
  document.querySelectorAll<HTMLCanvasElement>('canvas[data-orb]').forEach(canvas => {
    if (items.has(canvas)) return;
    const requestedSize = Number(canvas.dataset.size || 20);
    const size = ([20,32,64].includes(requestedSize) ? requestedSize : 20) as 20|32|64;
    const state = canvas.dataset.orb as Parameters<typeof resolvePreset>[0];
    const item = { size, preset: resolvePreset(state,size), visible: true, paused: canvas.dataset.paused === 'true' };
    canvas.style.width = `${size}px`; canvas.style.height = `${size}px`; items.set(canvas,item); io.observe(canvas); paint(canvas,item,.6);
  }); schedule();
}
export function setOrbHostState(visible: boolean, battery: boolean, motion = "on") { active = visible; lowPower = battery; animation = motion; schedule(); }
export function orbDiagnostics() { return { canvases: items.size, scheduled: request !== 0, paintedFrames: frames, paused: isPaused(), fpsCap: lowPower ? 15 : 30 }; }
document.addEventListener('visibilitychange', schedule); reduced.addEventListener('change', schedule);
new MutationObserver(refreshOrbs).observe(document.body,{childList:true,subtree:true});
refreshOrbs();
