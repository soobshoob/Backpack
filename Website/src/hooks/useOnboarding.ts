import {useEffect,useRef,useState} from 'react';
import {ActiveClock} from '../animation/timeline';
import {onboardingDuration} from '../animation/onboarding';

/** Explicit playback is allowed under reduced motion; global pause always wins. */
export function useOnboarding({paused,reduced,render}:{paused:boolean;reduced:boolean;render:(root:HTMLDivElement,time:number)=>void}) {
 const ref=useRef<HTMLDivElement>(null),clock=useRef(new ActiveClock()),latest=useRef(render);
 latest.current=render;
 const [playing,setPlaying]=useState(!reduced),[visible,setVisible]=useState(false),[tab,setTab]=useState(true),[revision,setRevision]=useState(0);
 useEffect(()=>{setPlaying(!reduced);clock.current.suspend();},[reduced]);
 useEffect(()=>{
  const observer=new IntersectionObserver(([entry])=>{clock.current.suspend();setVisible(entry.isIntersecting);},{threshold:.12});
  if(ref.current)observer.observe(ref.current);
  const visibility=()=>{clock.current.suspend();setTab(!document.hidden);};visibility();document.addEventListener('visibilitychange',visibility);
  return()=>{observer.disconnect();document.removeEventListener('visibilitychange',visibility);};
 },[]);
 const running=playing&&!paused&&visible&&tab;
 const active=useRef(false);active.current=running;
 useEffect(()=>{
  // Re-anchor the paused cursor when responsive layout changes; never advance its clock.
  const observer=new ResizeObserver(()=>{const root=ref.current;if(root)latest.current(root,!active.current&&clock.current.elapsed<500?1000:clock.current.elapsed%onboardingDuration);});
  if(ref.current)observer.observe(ref.current);
  return()=>observer.disconnect();
 },[]);
 useEffect(()=>{if(ref.current)latest.current(ref.current,!running&&clock.current.elapsed<500?1000:clock.current.elapsed%onboardingDuration);});
 useEffect(()=>{
  clock.current.suspend();const root=ref.current;if(!root||!running)return;
  let frame=0;
  const tick=(now:number)=>{const elapsed=clock.current.advance(now);root.dataset.elapsed=String(Math.round(elapsed));latest.current(root,elapsed%onboardingDuration);frame=requestAnimationFrame(tick);};
  frame=requestAnimationFrame(tick);return()=>{cancelAnimationFrame(frame);clock.current.suspend();};
 },[running,revision]);
 return {ref,playing,running,toggle:()=>setPlaying(v=>!v),restart:()=>{clock.current=new ActiveClock();if(ref.current){ref.current.dataset.elapsed='0';latest.current(ref.current,1000);}setRevision(v=>v+1);}};
}
