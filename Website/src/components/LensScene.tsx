import {SceneBackdrop} from './SceneBackdrop';
import { useId } from 'react';
import type { DemoCopy } from '../demoCopy';
import { attr, text } from '../animation/dom';

export function DetailBoard({ d, width = 360, height = 190 }: { d: DemoCopy; width?: number; height?: number }) {
  const narrow = width < 240;
  return <g className="detail-board">
    <rect width={width} height={height} rx="10" fill="#1a2521"/>
    <rect x="0" y="0" width={width} height="27" rx="9" fill="#293b32"/>
    <circle cx="13" cy="14" r="3" fill="#80a390"/><circle cx="24" cy="14" r="3" fill="#577e67"/>
    <text x="39" y="18" fill="#e4eee7" fontSize="10">{d.boardTag}</text>
    <text x="14" y="49" fill="#eaf5ee" fontSize={narrow ? 13 : 16} fontWeight="600">{d.boardHeading}</text>
    {[0, 1, 2].map(i => <g key={i} transform={`translate(13 ${63 + i * (narrow ? 49 : 36)})`}>
      <rect width={width - 26} height={narrow ? 40 : 29} rx="5" fill={i === 1 ? '#223f32' : '#243029'}/>
      <text x="8" y="18" fontSize="10" fill="#72d9b4">0{i + 1}</text>
      <text x="32" y="18" fontSize={narrow ? 9 : 10} fill="#d8e6de">{d.boardRows[i]}</text>
      {!narrow && <text x={width - 73} y="18" fontSize="10" fill="#91b9a4">{['A1', 'B2', 'C3'][i]}</text>}
      {narrow && <text x="32" y="31" fontSize="8" fill="#a4c7b4">{['A1 · 24', 'B2 · 48', 'C3 · 72'][i]}</text>}
    </g>)}
    {!narrow && <text x="15" y={height - 9} fontSize="8" fill="#94b5a2">A1 · B2 · C3 / 24 · 48 · 72</text>}
  </g>;
}

export function LensScene({ d, width = 360, height = 190, lensWidth = 104, lensHeight = 72, initialZoom = 2.5, variant='cursor',portrait=false }: {
  d: DemoCopy; width?: number; height?: number; lensWidth?: number; lensHeight?: number; initialZoom?:number; variant?:'cursor'|'window';portrait?:boolean;
}) {
  const id = useId().replace(/[^a-zA-Z0-9_-]/g, ''), x = width / 2, y = height / 2, radius=variant==='cursor'?Math.min(lensWidth,lensHeight)/2:10;
  return <svg className="lens-canvas" data-variant={variant} data-lens-width={lensWidth} data-lens-height={lensHeight} viewBox={portrait?`0 -170 ${width} 500`:`0 0 ${width} ${height}`} width={width} height={height} aria-hidden="true">
    <defs>
      <clipPath id={`${id}-clip`}><rect data-lens-clip="" x={x - lensWidth / 2} y={y - lensHeight / 2} width={lensWidth} height={lensHeight} rx={radius}/></clipPath>
      <mask id={`${id}-mask`} maskUnits="userSpaceOnUse" x="0" y={portrait?-170:0} width={width} height={portrait?500:height}>
        <rect y={portrait?-170:0} width={width} height={portrait?500:height} fill="white"/><rect data-lens-hole="" x={x - lensWidth / 2} y={y - lensHeight / 2} width={lensWidth} height={lensHeight} rx={radius} fill="black"/>
      </mask>
    </defs>
    {portrait&&<SceneBackdrop/>}<DetailBoard d={d} width={width} height={height}/>
    <rect data-source-region="" x="65" y="67" width="42" height="29" rx="4" fill="none" stroke="#7db79a" strokeWidth="1" strokeDasharray="3 3" opacity={variant==='window'?1:0}/>
    <rect data-dimming="" y={portrait?-170:0} width={width} height={portrait?500:height} fill="#020805" mask={`url(#${id}-mask)`} opacity="0"/>
    <g data-lens-copy="" clipPath={`url(#${id}-clip)`}><g data-magnified="" transform={`translate(${(1-initialZoom)*x} ${(1-initialZoom)*y}) scale(${initialZoom})`}><DetailBoard d={d} width={width} height={height}/></g></g>
    <rect data-lens-frame="" x={x - lensWidth / 2} y={y - lensHeight / 2} width={lensWidth} height={lensHeight} rx={radius} fill="none" stroke="#63dfb5" strokeWidth="2"/>
    <g data-window-header="" opacity={variant==='window'?1:0} transform={`translate(${x-lensWidth/2} ${y-lensHeight/2-19})`}><path d={`M0 19V7Q0 0 7 0H${lensWidth-7}Q${lensWidth} 0 ${lensWidth} 7V19Z`} fill="#152d21" stroke="#63dfb5" strokeWidth="1.5"/><path d="M7 7h6M7 11h6" stroke="#aacbb7"/><text x={lensWidth/2+3} y="13" textAnchor="middle" fontSize="8.5" fill="#d7f3e3">{d.windowLabel}</text></g>
    <g transform={`translate(${width-49} 5)`}><rect width="41" height="20" rx="6" fill="#0b1b12" stroke="#385c47"/><text data-lens-scale="" x="20.5" y="14" textAnchor="middle" fill="#b8efd5" fontSize="10">{initialZoom.toFixed(1)}×</text></g>
  </svg>;
}

export function applyLens(svg: Element, x: number, y: number, zoom: number, dimmed: boolean, windowPosition?:{x:number;y:number}) {
  const w = Number(svg.getAttribute('data-lens-width')), h = Number(svg.getAttribute('data-lens-height'));
  const windowMode=svg.getAttribute('data-variant')==='window';
  const cx=windowMode&&!dimmed?(windowPosition?.x??x):x,cy=windowMode&&!dimmed?(windowPosition?.y??y):y;
  for (const selector of ['[data-lens-clip]', '[data-lens-frame]']) {
    attr(svg, selector, 'x', (cx - w / 2).toFixed(2)); attr(svg, selector, 'y', (cy - h / 2).toFixed(2));
  }
  attr(svg,'[data-lens-hole]','x',x-w/2);attr(svg,'[data-lens-hole]','y',y-h/2);
  attr(svg,'[data-source-region]','x',x-w/zoom/2);attr(svg,'[data-source-region]','y',y-h/zoom/2);attr(svg,'[data-source-region]','width',w/zoom);attr(svg,'[data-source-region]','height',h/zoom);attr(svg,'[data-source-region]','opacity',windowMode&&!dimmed?1:0);
  attr(svg,'[data-window-header]','opacity',windowMode&&!dimmed?1:0);attr(svg,'[data-window-header]','transform',`translate(${cx-w/2} ${cy-h/2-19})`);
  attr(svg, '[data-magnified]', 'transform', `translate(${(cx-x*zoom).toFixed(3)} ${(cy-y*zoom).toFixed(3)}) scale(${zoom.toFixed(3)})`);
  attr(svg, '[data-lens-copy]', 'opacity', dimmed ? 0 : 1);
  attr(svg, '[data-dimming]', 'opacity', dimmed ? .79 : 0);
  text(svg, '[data-lens-scale]', dimmed ? '1×' : `${zoom.toFixed(1)}×`);
  svg.setAttribute('data-zoom', zoom.toFixed(3)); svg.setAttribute('data-mode', dimmed ? 'dim' : 'lens');
}
