// Test-only host boundary. The real app UI runs against synthetic downloads;
// this fixture never invokes Windows, records audio, or downloads model weights.
const files = ['moonshine-base-en', 'ggml-silero-v6.2.0.bin', 'qwen2.5-1.5b-instruct-q4_k_m.gguf'];
const sizes = [141300566, 885098, 1117320736];
const scenario = new URL(location.href).searchParams.get('case') || 'demo';
const state = {
  version: 'fixture', architecture: 'isolated', lowPower: false,
  settings: {onboarded: false, theme: 'light', orbAnimation: 'off', holdShortcut: 'Ctrl+Alt', toggleShortcut: 'Double Caps Lock', commandShortcut: 'Ctrl+Alt+Shift'},
  models: scenario === 'empty' ? [] : files.map((file,i)=>({file,bytes:sizes[i],installed:scenario==='ready'||(scenario==='partial'&&i===1)})),
  recording: {state:'idle'}, history:[], meetings:[], folders:[], transforms:[], calendarConnections:[]
};
const listeners = [], calls = []; let activeController;
const emit = (type,data) => listeners.forEach(callback=>callback({data:{type,data}}));
const wait = (ms,signal) => new Promise((resolve,reject)=>{
  const timer=setTimeout(resolve,ms);
  signal?.addEventListener('abort',()=>{clearTimeout(timer);reject(new Error('Download cancelled'));},{once:true});
});
window.chrome ??= {};
window.chrome.webview = {
  addEventListener: (_,listener)=>listeners.push(listener),
  postMessage: async ({id,action,args})=>{
    try {
      let result=null;
      if(action==='ready'||action==='refresh')result=structuredClone(state);
      if(action==='devices')result=[];
      if(action==='saveSettings'){state.settings=args;result=structuredClone(state);}
      if(action==='modelCancel')activeController?.abort();
      if(action==='modelDownload'){
        calls.push(args.file);activeController=new AbortController();
        const model=state.models.find(m=>m.file===args.file);
        for(const fraction of [.1,.5,1]){emit('download',{file:args.file,state:'downloading',received:model.bytes*fraction,total:model.bytes});await wait(350,activeController.signal);}
        model.installed=true;emit('download',{file:args.file,state:'complete',received:model.bytes,total:model.bytes});result=structuredClone(state.models);
      }
      queueMicrotask(()=>emit('response',{id,result}));
    }catch(error){emit('response',{id,error:error.message});}
  }
};
await import('../../src/Murmur/web/app.js');
if(scenario==='worst'){
  emit('download',{file:files[0],state:'downloading',received:141300566,total:141300566});
  emit('download',{file:files[1],state:'failed',error:'The connection to https://example.com/downloads/english/speech-detection/component.int8.onnx was interrupted before the integrity check completed. Please check your connection and retry.'});
  emit('download',{file:files[2],state:'queued'});
}
const panel=document.createElement('aside');panel.setAttribute('aria-label','Test fixture controls');
panel.style.cssText='position:fixed;bottom:0;right:8px;z-index:9999;background:var(--paper);padding:4px;border:1px solid var(--line);font-size:11px';
panel.innerHTML='<label>Test data <select aria-label="Test data">'+['demo','worst','empty','partial','ready'].map(mode=>`<option ${mode===scenario?'selected':''}>${mode}</option>`).join('')+'</select></label>';
panel.querySelector('select').onchange=e=>{const url=new URL(location.href);url.searchParams.set('case',e.target.value);location.href=url.href;};
document.body.append(panel);
window.setupFixture={calls,state};
