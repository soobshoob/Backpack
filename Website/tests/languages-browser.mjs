import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import {launchTestChrome} from './chrome.mjs';
const directory='qa/languages';await fs.mkdir(directory,{recursive:true});
const ids=['en','uk','de','fr','es','it','pt','nl','pl','cs','hu','ro','sv','da','nb','fi','lt','el'];
const test=await launchTestChrome(),results={checks:[],layouts:[],errors:[],browser:test.browser.version()};
try{
 const context=await test.browser.newContext({locale:'lt-LT',reducedMotion:'reduce'}),page=await context.newPage();
 page.on('pageerror',e=>results.errors.push(String(e)));page.on('console',m=>{if(m.type()==='error')results.errors.push(m.text());});
 await page.goto('http://127.0.0.1:5173/');await page.waitForFunction(()=>document.documentElement.lang==='lt');assert.equal(await page.locator('header .language-picker').count(),0);assert.equal(await page.locator('footer .language-picker').count(),1);
 results.checks.push('Lithuanian system locale chosen automatically; language picker exists only in footer');
 async function select(id){await page.locator('.language-trigger').click();await page.locator(`[role=option][lang="${id}"]`).click();await page.waitForFunction(id=>document.documentElement.lang===id,id);}
 async function layout(tag){await page.waitForTimeout(45);const state=await page.evaluate(()=>{
  const nav=document.querySelector('.navigation'),r=nav.getBoundingClientRect();
  return{width:innerWidth,overflow:document.documentElement.scrollWidth>innerWidth+2,navVisible:getComputedStyle(nav).display!=='none',center:Math.abs((r.left+r.right)/2-document.documentElement.clientWidth/2),clipped:[...document.querySelectorAll('.language-trigger,.language-panel button,.navigation a,.footer-links a,.category-strip a,.switcher button,.made-in,.native-button,.native-card p,.native-setting-row')].filter(el=>el.getClientRects().length).filter(el=>{const r=el.getBoundingClientRect();return r.left<-.5||r.right>innerWidth+.5||el.scrollWidth>el.clientWidth+2}).map(el=>el.textContent),panel:(()=>{const el=document.querySelector('.language-panel');if(!el)return null;const r=el.getBoundingClientRect();return{top:r.top,bottom:r.bottom,left:r.left,right:r.right,scroll:el.scrollHeight>el.clientHeight};})()};});
  results.layouts.push({tag,...state});assert(!state.overflow,`${tag}: horizontal overflow`);assert.deepEqual(state.clipped,[],`${tag}: clipped text`);if(state.navVisible)assert(state.center<2,`${tag}: nav center ${state.center}`);if(state.panel){assert(state.panel.top>=0&&state.panel.bottom<=1001,`${tag}: panel outside viewport`);}
 }
 for(const id of ids){await page.setViewportSize({width:1440,height:1000});await select(id);
  for(const scale of [1,2]){await page.evaluate(scale=>document.documentElement.style.fontSize=`${scale*100}%`,scale);
   for(const width of [360,768,1440,1920]){await page.setViewportSize({width,height:1000});await page.locator('.footer').scrollIntoViewIfNeeded();await layout(`${id}-${width}-${scale}x`);await page.locator('.language-trigger').click();assert.equal(await page.getByRole('option').count(),18);assert.equal(await page.locator('[role=option][lang=ru]').count(),0);await layout(`${id}-${width}-${scale}x-open`);
    if(['en','lt','el'].includes(id)&&[360,1440].includes(width)&&scale===1)await page.screenshot({path:`${directory}/${id}-footer-${width}.png`});await page.keyboard.press('Escape');
   }
  }
  await page.evaluate(()=>document.documentElement.style.fontSize='100%');
 }
 await page.setViewportSize({width:1440,height:1000});await select('en');await page.evaluate(()=>scrollTo(0,0));await page.screenshot({path:`${directory}/header-centered.png`});
 await page.locator('.language-trigger').focus();await page.keyboard.press('ArrowDown');await page.keyboard.press('End');assert.equal(await page.locator('[role=option][lang=el]').evaluate(el=>el===document.activeElement),true);await page.keyboard.press('Enter');await page.waitForFunction(()=>document.documentElement.lang==='el');assert(await page.locator('.language-trigger').evaluate(el=>el===document.activeElement));
 await page.reload();await page.waitForFunction(()=>document.documentElement.lang==='el');assert.equal(await page.evaluate(()=>localStorage.getItem('backpack-language')),'el');
 await page.locator('.language-trigger').click();await page.keyboard.press('Home');await page.keyboard.press('Enter');await page.waitForFunction(()=>document.documentElement.lang==='en');
 await page.locator('.language-trigger').click();await page.keyboard.type('li');assert.equal(await page.locator('[role=option][lang=lt]').evaluate(el=>el===document.activeElement),true);await page.keyboard.press('Escape');assert.equal(await page.getByRole('listbox').count(),0);
 await page.locator('.language-trigger').click();await page.locator('.footer .brand').click();assert.equal(await page.getByRole('listbox').count(),0);
 results.checks.push('All 18 local translations switch successfully; 288 layouts with normal/double text, centered navigation, bounded scrollable list, no overflow','Arrow/Home/End/Enter/Escape/typeahead and outside click work; focus returns; explicit choice persists after reload');
 await select('el');await page.evaluate(()=>document.fonts.ready);assert(await page.evaluate(()=>document.fonts.check('16px "Backpack Greek"','Ελληνικά')));await page.evaluate(()=>scrollTo(0,0));await page.screenshot({path:`${directory}/greek-header.png`});
 const session=await page.context().newCDPSession(page);const heading=await page.locator('h1').evaluate(el=>{el.id='qa-font-heading';return el.textContent});await session.send('DOM.enable');await session.send('CSS.enable');const {root}=await session.send('DOM.getDocument');const {nodeId}=await session.send('DOM.querySelector',{nodeId:root.nodeId,selector:'#qa-font-heading'});results.greekFonts=(await session.send('CSS.getPlatformFontsForNode',{nodeId})).fonts;assert(results.greekFonts.some(f=>f.familyName.includes('Noto Serif')&&f.isCustomFont));await session.detach();results.checks.push('Greek heading uses bundled Noto Serif glyphs, verified by Chrome platform-font data');
 for(const [browserLanguage,stored,expected]of [['ru-RU','ru','en'],['ja-JP',null,'en'],['de-DE',null,'de'],['el-GR',null,'el'],['de-DE','uk','uk']]){const ctx=await test.browser.newContext({locale:browserLanguage});if(stored)await ctx.addInitScript(value=>localStorage.setItem('backpack-language',value),stored);const p=await ctx.newPage();await p.goto('http://127.0.0.1:5173/');await p.waitForFunction(expected=>document.documentElement.lang===expected,expected);await ctx.close();}
 results.checks.push('Unsupported system language and unsupported saved preference fall back to English; German/Greek system language works; stored Ukrainian overrides system');
 assert.deepEqual(results.errors,[]);await context.close();console.log(JSON.stringify({checks:results.checks,layouts:results.layouts.length,errors:results.errors},null,2));
}finally{await fs.writeFile(`${directory}/results.json`,JSON.stringify(results,null,2));await test.close();}


