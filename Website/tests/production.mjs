import assert from 'node:assert/strict';
import { writeFile, mkdir } from 'node:fs/promises';
import { launchTestChrome } from './chrome.mjs';
await mkdir('qa/languages',{recursive:true});
const test=await launchTestChrome(),results={checks:[],errors:[]};
try{
 const page=await test.context.newPage();page.on('pageerror',e=>results.errors.push(String(e)));
 page.on('console',m=>{if(m.type()==='error')results.errors.push(m.text());});
 await page.setViewportSize({width:1280,height:1000});await page.goto('http://127.0.0.1:5175/');await page.evaluate(()=>document.fonts.ready);
 assert.equal(await page.locator('article').count(),8);assert.equal(await page.locator('[data-scene]').count(),12);assert.equal(await page.locator('#qa-inspector').count(),0);
 await page.locator('.language-trigger').click();await page.getByRole('option',{name:/^Lietuvių/}).click();await page.waitForFunction(()=>document.documentElement.lang==='lt');
 await page.locator('[data-scene="profiles"]').evaluate(el=>el.scrollIntoView({block:'center',behavior:'instant'}));await page.getByRole('radio',{name:'Mėtų',exact:true}).click();await page.waitForTimeout(4500);assert.equal(await page.locator('[data-scene="profiles"]').getAttribute('data-selection'),'2');
 results.checks.push('Compiled page, two core tools, six plugin cards, twelve scenes, language picker, no dev inspector, manual profile retains priority during playback');
 const noJS=await test.browser.newContext({javaScriptEnabled:false,viewport:{width:390,height:900}}),staticPage=await noJS.newPage();
 await staticPage.goto('http://127.0.0.1:5175/');assert.equal(await staticPage.locator('h1').count(),1);assert.equal(await staticPage.locator('article').count(),8);assert(await staticPage.locator('#navigation').isVisible());assert(!(await staticPage.locator('.language-picker').isVisible()));
 await staticPage.screenshot({path:'qa/product-model/no-javascript.png'});await noJS.close();results.checks.push('JavaScript disabled: prerendered text, eight cards, visible mobile navigation, no dead language trigger');
 await page.goto('http://127.0.0.1:5175/#plugin-cursorLens');await page.locator('#plugin-cursorLens').screenshot({path:'qa/product-model/production-cursor-card.png'});
 assert.deepEqual(results.errors,[]);results.checks.push('No production hydration or console errors');console.log(results);
}finally{await writeFile('qa/product-model/production-results.json',JSON.stringify(results,null,2));await test.close();}
