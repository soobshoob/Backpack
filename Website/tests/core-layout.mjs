import {launchTestChrome} from './chrome.mjs';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const test=await launchTestChrome(),results=[];
try{
 const p=await test.context.newPage();await p.emulateMedia({reducedMotion:'reduce'});await p.goto('http://127.0.0.1:5173');
 for(const scale of [1,2])for(const width of [360,390,768,1280,1920]){
  await p.setViewportSize({width,height:1000});await p.evaluate(scale=>document.documentElement.style.fontSize=`${scale*100}%`,scale);await p.waitForTimeout(60);
  const state=await p.locator('.core-section').evaluate(el=>({section:el.clientWidth,overflow:document.documentElement.scrollWidth>innerWidth+1,cards:[...el.querySelectorAll('article')].map(c=>{const r=c.getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height};})}));
  assert(!state.overflow,`${width}/${scale}: page overflow`);
  if(width>600){assert(Math.abs(state.cards[0].y-state.cards[1].y)<1);assert(state.cards[0].width>state.section*.45);}else assert(state.cards[1].y>state.cards[0].y);
  assert.equal(state.cards[0].height,(width>600?336:384)*scale);results.push({width,scale,...state});
 }
 await p.evaluate(()=>document.documentElement.style.fontSize='100%');await p.setViewportSize({width:390,height:1000});
 await p.locator('#tools').evaluate(el=>el.scrollIntoView({block:'start',behavior:'instant'}));await p.screenshot({path:'qa/product-model/compact-core-mobile.png'});
 const rail=p.locator('.catalog .showcase-viewport');await rail.scrollIntoViewIfNeeded();await rail.focus();await p.keyboard.press('End');await p.waitForTimeout(100);assert(await rail.evaluate(el=>el.scrollLeft+el.clientWidth>=el.scrollWidth-2));await p.keyboard.press('Home');await p.waitForTimeout(100);assert.equal(await rail.evaluate(el=>el.scrollLeft),0);
 for(const card of await p.locator('.catalog .showcase-card').all()){await card.evaluate(el=>el.scrollIntoView({block:'center',inline:'center',behavior:'instant'}));await card.locator('summary').click();assert(await card.locator('.showcase-info-panel').isVisible());await p.keyboard.press('Escape');assert.equal(await card.locator('details').getAttribute('open'),null);}
 console.log(`PASS: ${results.length} core layouts; plugin rail keyboard navigation and six detail panels`);
}finally{await fs.writeFile('qa/product-model/core-layout-results.json',JSON.stringify(results,null,2));await test.close();}
