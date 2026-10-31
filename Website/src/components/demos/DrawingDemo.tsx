import {SceneBackdrop} from '../SceneBackdrop';
import { useDemo } from '../../hooks/useDemo';
import { sampleDrawing } from '../../animation/timeline';
import { attr, move, text } from '../../animation/dom';
import { DetailBoard } from '../LensScene';
import { Pointer } from '../Pointer';
import type { SceneProps } from './KeysDemo';
export function DrawingDemo({ t, d, paused, reduced, portrait=false }: SceneProps) {
  const demo = useDemo({ paused, reduced, duration: 10400, staticTime: 8200, render(root, time) {
    const s = sampleDrawing(time);
    attr(root, '.annotation-curve', 'stroke-dashoffset', 1 - s.line); attr(root, '.annotation-head', 'stroke-dashoffset', 1 - s.head); attr(root, '.annotation-box', 'stroke-dashoffset', 1 - s.box);
    attr(root, '.annotations', 'opacity', s.opacity); attr(root, '.concept-marker', 'opacity', time >= 8400 && time < 10000 ? 1 : 0);
    attr(root, '.arrow-tool', 'fill', s.tool === 'arrow' ? '#245b3d' : '#1b2a21'); attr(root, '.rectangle-tool', 'fill', s.tool === 'rectangle' ? '#245b3d' : '#1b2a21');
    let position = {x:s.x,y:s.y};
    const stroke = time >= 1800 && time < 3000 ? ['.annotation-curve', s.line] : time >= 3300 && time < 3900 ? ['.annotation-head',s.head] : time >= 6100 && time < 8100 ? ['.annotation-box',s.box] : null;
    if(stroke){const path=root.querySelector<SVGPathElement>(String(stroke[0]))!;position=path.getPointAtLength(path.getTotalLength()*Number(stroke[1]));}
    move(root, '.drawing-pointer', position.x, position.y); text(root, '.scene-caption', d.drawSteps[s.selecting ? 0 : 1]); root.dataset.stroke = s.line.toFixed(3);
  } });
  return <div {...demo} className="demo-v2 drawing-scene" data-scene="drawing">
    <svg className="scene-svg" viewBox={portrait?"0 -170 360 500":"0 0 360 190"} aria-hidden="true">{portrait&&<SceneBackdrop/>}<DetailBoard d={d} width={304}/>
      <rect x="311" y="15" width="39" height="160" rx="9" fill="#15231b" stroke="#3f5b49"/>
      <g><rect className="arrow-tool" x="315" y="54" width="31" height="31" rx="6" fill="#245b3d"/><path d="m322 77 16-16m-13 0h13v13" fill="none" stroke="#d5f1de" strokeWidth="1.7"/></g>
      <g><rect className="rectangle-tool" x="315" y="94" width="31" height="31" rx="6" fill="#1b2a21"/><rect x="322" y="102" width="17" height="15" rx="2" fill="none" stroke="#d5f1de" strokeWidth="1.7"/></g>
      <g className="annotations" fill="none" stroke="#ee7e6d" strokeWidth="3.5" strokeLinecap="round" strokeLinejoin="round">
        <path className="annotation-curve" d="M47 147Q154.5 69.5 262 59" pathLength="1" strokeDasharray="1" strokeDashoffset="1"/><path className="annotation-head" d="m244 48 18 11-20 16" pathLength="1" strokeDasharray="1" strokeDashoffset="1"/>
        <path className="annotation-box" d="M125 116H203V166H125Z" pathLength="1" strokeDasharray="1" strokeDashoffset="1" stroke="#e6bf68" strokeWidth="2.5"/>
      </g><g className="drawing-pointer" transform="translate(285 153)"><Pointer scale={.7}/></g>
    </svg><p className="scene-caption">{d.drawSteps[0]}</p>
  </div>;
}
