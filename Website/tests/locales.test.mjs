import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import ts from 'typescript';
const source=fs.readFileSync('src/language.ts','utf8');
const {languages,resolveLanguage}=await import('data:text/javascript;base64,'+Buffer.from(ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.ESNext}}).outputText).toString('base64'));
const en=JSON.parse(fs.readFileSync('src/locales/en.json','utf8'));
function shape(value){return Array.isArray(value)?value.map(shape):value&&typeof value==='object'?Object.fromEntries(Object.entries(value).sort(([a],[b])=>a.localeCompare(b)).map(([k,v])=>[k,shape(v)])):typeof value;}
function strings(value){return typeof value==='string'?[value]:Object.values(value).flatMap(strings);}
test('Every selectable language has complete local content and demo copy with matching schema',()=>{
 assert.equal(languages.length,18);assert(!languages.some(v=>v.id==='ru'));assert(!fs.existsSync('src/locales/ru.json'));
 for(const {id} of languages){const locale=JSON.parse(fs.readFileSync(`src/locales/${id}.json`,'utf8'));assert.deepEqual(shape(locale),shape(en),id);assert(locale.content.madeIn.endsWith('.'),id);assert(strings(locale).every(v=>v.trim()===v&&v.length>0),id);assert(!strings(locale).some(v=>/undefined|\[object Object\]/.test(v)));if(id!=='en')assert.notEqual(locale.content.hero.join(''),en.content.hero.join(''),id);}
});
test('System preferences negotiate supported European language, then English; stored choice wins',()=>{
 for(const {id}of languages)assert.equal(resolveLanguage(null,[`${id}-XX`]),id);
 assert.equal(resolveLanguage('ru',['ru-RU','lt-LT']),'lt');assert.equal(resolveLanguage(null,['ja-JP','el-GR','en-US']),'el');assert.equal(resolveLanguage(null,['ja-JP','ko-KR']),'en');assert.equal(resolveLanguage('fr',['de-DE']),'fr');
});
