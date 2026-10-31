import {useState} from 'react';
import {useDemo} from '../../hooks/useDemo';
import {sampleTimer,sampleLayout} from '../../animation/plugins';
import type {SceneProps} from './KeysDemo';
import type {ModelCopy} from '../../i18n';
export function TimerDemo({paused,reduced,a}:SceneProps & {a:ModelCopy}){
 const [manual,setManual]=useState<null|{running:boolean;remaining:number;elapsed:number}>(null),[running,setRunning]=useState(false);
 const demo=useDemo({paused,reduced,duration:18000,staticTime:5000,render(root,time){
  const elapsed=Number(root.dataset.elapsed??0),s=sampleTimer(time);
  const left=manual?Math.max(0,manual.remaining-(manual.running?Math.max(0,elapsed-manual.elapsed)/1000:0)):s.remaining;
  const active=manual?manual.running&&left>0:s.running;if(active!==running)setRunning(active);
  const shown=Math.ceil(left);root.dataset.remaining=String(shown);root.dataset.timerRunning=String(active);
  root.querySelector('.timer-digits')!.textContent=`00:${String(shown).padStart(2,'0')}`;
  root.querySelector('.timer-state')!.textContent=left<=0?a.finished:active?a.start:a.pause;
  (root.querySelector('.timer-progress i') as HTMLElement).style.width=`${(1-left/12)*100}%`;
 }});
 const toggle=()=>{const root=demo.ref.current;if(!root)return;const remaining=Number(root.dataset.remaining??12);setManual({running:!running,remaining:remaining||12,elapsed:Number(root.dataset.elapsed??0)});};
 return <div {...demo} className="demo-v2 timer-scene" data-scene="timer"><div className="timer-stage" aria-hidden="true"><div className="virtual-page"><i/><i/><i/><i/></div><div className="timer-panel"><strong className="timer-digits">00:09</strong><small className="timer-state">{a.pause}</small><div className="timer-progress"><i/></div></div></div><p className="scene-caption">{a.timerCaption}</p><div className="timer-controls"><button type="button" className="button secondary timer-toggle" onClick={toggle}>{running?a.pause:a.resume}</button><button type="button" className="button secondary" onClick={()=>{const root=demo.ref.current;setManual({running:false,remaining:12,elapsed:Number(root?.dataset.elapsed??0)});}}>{a.reset}</button></div></div>;
}
export function LayoutDemo({paused,reduced,a,d}:SceneProps & {a:ModelCopy}){
 const demo=useDemo({paused,reduced,duration:10800,staticTime:5900,render(root,time){const s=sampleLayout(time),code=['EN','UK','DE'][s.index];root.dataset.layout=code;root.querySelector('.layout-toast')!.textContent=code;root.querySelector('.layout-text')!.textContent=Array.from(d.safePhrase).slice(0,Math.ceil(Array.from(d.safePhrase).length*s.typing)).join('');root.querySelector('.layout-stage')!.setAttribute('data-transition',String(s.transition));root.querySelector('.scene-caption')!.textContent=`${a.layoutCaption} · ${a.layoutSequence[s.index]}`;}});
 return <div {...demo} className="demo-v2 layout-scene" data-scene="layout"><div className="layout-stage" aria-hidden="true"><div className="virtual-page"><i/><i/><i/></div><span className="layout-text">{d.safePhrase}</span><span className="layout-toast">UK</span></div><p className="scene-caption">{a.layoutCaption}</p></div>;
}
