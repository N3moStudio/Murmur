# Thinking Orbs integration evidence

Inspected 2026-11-04. Requested source revision: `54722cc9a3dcb167037f7d449d6cca4d174e9eb5` in [Jakubantalik/Libraries.dev](https://github.com/Jakubantalik/Libraries.dev/tree/54722cc9a3dcb167037f7d449d6cca4d174e9eb5). This report proposes an integration; it does not claim Windows rendering or resource measurements have been performed.

## Identity and licensing

The pinned `packages/thinking-orbs/package.json` identifies version **0.3.2**, MIT, copyright **2026 Jakub Antalik**. The package's full LICENSE is saved with the inspected source under `research/thinking-orbs-reference/packages/thinking-orbs/LICENSE`. Retain that notice in redistributed source and the app's third-party notices. This is the public MIT component. No paid Studio presets, model files, Pro exports, or unrelated Libraries.dev libraries are needed.

## Exact relationship to Elsepin

Read-only inspection of the current Elsepin project confirms the same revision is pinned in the Elsepin project manifest. Its `Locus/Support/ElsepinProgressLabel.swift` uses `ThinkingOrb(state: state, size: .px20, theme: .auto, paused: ...)`, with the default speed and an 8-point gap before the existing status text. Its default state is `searching`; connection tests use `connecting`. The visual is hidden from accessibility and the status text is announced once. The label disappears on completion and pauses for invisibility, inactive scenes, reduced motion and low power. No Elsepin files were changed.

The [Swift package README](https://github.com/Jakubantalik/Libraries.dev/blob/54722cc9a3dcb167037f7d449d6cca4d174e9eb5/packages/thinking-orbs/ports/ios/ThinkingOrbsKit/README.md) explains that native geometry is checked against the web engine's golden vectors. Windows should use the original web engine rather than recreate approximate dots. Identical geometry does not imply pixel-identical rasterization between CoreGraphics and Chromium; the source explicitly discusses subpixel circle differences.

## Web entry point suitable for Murmur

Use **`thinking-orbs/engine`**, not the React component entry. Its exports include `resolvePreset`, `MODE_FRAMES`, `MODE_DRAWS` and `paintFrame`. The dependency graph is plain TypeScript geometry and 2D canvas painting; it imports no React runtime, no DOM observer, no WebGL library and no image assets. The existing WebView2 UI can drive it directly. The full component has a React >=18 peer dependency; the engine bundle does not need React.

Pinned source pointers (all saved locally):

| Source | Purpose |
|---|---|
| [src/engine/index.ts](https://github.com/Jakubantalik/Libraries.dev/blob/54722cc9a3dcb167037f7d449d6cca4d174e9eb5/packages/thinking-orbs/src/engine/index.ts) | React-free public entry and draw contract |
| [src/presets.ts](https://github.com/Jakubantalik/Libraries.dev/blob/54722cc9a3dcb167037f7d449d6cca4d174e9eb5/packages/thinking-orbs/src/presets.ts) | Exact state mapping, cached preset counts, radii and speed |
| [src/engine/core.ts](https://github.com/Jakubantalik/Libraries.dev/blob/54722cc9a3dcb167037f7d449d6cca4d174e9eb5/packages/thinking-orbs/src/engine/core.ts) | Projection, final z-sort, depth shading, canvas circle/line painter |
| [src/engine/registry.ts](https://github.com/Jakubantalik/Libraries.dev/blob/54722cc9a3dcb167037f7d449d6cca4d174e9eb5/packages/thinking-orbs/src/engine/registry.ts) | Geometry function registration |
| `src/engine/{orbits,lattice,braid,ribbon,morph,web}.ts` | Original geometry implementations |
| [src/ThinkingOrb.tsx](https://github.com/Jakubantalik/Libraries.dev/blob/54722cc9a3dcb167037f7d449d6cca4d174e9eb5/packages/thinking-orbs/src/ThinkingOrb.tsx) | Reference lifecycle/DPR/accessibility behavior, not required by engine |
| [vite.config.ts](https://github.com/Jakubantalik/Libraries.dev/blob/54722cc9a3dcb167037f7d449d6cca4d174e9eb5/packages/thinking-orbs/vite.config.ts) | Separate ESM/CJS engine build entry |

`resolvePreset(state, size)` returns `{mode, speed, opts}`; cache it when state/size changes. Each frame calls `MODE_FRAMES[mode](size, seconds * speed, opts)`, then `paintFrame(context, geometry, isDark)`. Geometry consists of finalized circles plus optional lines; lines paint first, then depth-sorted circles. Preserve default monochrome ink, radii, density, and speed for the same visual character.

Sizes in the **source** are 20, 32 and 64 CSS pixels. The README mentions only the two hand-tuned sizes; 32 is an interpolated intermediate. Use 20 for inline status/overlay and 32 or 64 only where the existing layout needs a larger indicator. Do not render the 64 geometry then shrink it to 20; presets have distinct density and radius tuning.

## Processing-state mapping (proposed, not reference-app evidence)

| Murmur operation | Upstream orb state | Geometry |
|---|---|---|
| Transcribing audio / general processing | working | Tilted dotted orbits |
| Cleaning up dictated text / writing a summary | composing | Undulating bands |
| Answering a meeting question | solving | Scrambling and resolving bands |
| Searching stored meetings | searching | Meridian on dotted globe |
| Connecting / syncing / checking update service | connecting | Connected nodes |

Other available states are listening, weaving, breathing and shaping. Recording already has real level feedback; the animated listening preset is time-driven and must not be represented as measured microphone amplitude. The orb should never determine work state, delays, recording or cancellation.

## Low-resource integration policy

These are proposed engineering choices; measure their effect on the actual installed app:

- Use one scheduler per WebView for all visible processing indicators. The upstream React implementation shares a time origin but allocates one requestAnimationFrame loop per instance; an engine consumer can consolidate that without changing geometry.
- Create/update indicators only for actual pending work; remove them immediately when work finishes or is cancelled. No permanent idle orb or polling timer.
- Cap canvas DPR at 2, as upstream does. Match CSS size to the selected preset.
- Stop scheduling when the host window is hidden/minimized, the document is hidden, or the individual indicator is offscreen. Feed native host visibility to the UI; do not assume browser visibility alone covers every native-window state.
- Reduced motion or Windows battery saver should paint one representative frame at **t = 0.6**, with no animation loop. Theme changes may repaint that static frame.
- A 30 fps drawing cap is a reasonable starting point for the Windows host, using elapsed time to preserve the original speed. It is an app-specific frame-pacing choice, not an upstream default. Check it visually before deciding; 60 Hz is the source component's normal rAF behavior.
- Do not attach optional gravity/pointer effects, tint, density overrides, shadows, glow, filters or physics; those are unnecessary to reproduce Elsepin's progress labels.
- Keep the canvas `aria-hidden=true` beside a single textual status/live region. Avoid duplicate state announcements.

No network, GPU shader compilation, external assets or extra browser instance is needed. CPU work still includes geometry allocation, trigonometry and sorting every drawn frame; “zero CPU” while animated would be false. Document actual visible-processing overhead and confirm **zero scheduled orb frames** after completion, while hidden, and under reduced motion.

## Verification still needed

Freeze the same state/size/time/theme in the app and original engine, compare screenshots at equivalent DPR, then inspect moving states. Check rapidly changing work states, failed/cancelled work, theme changes, minimize/tray return and battery saver. Measure app CPU and memory with no work, one visible orb, and several visible processing rows. The upstream Apple performance table is not a Windows measurement and should not appear as this app's benchmark.
