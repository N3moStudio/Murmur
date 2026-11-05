const fs=require('node:fs');const file='src/Murmur/web/app.js';let s=fs.readFileSync(file,'utf8');
s=s.replace("async function saveEditor(id){try{", "async function saveEditor(id){clearTimeout(autosave);try{");
s=s.replace("note=await call('saveMeeting',args);}}catch(e){notify('Could not save: '+e.message);}}", "note=await call('saveMeeting',args);}return true;}catch(e){notify('Could not save: '+e.message);return false;}}");
s=s.replace("window.chrome.webview.addEventListener('message',event=>{", "window.chrome.webview.addEventListener('message',async event=>{");
s=s.replace("const {type,data}=event.data;if(type==='response')", "const {type,data}=event.data;if(type==='flushEditor'){const saved=await saveEditor(page==='scratchpad'?'scratchpad-editor':'note-editor');await call('editorFlushed',{saved});return;}if(type==='response')");
// Refresh generated note results only after saving the currently visible editor.
s=s.replace("if(type==='snapshot'){state=data;if(!$('#dialog')", "if(type==='snapshot'){state=data;if(note&&data.recording.state!=='recording'&&data.recording.state!=='processing'){const currentId=note.id;await saveEditor('note-editor');const fresh=await call('meeting',{id:currentId});if(note?.id===currentId){note=fresh;render();}return;}if(!$('#dialog')");
s=s.replace("if(name==='Calendar')body=`<p>", "if(name==='Calendar')body=`${settingRow('Meeting reminders','Notify five minutes before meetings in your last refreshed calendar. Recording never starts automatically.',checkSetting('calendarReminders'))}<p>");
fs.writeFileSync(file,s);
