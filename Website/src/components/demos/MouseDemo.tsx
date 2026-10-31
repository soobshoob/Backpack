import {SceneBackdrop} from '../SceneBackdrop';
import { useId, useState } from 'react';
import { useDemo } from '../../hooks/useDemo';
import { sampleMouse } from '../../animation/timeline';
import { attr, move, text } from '../../animation/dom';
import type { MousePreset } from '../../data/toolPresets';
import { DemoPointer } from '../Pointer';
import { Switcher } from '../Controls';
import type { SceneProps } from './KeysDemo';
export function MouseDemo({ d, paused, reduced, portrait=false }: SceneProps) {
  const [style, setStyle] = useState(0), preset = (['outline', 'solid', 'retro'] as MousePreset[])[style];
  const id = useId().replace(/[^a-zA-Z0-9_-]/g, '');
  const demo = useDemo({ paused, reduced, duration: 10000, staticTime: 2000, render(root, time) {
    const s = sampleMouse(time);
    move(root, '.demo-pointer', s.x, s.y);
    attr(root, '.mouse-left-fill', 'opacity', s.left ? 1 : 0); attr(root, '.mouse-right-fill', 'opacity', s.right ? 1 : 0);
    attr(root, '.mouse-wheel-down', 'opacity', s.wheel<0 ? 1 : 0); attr(root, '.mouse-wheel-up', 'opacity', s.wheel>0 ? 1 : 0); attr(root, '.cursor-click', 'opacity', s.left || s.right ? .8 : 0);
    attr(root, '.mouse-target', 'fill', s.selected ? '#184b33' : '#24342b'); attr(root, '.mouse-check', 'opacity', s.selected ? 1 : 0);
    attr(root, '.context-demo', 'opacity', s.menu ? 1 : 0); move(root, '.scrolling-list', 0, s.scroll);
    text(root, '.scene-caption', d.mouseSteps[s.stage]); root.dataset.stage = String(s.stage);
  } });
  return <div {...demo} className="demo-v2 mouse-scene" data-scene="mouse">
    <svg className="scene-svg" viewBox={portrait?"0 -170 360 500":"0 0 360 190"} aria-hidden="true">{portrait&&<SceneBackdrop/>}
      <defs><clipPath id={`${id}-list`}><rect x="18" y="29" width="324" height="147" rx="8"/></clipPath></defs>
      <rect x="1" y="1" width="358" height="188" rx="11" fill={portrait?"none":"#101d16"} stroke={portrait?"none":"#304c3b"}/>
      <text x="19" y="20" fill="#91b49e" fontSize="10">{d.boardTag}</text>
      <g clipPath={`url(#${id}-list)`}><g className="scrolling-list">{[0, 1, 2, 3].map(i => <g transform={`translate(22 ${38 + i * 40})`} key={i}>
        <rect className={i === 0 ? 'mouse-target' : ''} width="310" height="32" rx="6" fill="#24342b"/><rect x="10" y="9" width="13" height="13" rx="3" fill="#5b7b68"/>
        <text x="33" y="21" fontSize="12" fill="#e1eee4">{d.board[i % 3]}</text>
        {i === 0 && <path className="mouse-check" d="m282 16 4 4 8-9" fill="none" stroke="#71e7b6" strokeWidth="2" opacity="0"/>}
      </g>)}</g></g>
      <g className="context-demo" opacity="0" transform="translate(214 90)"><rect width="124" height="85" rx="7" fill="#111b16" stroke="#6c8b78"/>{d.context.map((s, i) => <text x="12" y={22 + 25 * i} fill="#e0ebe3" fontSize="11" key={i}>{s}</text>)}</g>
      <DemoPointer indicator preset={preset}/>
    </svg>
    <p className="scene-caption">{d.mouseSteps[0]}</p><Switcher labels={d.mouseStyles} value={style} onChange={setStyle} label={d.mouseStyle}/><p className="preset-name">{d.mouseNames[style]}</p>
  </div>;
}
