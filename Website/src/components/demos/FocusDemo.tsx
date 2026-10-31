import { useState } from 'react';
import { useDemo } from '../../hooks/useDemo';
import { sampleFocus, point } from '../../animation/timeline';
import { move } from '../../animation/dom';
import { LensScene, applyLens } from '../LensScene';
import { Pointer } from '../Pointer';
import type { SceneProps } from './KeysDemo';
export function FocusDemo({t,d,paused,reduced,portrait=false,kind='lens'}:SceneProps & {kind?:'lens'|'focus'}){
 const demo=useDemo({paused,reduced,duration:12000,staticTime:5600,render(root,time){
  const s=sampleFocus(time),dimmed=kind==='focus';
  const position=point(time,[[0,235,103],[1500,235,103],[3300,258,132],[9500,258,132],[12000,235,103]]);
  applyLens(root.querySelector('.lens-canvas')!,s.x,s.y,dimmed?1:2.5,dimmed,position);
  const cursor=dimmed?{x:s.x+42,y:s.y+25}:time>=1500&&time<3300?{x:position.x,y:position.y-46}:point(time,[[0,320,161],[1500,235,57],[3300,258,86],[5000,106,116],[10000,106,116],[12000,320,161]]);
  move(root,'.lens-pointer',cursor.x,cursor.y);
 }});
 return <div {...demo} className="demo-v2 lens-demo" data-scene={kind==='focus'?'focus':'lens'}><div className="lens-stage" aria-hidden="true"><LensScene d={d} portrait={portrait} variant="window" initialZoom={kind==='focus'?1:2.5}/><svg className="pointer-overlay" viewBox={portrait?"0 -170 360 500":"0 0 360 190"}><g className="lens-pointer"><Pointer scale={.75}/></g></svg></div><p className="scene-caption">{kind==='focus'?t.dimming:d.windowCaption}</p></div>;
}
