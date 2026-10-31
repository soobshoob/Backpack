/** Opt-in development diagnostics. Excluded from production by import.meta.env.DEV. */
export function installInspector() {
  const panel=document.createElement('aside');
  panel.id='qa-inspector'; panel.setAttribute('aria-label','Development QA');
  panel.style.cssText='position:fixed;bottom:8px;left:8px;right:8px;z-index:200;max-width:660px;padding:9px 12px;background:#07130cf2;border:1px solid #51956a;border-radius:10px;color:#d6f6e3;font:12px/1.6 monospace;pointer-events:none';
  const status=document.createElement('div'),audit=document.createElement('div');panel.append(status,audit);document.body.append(panel);
  const playheads=()=>Object.fromEntries(Array.from(document.querySelectorAll<HTMLElement>('[data-scene]')).map(el=>[el.dataset.scene!,Number(el.dataset.elapsed??0)]));
  let hiddenAt=0,hiddenHeads:Record<string,number>={},lastHidden='No hidden-tab interval recorded';
  const visibility=()=>{
    if(document.hidden){hiddenAt=Date.now();hiddenHeads=playheads();}
    else if(hiddenAt){const now=playheads(),max=Math.max(0,...Object.entries(now).map(([scene,time])=>time-(hiddenHeads[scene]??time)));lastHidden=`Hidden ${Date.now()-hiddenAt} ms; maximum playhead advance: ${max} ms`;hiddenAt=0;}
  };
  document.addEventListener('visibilitychange',visibility);
  const update=()=>{
    const scenes=Array.from(document.querySelectorAll<HTMLElement>('[data-scene]'));
    status.textContent=`QA | ${document.hidden?'hidden':'visible'} | reduced motion: ${matchMedia('(prefers-reduced-motion:reduce)').matches?'on':'off'} | running: ${scenes.filter(el=>el.dataset.running==='true').length}/${scenes.length} | font: ${getComputedStyle(document.body).fontFamily}`;
    audit.textContent=lastHidden;
  };
  update();const interval=setInterval(update,200);
  window.addEventListener('pagehide',()=>{clearInterval(interval);document.removeEventListener('visibilitychange',visibility);panel.remove();},{once:true});
}
