import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { access, mkdir, readFile, rm } from 'node:fs/promises';
import path from 'node:path';

/** A disposable Chrome profile, with native visibility instead of focus emulation. */
export async function launchTestChrome() {
  const root=path.resolve('qa'),profile=path.join(root,`.chrome-test-${Date.now()}`);
  const executable=process.env.CHROME_PATH??path.join(process.env.ProgramFiles??'C:\\Program Files','Google','Chrome','Application','chrome.exe');
  await access(executable);await mkdir(profile,{recursive:true});
  const child=spawn(executable,['--headless=new','--remote-debugging-port=0','--remote-debugging-address=127.0.0.1',`--user-data-dir=${profile}`,'--no-first-run','--no-default-browser-check','about:blank'],{windowsHide:true,stdio:'ignore'});
  let browser;
  try{
    let port='';
    for(let i=0;i<200;i++){try{port=(await readFile(path.join(profile,'DevToolsActivePort'),'utf8')).split(/\r?\n/)[0];break;}catch{await new Promise(r=>setTimeout(r,50));}}
    if(!port)throw Error('Test Chrome did not start');
    browser=await chromium.connectOverCDP(`http://127.0.0.1:${port}`,{noDefaults:true});
    const context=browser.contexts()[0];
    return {browser,context,async close(){
      await browser.close().catch(()=>{});child.kill();await new Promise(r=>setTimeout(r,400));
      if(!profile.startsWith(root+path.sep))throw Error('Unsafe test profile path');
      await rm(profile,{recursive:true,force:true,maxRetries:5,retryDelay:200});
    }};
  }catch(error){await browser?.close().catch(()=>{});child.kill();throw error;}
}
