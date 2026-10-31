import {useRef} from 'react';
import {Backpack,Keyboard,Mouse,Palette,Monitor,Languages,Puzzle,Box,Layers,SlidersHorizontal,ShieldCheck,Info,HelpCircle,ChevronDown,ArrowLeft,RotateCcw,Pause,Play,Check,Power,Minus,Square,X} from 'lucide-react';
import type {Language,ModelCopy} from '../i18n';
import type {SceneProps} from './demos/KeysDemo';
import {onboardingCopy} from '../onboardingCopy';
import {sampleOnboarding,onboardingDuration} from '../animation/onboarding';
import {useOnboarding} from '../hooks/useOnboarding';

function Caps({cream=false,small=false}:{cream?:boolean;small?:boolean}){return <div className={`ob-caps ${cream?'ob-cream':''} ${small?'ob-caps-small':''}`}><span>Ctrl</span><span>⇧<small>Shift</small></span><span>K</span></div>;}
function Brand(){return <><Backpack/><strong>Backpack<span>.</span></strong></>;}
/** A localized, staged reconstruction of the inspected preview.5 UI, not an app embed. */
export function OnboardingDemo({t,d,a,lang,paused,reduced}:SceneProps&{a:ModelCopy;lang:Language}){
 const copy=onboardingCopy(lang),positions=useRef(new Map<string,{x:number;y:number}>());
 const demo=useOnboarding({paused,reduced,render(root,time){
  const s=sampleOnboarding(time);root.dataset.time=String(Math.round(time));root.dataset.step=String(s.stage);root.dataset.page=s.page;
  root.dataset.cream=String(s.cream);root.dataset.pressed=String(s.pressed);root.dataset.dim=String(s.dim);root.dataset.checked=String(s.checked);
  const viewport=root.querySelector<HTMLElement>('.ob-viewport')!;
  root.style.setProperty('--ob-progress',String(time/onboardingDuration));
  root.style.setProperty('--ob-opacity',String(s.opacity));
  root.style.setProperty('--ob-transition',String(s.transition));
  for(const [name,value]of [['style',s.styleScroll],['catalog',s.catalogScroll],['detail',s.detailScroll]] as const){const pane=root.querySelector<HTMLElement>(`[data-scroll="${name}"]`);if(pane)pane.style.transform=`translateY(${-value*(name==='style'?Math.min(115,pane.offsetHeight*.23):name==='catalog'?Math.min(220,pane.offsetHeight*.39):Math.min(330,pane.offsetHeight*.53))}px)`;}
  root.querySelector<HTMLElement>('.ob-shortcut')!.hidden=!s.shortcut&&!s.escape;
  root.querySelector<HTMLElement>('.ob-shortcut')!.textContent=s.escape?'Esc':'Ctrl + Alt + F';
  const selection=root.querySelector<HTMLElement>('.ob-selection')!;selection.hidden=!s.selecting&&!s.dim;
  const area=root.querySelector<HTMLElement>('[data-target="area-box"]')!,ar=area.getBoundingClientRect(),vr=viewport.getBoundingClientRect();
  selection.style.cssText=`left:${ar.left-vr.left}px;top:${ar.top-vr.top}px;width:${ar.width*(s.dim?1:s.selection)}px;height:${ar.height*(s.dim?1:s.selection)}px`;
  root.querySelectorAll<HTMLElement>('[data-target]').forEach(el=>{if(!el.getClientRects().length)return;const r=el.getBoundingClientRect();positions.current.set(el.dataset.target!,{x:r.left-vr.left+r.width/2,y:r.top-vr.top+r.height/2});});
  const from=positions.current.get(s.from)??{x:vr.width*.66,y:vr.height*.5},to=positions.current.get(s.to)??from;
  const pointer=root.querySelector<HTMLElement>('.ob-pointer')!;pointer.style.transform=`translate(${from.x+(to.x-from.x)*s.travel}px,${from.y+(to.y-from.y)*s.travel}px)`;
  const ring=pointer.querySelector<HTMLElement>('.ob-click')!;ring.style.opacity=s.click<0?'0':String(1-s.click);ring.style.transform=`translate(-50%,-50%) scale(${.5+Math.max(0,s.click)*1.6})`;
 }});
 const nav=[{key:'keys',text:copy.keys,Icon:Keyboard},{key:'style',text:t.chooseStyle,Icon:Palette,child:true},{key:'position',text:copy.position,Icon:Monitor,child:true},{key:'indicator',text:copy.indicator,Icon:Languages,child:true},{key:'mouse',text:t.mouse,Icon:Mouse},{key:'plugins',text:a.pluginsTab,Icon:Puzzle},{key:'focus',text:a.pluginNames[3],Icon:Box,child:true},{key:'lens',text:a.pluginNames[2],Icon:Box,child:true},{key:'timer',text:a.pluginNames[5],Icon:Box,child:true},{key:'profiles',text:t.profiles,Icon:Layers},{key:'settings',text:t.program.settings,Icon:SlidersHorizontal},{key:'appearance',text:t.appearance,Icon:Palette,child:true},{key:'privacy',text:t.program.privacy,Icon:ShieldCheck,child:true}];
 return <div ref={demo.ref} className="onboarding-demo" data-scene="onboarding" data-running={demo.running} data-reduced={reduced} data-page="home" data-step="0" data-time="0" data-elapsed="0" role="group" aria-label={copy.label}>
  <div className="ob-label"><span className="ob-label-dot"/>{copy.label}<span className="ob-length">0:32</span></div>
  <div className="ob-viewport" aria-hidden="true">
   <div className="ob-film">
    <div className="ob-window">
     <aside className="ob-sidebar"><div className="ob-brand" data-target="brand"><Brand/></div><div className="ob-nav">{nav.map(({key,text,Icon,child})=><div key={key} className={`ob-nav-item ${child?'ob-child':''}`} data-nav={key} data-target={key==='plugins'?'plugins':undefined}><Icon/><span>{text}</span>{['keys','plugins','settings'].includes(key)&&<ChevronDown className="ob-chevron"/>}</div>)}</div><div className="ob-help"><HelpCircle/><span>{t.nav[3]}</span></div></aside>
     <div className="ob-window-actions"><span data-target="minimize"><Minus/></span><span><Square/></span><span><X/></span></div>
     <div className="ob-content">
      <div className="ob-heading"><ArrowLeft/><span className="ob-title-home">{t.home}</span><span className="ob-title-style">{t.chooseStyle}</span><span className="ob-title-catalog">{a.pluginsTab}</span><span className="ob-title-detail">{a.pluginNames[3]}</span></div>
      <div className="ob-pages">
       <section className="ob-page ob-home" data-view="home">
        <div className="ob-card ob-home-hero"><div><div className="ob-hero-brand"><Brand/></div><p>{a.intro}</p><small>{t.toolsText}</small><Pause size={17}/></div><div className="ob-home-preview" data-target="home-rest"><Caps/><Caps cream/><Mouse/></div></div>
        <div className="ob-home-grid"><div className="ob-card"><b><Keyboard/>{t.categories[0]}</b><p>{t.cards[0][2]}</p><span className="ob-button" data-target="choose-style">{copy.choose}</span><span className="ob-button ob-secondary">{copy.settings}</span></div><div className="ob-card"><b><Mouse/>{t.mouse}</b><p>{t.cards[1][2]}</p><span className="ob-button">{copy.settings}</span></div><div className="ob-card ob-tools-card"><b><Puzzle/>{a.pluginsTab}</b><p>{a.extension}</p><span className="ob-button ob-primary">{a.pluginsTab}</span></div></div>
       </section>
       <section className="ob-page ob-style" data-view="style">
        <div className="ob-preview"><div className="ob-before"><Caps/></div><div className="ob-after"><Caps cream/></div></div>
        <div className="ob-scroll-window"><div data-scroll="style"><div className="ob-card"><b>{t.chooseStyle}</b><p className="ob-hint">{d.keyExamples}</p><div className="ob-style-grid">{['Low profile · classic','Low profile · graphite','Laptop · silver','Laptop · midnight','PBT · cream','PBT · ocean'].map((name,i)=><div key={name} className={`ob-style-tile ob-tile-${i}`} data-target={i===4?'cream':i===3?'style-scroll':undefined}><div className="ob-thumb"><Caps cream={i===4} small/></div><strong>{name}</strong><small>{a.builtin} · {d.families[i<2?0:i<4?1:2]}</small></div>)}</div><div className="ob-style-result"><Check/>{d.selected} · PBT · cream</div></div><div className="ob-card ob-setting-row"><span>{t.radius}</span><span className="ob-field">12</span></div></div></div>
       </section>
       <section className="ob-page ob-catalog" data-view="catalog"><div className="ob-scroll-window"><div data-scroll="catalog">
        <div className="ob-card"><b>{copy.builtin}</b><p>{a.coreText}</p><div className="ob-setting-row"><span><Keyboard/>{t.categories[0]}</span><span className="ob-button">{d.context[0]}</span></div><div className="ob-setting-row"><span><Mouse/>{t.mouse}</span><span className="ob-button">{d.context[0]}</span></div></div>
        <div className="ob-card"><b>{copy.external}</b><p className="ob-hint">{a.trust}</p><span className="ob-button ob-primary">{t.program.importPlugin}</span><div className="ob-setting-row ob-view-control"><span>{t.program.view}</span><span>{t.program.cards}　<span className="ob-button">{t.program.list}</span></span></div></div>
        <div className="ob-card ob-installed"><b>{copy.installed}</b>{[4,0,3,2,5].map(i=><div className={`ob-plugin-row ${i===3?'ob-focus-row':''}`} key={i}><Box/><div><strong>{a.pluginNames[i]}</strong><small>Backpack{i===3?` · ${a.enabled}`:''}</small></div><span className="ob-toggle"/><Power/><Info data-target={i===3?'focus-info':undefined}/></div>)}</div>
       </div></div></section>
       <section className="ob-page ob-detail" data-view="detail"><div className="ob-preview ob-focus-preview"><div className="ob-mini-board"><b>{d.boardHeading}</b>{d.boardRows.map((r,i)=><div key={r}><small>0{i+1}</small>{r}<i/></div>)}<div className="ob-mini-focus"/></div></div><div className="ob-scroll-window"><div data-scroll="detail">
        <div className="ob-card"><b>{a.pluginNames[3]}</b><span className="ob-button"><ArrowLeft/>{a.pluginsTab}</span><p>{a.pluginBenefits[3]}</p><div className="ob-setting-row"><span>{a.byAuthor}</span><strong>Backpack</strong></div><div className="ob-setting-row"><span>{a.enabled}</span><span className="ob-toggle"/></div><p className="ob-hint">{a.trust}</p></div>
        <div className="ob-card"><b>{t.dimming}</b><div className="ob-setting-row"><span>{t.dimming}</span><span className="ob-field">70 %</span></div><div className="ob-slider"><i/></div><div className="ob-setting-row"><span>{t.radius}</span><span className="ob-field">16</span></div></div>
        <div className="ob-card ob-commands"><b>{t.program.commands}</b><div className="ob-setting-row" data-target="command"><span>{a.pluginNames[3]} · {t.dimming}</span><span className="ob-field">Ctrl + Alt + F</span></div><div className="ob-setting-row"><span>{a.pluginNames[3]} · {copy.settings}</span><span className="ob-field">—</span></div></div>
       </div></div></section>
      </div>
     </div>
    </div>
    <div className="ob-workspace"><div className="ob-project"><div className="ob-project-bar"><i/><i/><i/><span>{d.boardTag}</span></div><div className="ob-project-body"><span className="ob-project-tag">Backpack</span><strong>{d.boardHeading}</strong><p>{d.board[0]}</p><div className="ob-area" data-target="area-box"><i className="ob-area-start" data-target="area-start"/>{d.boardRows.map((r,i)=><div className={`ob-work-row ob-work-row-${i}`} key={r}><span>0{i+1}</span><b>{r}</b>{i===1?<span className="ob-check" data-target="check"><Check/></span>:<span>→</span>}</div>)}<i className="ob-area-end" data-target="area-end"/></div><div className="ob-project-lines"><i/><i/><i/></div></div></div><div className="ob-taskbar"><span data-target="taskbar"><Backpack/></span></div></div>
    <div className="ob-selection" hidden/>
   </div>
   <div className="ob-pointer"><span className="ob-halo"/><span className="ob-click"/><svg width="26" height="34" viewBox="-2 -2 25 34"><path d="M0 0V23L6.2 17.3 10.9 27.4 15.1 25.4 10.4 15.8H19Z" fill="white" stroke="#10201a" strokeWidth="1.7" strokeLinejoin="round"/></svg></div>
   <div className="ob-shortcut" hidden/>
  </div>
  <div className="ob-playbar"><div className="ob-stage-caption">{copy.stages.map((text,i)=><span key={i} data-caption={i}>{text}</span>)}</div><div className="ob-play-controls"><button type="button" aria-label={demo.playing?copy.pause:copy.play} aria-pressed={demo.playing} onClick={demo.toggle}>{demo.playing?<Pause size={18}/>:<Play size={18}/>}</button><button type="button" aria-label={copy.restart} onClick={demo.restart}><RotateCcw size={18}/></button></div></div><div className="ob-progress"><i/></div>
 </div>;
}
