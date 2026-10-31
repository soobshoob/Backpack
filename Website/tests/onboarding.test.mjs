import test from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';import ts from 'typescript';
async function load(path,replacements=[]){let code=ts.transpileModule(fs.readFileSync(path,'utf8'),{compilerOptions:{module:ts.ModuleKind.ESNext,target:ts.ScriptTarget.ES2022}}).outputText;for(const [a,b]of replacements)code=code.replace(a,b);return import('data:text/javascript;base64,'+Buffer.from(code).toString('base64'));}
const timeline=await load('src/animation/timeline.ts');const source=await load('src/animation/onboarding.ts',[["import { progress, smooth } from './timeline';",`const progress=${timeline.progress.toString()},smooth=${timeline.smooth.toString()},clamp=${timeline.clamp.toString()};`],["import { point, progress, smooth } from './timeline';",`const progress=${timeline.progress.toString()},smooth=${timeline.smooth.toString()},clamp=${timeline.clamp.toString()};`]]);
const copy=await load('src/onboardingCopy.ts');
test('Onboarding follows real navigation, applies one preset, runs Focus and returns through the brand',()=>{
 const sample=source.sampleOnboarding;assert.equal(source.onboardingDuration,32000);
 assert.deepEqual([1000,7000,11000,15000,22000,27000,30000].map(t=>sample(t).page),['home','style','catalog','detail','workspace','detail','home']);
 assert(!sample(6400).cream);assert(sample(6500).cream);assert(sample(8000).pressed);
 assert(sample(19500).selecting);assert(!sample(19500).dim);assert(sample(22000).dim);assert(!sample(22000).checked);assert(sample(22500).checked);assert(!sample(25000).dim);assert(sample(25200).escape);
 assert.equal(sample(17500).to,'area-start');assert(sample(18000).shortcut);assert.equal(sample(28700).to,'brand');
});
test('Three loops have matching state and a continuous return path, without installation/trust stages',()=>{
 for(let time=0;time<32000;time+=20){const a=source.sampleOnboarding(time);assert.deepEqual(source.sampleOnboarding(time+32000),a);assert.deepEqual(source.sampleOnboarding(time+64000),a);assert(a.travel>=0&&a.travel<=1);assert(Number.isFinite(a.travel));assert(a.selection>=0&&a.selection<=1);assert(!/enable|trust|install/.test(a.from+a.to));}
 assert.equal(source.sampleOnboarding(31999).to,'home-rest');assert.equal(source.sampleOnboarding(0).from,'home-rest');
});
test('All 18 existing languages have localized controls, seven stage captions and native navigation labels, with no Russian',()=>{
 const langs=fs.readdirSync('src/locales').map(f=>f.slice(0,-5));assert.equal(langs.length,18);assert(!langs.includes('ru'));
 for(const lang of langs){const c=copy.onboardingCopy(lang);assert.equal(c.stages.length,7);for(const value of Object.values(c).flat())assert(value.length>2,lang);if(lang!=='en'){assert.notEqual(c.description,copy.onboardingCopy('en').description);assert.notEqual(c.play,copy.onboardingCopy('en').play);}}
});
