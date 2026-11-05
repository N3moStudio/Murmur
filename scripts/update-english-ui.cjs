const fs=require('node:fs');
const path='src/Murmur/web/app.js';
let source=fs.readFileSync(path,'utf8');
const replacements=[
 ["state.models.filter(m=>m.file.startsWith('ggml-')&&!m.file.includes('silero'))", "state.models.filter(m=>m.speech)"],
 ["Start with the balanced speech model, speech detection, and the local writing model. About 1.27 GB in total.", "Fast English dictation, speech detection, and local writing. About 1.26 GB to download once. Models load when you start speaking and unload after inactivity."],
 ["['ggml-silero-v6.2.0.bin','ggml-base.bin','qwen2.5-1.5b-instruct-q4_k_m.gguf']", "['ggml-silero-v6.2.0.bin','moonshine-base-en','qwen2.5-1.5b-instruct-q4_k_m.gguf']"],
 ["The writing model unloads after 45 seconds without work.", "The speech model unloads after 30 seconds of inactivity; the writing model after 45 seconds."],
 ["from the focused app and field labels", "from the focused app and window title"]
];
for(const [oldValue,newValue] of replacements){if(!source.includes(oldValue))throw Error('Expected text missing: '+oldValue);source=source.replace(oldValue,newValue);}
fs.writeFileSync(path,source);
const main='src/Murmur/Windows/MainForm.cs';
source=fs.readFileSync(main,'utf8').replace('recording.Dispose(); text.Dispose();','recording.Dispose(); speech.Dispose(); text.Dispose();');
fs.writeFileSync(main,source);
