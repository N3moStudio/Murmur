const fs=require('node:fs');const file='src/Murmur/web/app.js';let s=fs.readFileSync(file,'utf8');
s=s.replace('let autosave;', 'let autosave; const dirtyEditors=new Set();');
s=s.replace("includes(el.id)){clearTimeout(autosave);autosave=setTimeout(()=>saveEditor(el.id),500);", "includes(el.id)){dirtyEditors.add(el.id);clearTimeout(autosave);autosave=setTimeout(()=>saveEditor(el.id),500);");
const start=s.indexOf('async function saveEditor(id){'),end=s.indexOf("\ndocument.addEventListener('change'",start);
if(start<0||end<0)throw Error('Save function not found');
const replacement=`async function saveEditor(id){
 clearTimeout(autosave);
 try{
  if(id==='scratchpad-editor'){
   if(!dirtyEditors.has(id))return true;
   const captured=$('#scratchpad-editor')?.value??state.scratchpad;
   await call('scratchpad',{text:captured});state.scratchpad=captured;
   if($('#scratchpad-editor')?.value===captured)dirtyEditors.delete(id);
  }else if(note){
   const args={id:note.id},captured=[];
   for(const [elementId,key] of [['note-editor','notes'],['summary-editor','summary'],['note-title','title']]){
    const element=$('#'+elementId);if(element&&dirtyEditors.has(elementId)){args[key]=element.value;captured.push([elementId,element.value]);}
   }
   if(!captured.length)return true;
   const saved=await call('saveMeeting',args);if(note?.id===args.id)note=saved;
   for(const [elementId,value] of captured)if($('#'+elementId)?.value===value)dirtyEditors.delete(elementId);
  }
  return true;
 }catch(e){notify('Could not save: '+e.message);return false;}
}`;
s=s.slice(0,start)+replacement+s.slice(end);
s=s.replaceAll("await saveEditor(page==='scratchpad'?'scratchpad-editor':'note-editor');", "if(!await saveEditor(page==='scratchpad'?'scratchpad-editor':'note-editor'))return;");
// Do not replace the const saved = await expression in the flush handler.
s=s.replace("const saved=if(!await saveEditor(page==='scratchpad'?'scratchpad-editor':'note-editor'))return;", "const saved=await saveEditor(page==='scratchpad'?'scratchpad-editor':'note-editor');");
s=s.replaceAll("await saveEditor('note-editor');", "if(!await saveEditor('note-editor'))return;");
s=s.replace("if(note?.id===currentId){note=fresh;render();}", "if(note?.id===currentId){note=fresh;if(!dirtyEditors.size)render();}");
fs.writeFileSync(file,s);
