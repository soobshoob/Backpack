import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import ts from 'typescript';
async function load(path){return import('data:text/javascript;base64,'+Buffer.from(ts.transpileModule(fs.readFileSync(path,'utf8').replace("import settings from './integrations/settings.json';",'const settings='+fs.readFileSync('src/integrations/settings.json','utf8')+';'),{compilerOptions:{module:ts.ModuleKind.ESNext,target:ts.ScriptTarget.ES2022}}).outputText).toString('base64'));}
const {sampleTimer,sampleLayout}=await load('src/animation/plugins.ts');
const {product}=await load('src/product.ts');
test('Target core is exactly keys and mouse; six independent plugins distinguish source/package/public',()=>{
 assert.deepEqual(product.core.map(v=>v.id),['keys','mouse']);assert.deepEqual(product.plugins.map(v=>v.id),['drawing','layout','lens','focus','cursorLens','timer']);assert.deepEqual(product.plugins.map(v=>v.status),['planned','planned','verified','verified','verified','verified']);assert(product.plugins.every(v=>!v.public&&!v.packageUrl&&!v.documentationUrl&&!v.exampleUrl));assert.equal(product.version,'0.4.0-preview.4');assert.equal(product.sdk.verifiedVersion,'1.1');assert.equal(product.sdk.sourceVersion,'1.1');assert.equal(product.published,false);assert.equal(product.downloadUrl,null);assert(product.plugins[5].verifiedAsPlugin);assert(product.plugins[2].verifiedAsPlugin);
});
test('Timer counts down, holds on automatic pause, resumes, finishes and loops without negative time',()=>{
 assert.equal(sampleTimer(1000).remaining,12);assert.equal(sampleTimer(3500).remaining,9.5);assert.equal(sampleTimer(4200).remaining,9);assert.equal(sampleTimer(6100).remaining,9);assert(sampleTimer(5000).paused);assert.equal(sampleTimer(7500).remaining,8);assert(sampleTimer(16000).finished);assert.equal(sampleTimer(18000).remaining,12);for(let t=0;t<36000;t+=20){const s=sampleTimer(t);assert(s.remaining>=0&&s.remaining<=12);assert(s.progress>=0&&s.progress<=1);}
});
test('Synthetic layout changes use three separate languages with a typing phase',()=>{
 assert.deepEqual([sampleLayout(1200).index,sampleLayout(4800).index,sampleLayout(8400).index],[0,1,2]);assert(sampleLayout(3700).transition);assert.equal(sampleLayout(6400).typing,1);assert.equal(sampleLayout(10800).index,0);
});
test('Every language has the new architecture and seven matching FAQ entries; promised SDK freedom remains upcoming',()=>{
 const en=JSON.parse(fs.readFileSync('src/locales/en.json','utf8')).model;
 for(const file of fs.readdirSync('src/locales')){const data=JSON.parse(fs.readFileSync(`src/locales/${file}`,'utf8'));assert.equal(data.model.pluginNames.length,6);assert.equal(data.model.faq.length,7);assert.equal(data.model.workflowSteps.length,5);if(file!=='en.json')assert.notEqual(data.model.intro,en.intro,file);assert(!data.content.interfaceNote.includes('KeyOverlay'));}
 assert.match(en.developerText,/freely/);assert.match(en.licenseNote,/being updated/);assert.match(en.licenseNote,/current package/i);
});

