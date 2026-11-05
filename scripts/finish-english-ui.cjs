const fs=require('node:fs');
let source=fs.readFileSync('src/Murmur/web/app.js','utf8');
const start=source.indexOf("${settingRow('Language','Automatic language detection");
const end=source.indexOf("${settingRow('Theme'",start);
if(start<0||end<0)throw Error('Language UI not found');
source=source.slice(0,start)+"${settingRow('Language','This release focuses on fast English dictation.','<span>English</span>')}"+source.slice(end);
source=source.replace("await call('transform',{text:source,prompt:t.prompt})", "await call('transform',{text:source,prompt:t.prompt,id:t.id||id||''})");
fs.writeFileSync('src/Murmur/web/app.js',source);
