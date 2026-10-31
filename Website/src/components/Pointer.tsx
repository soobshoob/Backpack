import { useId } from 'react';
import { mousePresets, type MousePreset } from '../data/toolPresets';

/** Pointer hotspot is (0,0), independent of highlight and button indicator. */
export function Pointer({ scale = 1 }: { scale?: number }) {
  return <g className="system-pointer" transform={`scale(${scale})`}><path d="M0 0 0 23 6.2 17.3 10.9 27.4 15.1 25.4 10.4 15.8 19 15.8Z" fill="#fff" stroke="#171b19" strokeWidth="1.5" strokeLinejoin="round"/></g>;
}

export function MouseIndicator({ preset = 'outline' }: { preset?: MousePreset }) {
  const id = useId().replace(/[^a-zA-Z0-9_-]/g, ''), p = mousePresets[preset];
  return <g className={`tool-mouse mouse-${preset}`} data-mouse-preset={preset}>
    <defs><clipPath id={`${id}-body`}><rect x="6" y="4" width="28" height="48" rx="14"/></clipPath></defs>
    <rect x="0" y="0" width="40" height="56" rx="19" fill={p.plate}/>
    <g clipPath={`url(#${id}-body)`}>
      <rect x="6" y="4" width="28" height="48" fill={p.body}/>
      <rect x="6" y="4" width="14" height="18" fill={p.top}/><rect x="20" y="4" width="14" height="18" fill={p.top}/>
      <rect className="mouse-left-fill" x="6" y="4" width="14" height="18" fill={p.accent} opacity="0"/>
      <rect className="mouse-right-fill" x="20" y="4" width="14" height="18" fill={p.accent} opacity="0"/>
    </g>
    <rect x="6" y="4" width="28" height="48" rx="14" fill="none" stroke={preset === 'solid' ? 'none' : p.line} strokeWidth="2.5"/>
    <path d="M6 22H34M20 4V9M20 19V22" stroke={preset === 'solid' ? p.plate : p.line} strokeWidth={preset === 'solid' ? 2.2 : 2.4}/>
    <rect x="17" y="8.5" width="6" height="11" rx="3" fill={preset === 'outline' ? p.plate : p.line} stroke={p.line} strokeWidth="1.7"/>
    <g className="mouse-wheel-up" opacity="0"><path d="m16 17 4-8 4 8M20 10V23" fill="none" stroke="#38BDF8" strokeWidth="2.3" strokeLinecap="round" strokeLinejoin="round"/></g>
    <g className="mouse-wheel-down" opacity="0"><path d="m16 13 4 8 4-8M20 8V20" fill="none" stroke="#38BDF8" strokeWidth="2.3" strokeLinecap="round" strokeLinejoin="round"/></g>
  </g>;
}

export function DemoPointer({ indicator = false, preset = 'outline' }: { indicator?: boolean; preset?: MousePreset }) {
  return <g className="demo-pointer" transform="translate(55 130)">
    <circle className="cursor-highlight" r="22" fill="#FFD400" opacity=".35"/>
    <circle className="cursor-click" r="13" fill="none" stroke="#FFD400" strokeWidth="2" opacity="0"/>
    {indicator && <g transform="translate(-21 20) scale(.55)"><MouseIndicator preset={preset}/></g>}
    <Pointer scale={.78}/>
  </g>;
}
