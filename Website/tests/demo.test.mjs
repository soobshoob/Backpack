import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import ts from 'typescript';
async function load(path) {
  const input=await readFile(new URL(path,import.meta.url),'utf8');
  const {outputText}=ts.transpileModule(input,{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext}});
  return import('data:text/javascript;base64,'+Buffer.from(outputText).toString('base64'));
}
const {ActiveClock,samplePrivacy,sampleKeys,sampleMouse,sampleInterface,sampleDrawing,point}=await load('../src/animation/timeline.ts');
const {resolveLanguage,optionIndex}=await load('../src/language.ts');
const demoDictionaries={};for(const file of await readdir(new URL('../src/locales/',import.meta.url))){const data=JSON.parse(await readFile(new URL('../src/locales/'+file,import.meta.url),'utf8'));demoDictionaries[file.slice(0,-5)]=data.demo;}

for (const [language,copy] of Object.entries(demoDictionaries)) {
  test(`${language}: protection precedes dots; no protected or delayed keys across two complete cycles`,()=>{
    let firstDot=null,protectionStart=null;
    for(let time=0;time<26400;time+=5){
      const s=samplePrivacy(time,copy.safePhrase,copy.returnPhrase),local=time%13200;
      if(s.protectedInput){assert.deepEqual(s.keys,[]);assert.equal(s.phase,'protected');protectionStart??=local;}
      if(s.passwordDots>0&&local<9600){firstDot??=local;assert(s.protectedInput);}
      if(local>=4550&&local<10400)assert.deepEqual(s.keys,[]);
      if(local>=10400){for(const k of s.keys)assert(Array.from(copy.returnPhrase).includes(k));}
      assert(!Object.keys(s).some(k=>/password(text|value|characters)/i.test(k)));
    }
    assert(protectionStart<firstDot);
    assert.equal(samplePrivacy(9000,copy.safePhrase,copy.returnPhrase).passwordDots,8);
    assert.equal(samplePrivacy(12000,copy.safePhrase,copy.returnPhrase).normalText,copy.returnPhrase);
    assert.equal(samplePrivacy(13200,copy.safePhrase,copy.returnPhrase).normalText,'');
  });
}
test('Unicode characters are not split, including Ukrainian letters and a supplementary code point',()=>{
  const phrase='ІЇЄҐ 🙂',allowed=new Set(Array.from(phrase));
  for(let t=1800;t<4500;t+=10){const s=samplePrivacy(t,phrase,'Нова');for(const k of s.keys)assert(allowed.has(k));assert(!s.normalText.includes('\uFFFD'));}
});
test('Switching language during the protected phase cannot replay either language’s ordinary keys',()=>{
  for(const copy of Object.values(demoDictionaries))assert.deepEqual(samplePrivacy(6900,copy.safePhrase,copy.returnPhrase).keys,[]);
});
test('Active time freezes and resumes without accumulated hidden time, negative deltas, or a large jump',()=>{
  const clock=new ActiveClock();assert.equal(clock.advance(1000),0);assert.equal(clock.advance(1016),16);
  clock.suspend();clock.suspend();assert.equal(clock.advance(90000),16);assert.equal(clock.advance(90016),32);
  assert.equal(clock.advance(90000),32);assert.equal(clock.advance(99000),132);
});
test('A privacy pause/resume keeps the same protected state and produces only new return events',()=>{
  const clock=new ActiveClock();clock.elapsed=7000;clock.advance(1000);clock.suspend();clock.advance(800000);
  const c=demoDictionaries.uk,s=samplePrivacy(clock.elapsed,c.safePhrase,c.returnPhrase);
  assert.equal(clock.elapsed,7000);assert(s.protectedInput);assert.deepEqual(s.keys,[]);
  clock.elapsed=11000;clock.suspend();clock.advance(900000);const next=samplePrivacy(clock.elapsed,c.safePhrase,c.returnPhrase);
  assert.equal(next.phase,'return');assert.equal(next.normalText,Array.from(c.returnPhrase).slice(0,Math.floor(.4*Array.from(c.returnPhrase).length)).join(''));
});
test('Shortcut has ordered 220ms presses, a hold, individual release, and disappearance',()=>{
  assert.deepEqual(sampleKeys(1150).pressed,[true,false,false]);assert.deepEqual(sampleKeys(1400).pressed,[true,true,false]);assert.deepEqual(sampleKeys(1800).pressed,[true,true,true]);
  assert.deepEqual(sampleKeys(2900).pressed,[true,true,false]);assert.deepEqual(sampleKeys(3400).pressed,[false,false,false]);assert.equal(sampleKeys(4600).opacity,0);assert(sampleKeys(5300).layoutVisible);
});
test('Mouse clicks are independent; scrolling is drawn only at the wheel phase',()=>{
  assert(sampleMouse(1700).left);assert(!sampleMouse(1700).right);assert(sampleMouse(4500).right);assert(!sampleMouse(4500).left);assert(sampleMouse(4900).menu);
  assert.equal(sampleMouse(7800).wheel,-1);assert(sampleMouse(8200).scroll<0);assert.equal(sampleMouse(6400).wheel,0);
});
test('Cursor paths interpolate and close the loop continuously',()=>{
  const p=point(500,[[0,0,0],[1000,100,80]]);assert.equal(p.x,50);assert.equal(p.y,40);
  for(const sample of [sampleMouse,sampleInterface])for(const t of [1300,2200,4100,4400,5000,7400,8000,9100,10400,11900,13600]){
    const a=sample(t-1),b=sample(t+1);assert(Math.hypot(a.x-b.x,a.y-b.y)<5,`${sample.name} jump at ${t}`);
  }
});
test('Interface zoom, slider and actual magnification agree; dimming follows mode click',()=>{
  for(let t=2200;t<=4100;t+=10){const s=sampleInterface(t);assert(s.zoom>=1&&s.zoom<=2.5);assert(Math.abs(s.knob-(286+(s.zoom-1)/1.5*106))<1e-9);}
  assert.equal(sampleInterface(6500).zoom,2.5);assert(sampleInterface(10000).dimmed);assert(!sampleInterface(9000).dimmed);
});
test('Draw curve, arrowhead and second shape appear sequentially',()=>{
  assert.equal(sampleDrawing(1700).line,0);assert.equal(sampleDrawing(3000).line,1);assert.equal(sampleDrawing(3000).head,0);assert.equal(sampleDrawing(4000).head,1);
  assert.equal(sampleDrawing(6000).box,0);assert.equal(sampleDrawing(8200).box,1);assert.equal(sampleDrawing(10100).opacity,0);
});
test('Language selection validates storage, preserves explicit choice and has English fallback',()=>{
  assert.equal(resolveLanguage('ru','uk-UA'),'uk');assert.equal(resolveLanguage(null,'uk-UA'),'uk');assert.equal(resolveLanguage('__proto__','en-US'),'en');assert.equal(resolveLanguage('de','lt-LT'),'de');assert.equal(resolveLanguage(null,'RU-ru'),'en');assert.equal(resolveLanguage(null,['ja-JP','el-GR','en']),'el');assert.equal(resolveLanguage(null,'lt-LT'),'lt');assert.equal(resolveLanguage(null,'no-NO'),'nb');assert.equal(resolveLanguage(null,[]),'en');
});
test('Language list supports wraparound, Home and End without losing the selection',()=>{
  assert.equal(optionIndex(2,'ArrowDown',3),0);assert.equal(optionIndex(0,'ArrowUp',3),2);assert.equal(optionIndex(1,'Home',3),0);assert.equal(optionIndex(1,'End',3),2);assert.equal(optionIndex(1,'Escape',3),1);
});
