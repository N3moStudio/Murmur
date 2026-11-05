// Pin/attribute the MIT OpenWhispr source; run only when deliberately updating it.
const fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('research/remakes/reuse-candidates/openwhispr/src/helpers/toneAppCategories.js','utf8');
const box={module:{exports:{}}};vm.runInNewContext(source+'\nmodule.exports.catalog={APP_SIGNATURES,BROWSER_SIGNATURES,SITE_TITLES};',box);
const c=box.module.exports.catalog;
let result='// Derived from OpenWhispr toneAppCategories.js, MIT © 2024 OpenWhispr Team.\n// Revision d5821a7c88f4c1803b06c51b4580f16c51dd0dbe. See third_party/openwhispr/LICENSE.\nnamespace Murmur.Core;\npublic static partial class AppContextClassifier\n{\n';
for(const [group,values] of Object.entries(c)){
 if(Array.isArray(values))result+=`    private static readonly string[] ${group} = [${values.map(JSON.stringify).join(',')}];\n`;
 else for(const [category,list]of Object.entries(values))result+=`    private static readonly string[] ${group}_${category} = [${list.map(JSON.stringify).join(',')}];\n`;
}
fs.writeFileSync('src/Murmur/Core/ContextCatalog.cs',result+'}\n');
fs.mkdirSync('third_party/openwhispr',{recursive:true});fs.copyFileSync('research/remakes/reuse-candidates/openwhispr/LICENSE','third_party/openwhispr/LICENSE');
fs.copyFileSync('research/remakes/reuse-candidates/openwhispr/PROVENANCE.json','third_party/openwhispr/PROVENANCE.json');
fs.copyFileSync('research/remakes/reuse-candidates/openwhispr/src/helpers/toneAppCategories.js','third_party/openwhispr/toneAppCategories.js');
fs.mkdirSync('tests/fixtures',{recursive:true});
const cases=[['chrome','Inbox - Gmail - Google Chrome'],['msedge','Gmail and 4 more pages - Work - Microsoft\u200b Edge'],['firefox','engineering - OpenWhispr - Slack — Mozilla Firefox'],['chrome','Outlook paste bug · Issue #12 · org/app - Google Chrome'],['chrome','Slack - Google Docs - Google Chrome'],['Code','notes - Gmail'],['Mailchimp','Gmail'],['slack','Gmail'],['ApplicationFrameHost','WhatsApp'],['Thorium','Inbox - Gmail - Thorium'],['brave','slack - Brave Search - Brave'],['olk','New email'],['PhoneExperienceHost','Phone Link'],['ms-teams','Messages']];
fs.writeFileSync('tests/fixtures/context-categories.json',JSON.stringify(cases.map(([process,title])=>({process,title,expected:box.module.exports.classifyToneAppCategory({appSignature:process,windowTitle:title})}))));
const orb=JSON.parse(fs.readFileSync('artifacts/orb-preview-geometry.json','utf8')).inline;
fs.writeFileSync('tests/fixtures/thinking-orbs-20.json',JSON.stringify(orb));
