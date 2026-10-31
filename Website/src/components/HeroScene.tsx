import { useRef } from 'react';
import { useDemo } from '../hooks/useDemo';
import { sampleKeys } from '../animation/timeline';
import type { Copy } from '../i18n';
import { Key } from './Controls';
import { Pointer } from './Pointer';
export function HeroScene({paused,reduced,t}:{paused:boolean;reduced:boolean;t:Copy}) {
  const layer=useRef<HTMLDivElement>(null);
  const demo=useDemo({paused,reduced,duration:12000,staticTime:2000,render(root,time){
    root.querySelectorAll<HTMLElement>('.floating-cap').forEach((element,i)=>{
      const angle=[16,-17,17,25,28,-18][i];
      const phase=time/(4500+i*230)*Math.PI*2+i;
      element.style.transform=`translateY(${reduced?0:Math.sin(phase)*11}px) rotate(${angle+(reduced?0:Math.sin(phase)*2)}deg)`;
    });
    const s=sampleKeys(time);
    root.querySelectorAll<HTMLElement>('.hero-pulse .tool-key').forEach((el,i)=>el.dataset.pressed=String(s.pressed[i]));
    root.querySelectorAll<HTMLElement>('.hero-ring').forEach((el,i)=>{const p=((time/4000+i/3)%1);el.style.transform=`scale(${.6+p*1.5})`;el.style.opacity=String(reduced?.18:(1-p)*.25);});
  }});
  return <div {...demo} className="hero-scene hero-v2" aria-hidden="true" data-scene="hero" onPointerMove={event=>{
    if(paused||reduced||!matchMedia('(pointer:fine)').matches)return;
    const r=event.currentTarget.getBoundingClientRect();layer.current?.style.setProperty('--px',`${(event.clientX-r.left-r.width/2)/60}px`);layer.current?.style.setProperty('--py',`${(event.clientY-r.top-r.height/2)/60}px`);
  }}>
    <div className="scene-grid"/><div className="scene-word">Backpack</div><div className="hero-layer" ref={layer}>
      <div className="floating-cap ctrl hero-pulse"><Key preset="graphite" kind="modifier">Ctrl</Key></div>
      <div className="floating-cap letter-k hero-pulse"><Key preset="cream">K</Key></div>
      <div className="floating-cap letter-s"><Key preset="silver">S</Key></div><div className="floating-cap alt"><Key preset="cream" kind="modifier">Alt</Key></div>
      <div className="floating-cap small-key"><Key preset="lavender" kind="enter">↵</Key></div><div className="floating-cap emerald-key"><Key preset="mint">✦</Key></div>
      <div className="hero-pointer"><i className="hero-ring"/><i className="hero-ring"/><i className="hero-ring"/><svg viewBox="0 0 30 36"><g transform="translate(3 2)"><Pointer/></g></svg></div>
      <div className="scene-note">{t.sceneNote[0]}<br/>{t.sceneNote[1]}<svg viewBox="0 0 100 65"><path d="M90 3Q88 57 15 45m0 0 15-8m-15 8 15 8"/></svg></div>
    </div><span className="scene-coordinate">01 — 05 / {t.toolkit}</span>
  </div>;
}
