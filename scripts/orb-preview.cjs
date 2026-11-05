// Preview the pinned upstream geometry, without redrawing or inventing a preset.
const fs = require('node:fs');
const { resolvePreset, MODE_FRAMES } = require('../artifacts/orbs-engine.cjs');
const states = ['searching', 'working', 'composing', 'connecting', 'solving', 'listening', 'weaving', 'breathing', 'shaping'];
const frame = (state, size, time) => { const p = resolvePreset(state, size); return MODE_FRAMES[p.mode](size, time * p.speed, p.opts); };
fs.writeFileSync('artifacts/orb-preview-geometry.json', JSON.stringify({
  states,
  still: states.map(s => frame(s, 64, 0.6)),
  inline: frame('searching', 20, 0.6),
  motion: Array.from({ length: 80 }, (_, i) => states.slice(0,4).map(s => frame(s, 64, i / 20)))
}));
