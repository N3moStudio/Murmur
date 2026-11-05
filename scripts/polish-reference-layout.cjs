const fs = require('node:fs');
const file = 'src/Murmur/web/app.js';
let source = fs.readFileSync(file, 'utf8');
const homeStart = source.indexOf('function home(){');
const homeEnd = source.indexOf('\nfunction stats()', homeStart);
const oldHome = source.slice(homeStart, homeEnd);
const historyStart = oldHome.indexOf('(items.length?');
const historyEnd = oldHome.lastIndexOf('+stats();}');
if (homeStart < 0 || homeEnd < 0 || historyStart < 0 || historyEnd < 0) throw Error('Home structure changed');
const history = oldHome.slice(historyStart, historyEnd);
const newHome = `function home(){
 const items=state.history.filter(h=>(h.text+' '+h.raw+' '+h.target).toLowerCase().includes(query.toLowerCase()));
 let previous='';
 return head(\`Hey, get back into the flow with <span class="key shortcut-highlight">\${escape(state.settings.toggleShortcut)}</span>\`,button('Import audio','importAudio'))+
 \`<div class="dictation-grid"><section><div class="hero warm dictation-hero"><div class="hero-copy"><h2>Make Murmur sound like <em>you</em></h2><p>Set up different writing styles for different apps.</p><button data-page="style" class="primary">Set your style</button></div><canvas data-orb="searching" data-size="96" data-paused="true" aria-hidden="true"></canvas></div><div class="row spread"><h3>Dictation history</h3><div>\${button('Start dictation','record','data-mode="dictation"','quiet small')}\${button('⌕ Search','search','','quiet small')}</div></div>\${searchOpen?\`<input class="search" id="search-input" aria-label="Search history" value="\${escape(query)}" placeholder="Search past transcripts">\`:''}\`+
 ${history}+\`</section><aside class="voice-stats">\${stats()}<div class="voice-tip"><h3>Your voice, everywhere.</h3><p>Double-tap Ctrl to begin. Tap Ctrl once to finish.</p><p class="muted">The recording bar stays hidden until you need it.</p></div></aside></div>\`;
}`;
source = source.slice(0,homeStart)+newHome+source.slice(homeEnd);
source = source.replace("function modelsView(){", "function modelName(file){return ({'moonshine-base-en':'Fast English','ggml-base.bin':'Whisper · balanced','ggml-small.bin':'Whisper · higher accuracy','ggml-tiny.bin':'Whisper · compact','ggml-large-v3-turbo-q5_0.bin':'Whisper · highest quality','ggml-silero-v6.2.0.bin':'Speech detection','qwen2.5-1.5b-instruct-q4_k_m.gguf':'Local writing','speaker-segmentation.int8.onnx':'Speaker detection','3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx':'Speaker recognition'})[file]||file;}\nfunction modelsView(){");
source=source.replace("escape(m.file.replace('ggml-','').replace('.bin','').replace('.gguf',''))",'escape(modelName(m.file))');
source=source.replace("[m.file,m.file.replace('ggml-','').replace('.bin','')]",'[m.file,modelName(m.file)]');
source=source.replace('${modelsView()}${button(\'Download recommended models\'',"${recommendedModels()}${button('Download recommended models'");
const helper=`function recommendedModels(){return state.models.filter(m=>['moonshine-base-en','ggml-silero-v6.2.0.bin','qwen2.5-1.5b-instruct-q4_k_m.gguf'].includes(m.file)).map(m=>\`<div class="model row spread"><div><strong>\${escape(modelName(m.file))}</strong><p>\${size(m.bytes)} · \${m.installed?'Ready to use':'Download once'}</p><div data-download="\${m.file}"></div></div><span aria-label="\${m.installed?'Installed':'Not installed'}">\${m.installed?'✓':'↓'}</span></div>\`).join('')+button('Other speech models','models','','quiet small');}\n`;
source=source.replace('function onboarding(){',helper+'function onboarding(){');
source=source.replace("${settingRow('Recording limit (minutes)'", "${settingRow('Identify speakers','Optional. Download the two speaker models in Models. Adds processing time and memory use; labels can be renamed.',checkSetting('identifySpeakers'))}${settingRow('Recording limit (minutes)'");
source=source.replaceAll('People sharing a track are not automatically identified.', 'Enable speaker identification in Notetaker settings to distinguish people on the same track. Mixed turns are marked when uncertain.');
fs.writeFileSync(file,source);
