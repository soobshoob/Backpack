import { readFile, access, readdir } from 'node:fs/promises';
import assert from 'node:assert/strict';
const html=await readFile('dist/index.html','utf8');
assert(html.includes('<h1>'),'Static HTML must include the heading without JavaScript');
assert.equal((html.match(/<article/g)||[]).length,6);
assert.equal((html.match(/data-role="core"/g)||[]).length,2);
assert.equal((html.match(/data-role="plugin"/g)||[]).length,4);
assert(html.includes('0.4.0-preview.4'));
assert(html.includes('not an open-source license'));
assert(html.includes('not a security sandbox'));
assert(!html.includes('href="file:'));
assert(!html.includes('href="undefined'));
assert(!html.includes('.bppkg"'));
assert(!html.includes('href="#"'),'No placeholder links');
assert(!html.includes('localhost'),'No development domain in production HTML');
assert(!html.includes('D:\\'),'No local filesystem references');
assert(html.includes('Publication in Microsoft Store is being prepared'));
assert(html.includes('GitHub Releases is not connected yet'));
assert(html.includes('support-neon'));
const urls=[...html.matchAll(/(?:src|href)="([^"#]+)"/g)].map(m=>m[1]).filter(u=>!u.startsWith('https:'));
for(const url of urls){let path='dist/'+url.split('#')[0].replace(/^\.?\//,'');if(!/\.[a-z0-9]+$/i.test(path))path+='/index.html';await access(path);}
for(const route of ['download','plugins','plugins/publish','developers','plugins/drawing','plugins/layout-indicator','plugins/lens','plugins/focus','plugins/cursor-magnifier','plugins/presentation-timer']){
 const page=await readFile(`dist/${route}/index.html`,'utf8');assert(page.includes('<h1>'));assert(!page.includes('src="./assets/'));assert(!page.includes('href="#"'));
 for(const [,url]of page.matchAll(/(?:src|href)="(\/[^"#]+)"/g)){let path='dist'+url.split('#')[0];if(!/\.[a-z0-9]+$/i.test(path))path+='/index.html';await access(path);}
}
for(const cssFile of (await readdir('dist/assets')).filter(f=>f.endsWith('.css'))){
 const css=await readFile('dist/assets/'+cssFile,'utf8');
 for(const [,url] of css.matchAll(/url\(([^)]+)\)/g)){assert(!/^https?:/.test(url),'Fonts must be local');await access(url.startsWith('/')?'dist'+url:new URL('../dist/assets/'+url,import.meta.url));}
}
for(const jsFile of (await readdir('dist/assets')).filter(f=>f.endsWith('.js'))){const js=await readFile('dist/assets/'+jsFile,'utf8');assert(!js.includes('backpack-fixture/test-repo'));assert(!js.includes('TESTFIXTURE123'));assert(!js.includes('9FIXTURE0001'));}
function luminance(hex){const c=hex.match(/\w\w/g).map(x=>parseInt(x,16)/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4);return c[0]*.2126+c[1]*.7152+c[2]*.0722;}
const pairs=[['text','f2f5f3','080d0b'],['secondary text','a3b0a9','121a16'],['button text','03170e','00a878'],['accent link','31c99a','080d0b'],['focus outline','31c99a','121a16']];
for(const [label,fg,bg] of pairs){const l=[luminance(fg),luminance(bg)].sort((a,b)=>b-a);const ratio=(l[0]+.05)/(l[1]+.05);assert(ratio>=4.5,`${label}: contrast too low`);console.log(`${label}: ${ratio.toFixed(2)}:1`);}
console.log(`PASS: home + 10 nested routes, two core tools/four featured author examples, two honest release channels, neon support, no fixture service URLs, local links/gallery/fonts and contrast pairs.`);
