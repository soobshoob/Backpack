import { useId, type CSSProperties, type ReactNode } from 'react';
import { keyPresets, type KeyPreset } from '../data/toolPresets';

export function presetVariables(preset: KeyPreset) {
  const p = keyPresets[preset];
  return { '--key-primary': p.primary, '--key-secondary': p.secondary, '--key-ink': p.ink, '--key-border': p.border, '--key-group': p.group } as CSSProperties;
}

/** The same face/body/label structure in hero, shortcuts, profiles and privacy. */
export function Key({ children, preset = 'cream', kind = 'normal', className = '', pressed = false }: {
  children: ReactNode; preset?: KeyPreset; kind?: 'normal' | 'modifier' | 'enter' | 'space' | 'long'; className?: string; pressed?: boolean;
}) {
  const id=useId().replace(/[^a-zA-Z0-9_-]/g,'');
  if(keyPresets[preset].family==='3d'){
    const w=kind==='normal'?108:kind==='long'?196:162;
    const enter=kind==='enter';
    const body=(press:number)=>enter
      ? `M63 ${4+press}H${w-12}Q${w-9} ${4+press} ${w-9} ${8+press}L${w-3} 86Q${w-2} 95 ${w-10} 95H10Q2 95 3 86V${57+press}Q3 ${53+press} 9 ${53+press}H52V${12+press}Q52 ${4+press} 63 ${4+press}Z`
      : `M14 ${4+press}H${w-14}Q${w-9} ${4+press} ${w-9} ${10+press}L${w-3} 86Q${w-2} 95 ${w-10} 95H10Q2 95 3 86L9 ${10+press}Q9 ${4+press} 14 ${4+press}Z`;
    const face=enter
      ? `M64 4H${w-16}Q${w-10} 4 ${w-10} 10L${w-8} 54Q${w-8} 62 ${w-16} 62H16Q8 62 8 54V36Q8 30 15 30H53V12Q53 4 64 4Z`
      : `M16 4H${w-16}Q${w-10} 4 ${w-10} 11L${w-8} 54Q${w-8} 62 ${w-16} 62H16Q8 62 8 54L10 11Q10 4 16 4Z`;
    return <span className={`tool-key family-3d svg-key key-${kind} ${className}`} style={presetVariables(preset)} data-pressed={pressed} data-preset={preset}>
      <svg viewBox={`0 0 ${w} 100`} aria-hidden="true">
        <defs><linearGradient id={`key-body-${id}`}><stop stopColor="var(--key-secondary)"/><stop offset="1" stopColor="var(--key-secondary)" stopOpacity=".78"/></linearGradient></defs>
        <path className="key3d-body key3d-idle" d={body(0)} fill={`url(#key-body-${id})`} stroke="var(--key-border)" strokeWidth="3" strokeLinejoin="round"/>
        <path className="key3d-body key3d-down" d={body(13)} fill={`url(#key-body-${id})`} stroke="var(--key-border)" strokeWidth="3" strokeLinejoin="round"/>
        <g className="key3d-top"><path d={face} fill="var(--key-primary)" stroke="var(--key-border)" strokeWidth="3" strokeLinejoin="round"/>
          <text className="key-label" x={enter?(w+53)/2:w/2} y="34" dominantBaseline="middle" textAnchor="middle" style={{fontSize:kind==='normal'?44:kind==='long'?23:26}} fill="var(--key-ink)">{children}</text>
        </g>
      </svg>
    </span>;
  }
  return <span className={`tool-key family-${keyPresets[preset].family} key-${kind} ${className}`} style={presetVariables(preset)} data-pressed={pressed} data-preset={preset}>
    <span className="key-body"/><span className="key-face"><span className="key-label">{children}</span></span>
  </span>;
}

export function KeyGroup({ children, preset = 'cream', className = '' }: { children: ReactNode; preset?: KeyPreset; className?: string }) {
  return <div className={`key-group family-${keyPresets[preset].family} ${className}`} style={presetVariables(preset)} data-preset={preset}>{children}</div>;
}

export function Switcher({ labels, value, onChange, label }: { labels: string[]; value: number; onChange: (n: number) => void; label: string }) {
  return <div className="switcher" role="radiogroup" aria-label={label}>{labels.map((s, i) => <button key={i} type="button" role="radio" aria-checked={value === i} tabIndex={value === i ? 0 : -1} onClick={() => onChange(i)} onKeyDown={event => {
    let next = i;
    if (['ArrowRight', 'ArrowDown'].includes(event.key)) next = (i + 1) % labels.length;
    else if (['ArrowLeft', 'ArrowUp'].includes(event.key)) next = (i - 1 + labels.length) % labels.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = labels.length - 1;
    else return;
    event.preventDefault(); onChange(next);
    (event.currentTarget.parentElement?.children[next] as HTMLButtonElement)?.focus();
  }}>{s}</button>)}</div>;
}

