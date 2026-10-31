import { useDemo } from '../../hooks/useDemo';
import { sampleFocus } from '../../animation/timeline';
import { move } from '../../animation/dom';
import { LensScene, applyLens } from '../LensScene';
import { Pointer } from '../Pointer';
import type { SceneProps } from './KeysDemo';
export function CursorLensDemo({d,paused,reduced,portrait=false}:SceneProps){
 const demo=useDemo({paused,reduced,duration:12000,staticTime:5600,render(root,time){const p=sampleFocus(time);applyLens(root.querySelector('.lens-canvas')!,p.x,p.y,2.5,false);move(root,'.cursor-lens-pointer',p.x,p.y);}});
 return <div {...demo} className="demo-v2 cursor-lens-scene" data-scene="cursor-lens"><div className="lens-stage" aria-hidden="true"><LensScene d={d} portrait={portrait} lensWidth={104} lensHeight={104}/><svg className="pointer-overlay" viewBox={portrait?"0 -170 360 500":"0 0 360 190"}><g className="cursor-lens-pointer"><Pointer scale={.7}/></g></svg></div><p className="scene-caption">{d.cursorCaption}</p></div>;
}
