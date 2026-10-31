import { launchTestChrome } from './chrome.mjs';
import { writeFile } from 'node:fs/promises';
const chrome=await launchTestChrome(),page=await chrome.context.newPage(),timestamps=[];
try{
 await page.setViewportSize({width:1280,height:1000});await page.goto(process.env.BACKPACK_TEST_URL??'http://127.0.0.1:5173/');await page.evaluate(()=>document.fonts.ready);
 await page.locator('.language-trigger').click();await page.getByRole('option',{name:/^Українська/}).click();await page.waitForFunction(()=>document.documentElement.lang==='uk');
 await page.emulateMedia({reducedMotion:'no-preference'});await page.reload();await page.locator('#feature-0').evaluate(el=>el.scrollIntoView({block:'start',behavior:'instant'}));await page.locator('[data-scene="keys"] [role="radio"]').last().click();
 await page.waitForFunction(()=>{const time=Number(document.querySelector('[data-scene="keys"]').dataset.time);return time>=700&&time<950;});
 await page.locator('[data-scene="keys"]').screenshot({path:'qa/product-model/3d-idle.png'});timestamps.push({file:'3d-idle.png',time:await page.locator('[data-scene="keys"]').getAttribute('data-time')});
 await page.waitForFunction(()=>{const time=Number(document.querySelector('[data-scene="keys"]').dataset.time);return time>=2050&&time<2450;});
 await page.locator('.motion-button').evaluate(el=>el.click());await page.locator('[data-scene="keys"]').screenshot({path:'qa/product-model/3d-pressed.png'});timestamps.push({file:'3d-pressed.png',time:await page.locator('[data-scene="keys"]').getAttribute('data-time')});
 await page.emulateMedia({reducedMotion:'reduce'});
 await page.locator('#plugin-cursorLens').screenshot({path:'qa/product-model/cursor-card.png'});
 await page.locator('#plugin-lens').screenshot({path:'qa/product-model/window-card.png'});
 await page.setViewportSize({width:390,height:900});await page.locator('.brand').first().click();await page.waitForTimeout(300);await page.screenshot({path:'qa/product-model/mobile-final.png',fullPage:true});
 await page.setViewportSize({width:1280,height:900});await page.screenshot({path:'qa/product-model/desktop-final.png',fullPage:true});
 await writeFile('qa/product-model/detail-timestamps.json',JSON.stringify(timestamps,null,2));
 console.log('Saved final desktop/mobile, cursor/window cards and 3D idle/pressed states');
}finally{await chrome.close();}
