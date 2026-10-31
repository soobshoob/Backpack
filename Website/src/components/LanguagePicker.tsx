import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { Check, ChevronDown } from 'lucide-react';
import { languages, optionIndex, type Language } from '../language';

export function LanguagePicker({ value, onChange, label }: { value: Language; onChange: (value: Language) => void; label: string }) {
  const [open, setOpen] = useState(false), [active, setActive] = useState(languages.findIndex(v => v.id === value));
  const root = useRef<HTMLDivElement>(null), trigger = useRef<HTMLButtonElement>(null), panel = useRef<HTMLDivElement>(null), options = useRef<(HTMLButtonElement | null)[]>([]);
  const search = useRef({text:'', time:0});
  const listId = useId(), current = languages.find(l => l.id === value)!;
  function close(returnFocus = false) { setOpen(false); if (returnFocus) trigger.current?.focus({preventScroll:true}); }
  function show() { setActive(languages.findIndex(v => v.id === value)); search.current.text=''; setOpen(true); }
  useLayoutEffect(() => {
    if (!open) return;
    const place = () => {
      if (!panel.current || !trigger.current) return;
      const anchor = trigger.current.getBoundingClientRect(), width = panel.current.offsetWidth;
      // Prefer above the footer trigger; constrain the menu to the visible viewport.
      const roomAbove=anchor.top-20, roomBelow=innerHeight-anchor.bottom-20;
      const above=roomAbove>=Math.min(260,roomBelow);
      panel.current.style.maxHeight=`${Math.max(100, Math.min(384,above?roomAbove:roomBelow,innerHeight-24))}px`;
      const height=panel.current.offsetHeight;
      panel.current.style.left=`${Math.max(18,Math.min(anchor.left,innerWidth-width-18))}px`;
      panel.current.style.top=`${Math.max(12, Math.min(above?anchor.top-height-8:anchor.bottom+8,innerHeight-height-12))}px`;
    };
    place(); window.addEventListener('resize',place); window.addEventListener('scroll',place,true);
    return () => { window.removeEventListener('resize',place); window.removeEventListener('scroll',place,true); };
  }, [open]);
  useEffect(() => {
    if (!open) return;
    const option=options.current[active];option?.focus({preventScroll:true});
    if (option && panel.current) {
      const top=option.offsetTop, bottom=top+option.offsetHeight, viewport=panel.current;
      if(top<viewport.scrollTop)viewport.scrollTop=top-8;
      else if(bottom>viewport.scrollTop+viewport.clientHeight)viewport.scrollTop=bottom-viewport.clientHeight+8;
    }
  }, [open,active]);
  useEffect(() => {
    if (!open) return;
    const outside = (event: PointerEvent) => { if (event.target instanceof Node && !root.current?.contains(event.target)) setOpen(false); };
    document.addEventListener('pointerdown', outside);
    return () => document.removeEventListener('pointerdown', outside);
  }, [open]);
  return <div className="language-picker" ref={root} onBlur={event => { if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false); }} onKeyDown={event => {
    if (event.key === 'Escape' && open) { event.preventDefault(); event.stopPropagation(); close(true); }
  }}>
    <button type="button" ref={trigger} className="language-trigger" aria-label={`${label}: ${current.name}`} aria-haspopup="listbox" aria-expanded={open} aria-controls={listId} onClick={() => open ? close() : show()} onKeyDown={event => {
      if (['ArrowDown','ArrowUp'].includes(event.key)) { event.preventDefault(); show(); }
    }}><span className="language-label">{label}</span><span lang={current.id}>{current.name}</span><ChevronDown size={15}/></button>
    {open && <div className="language-panel" ref={panel} role="listbox" aria-label={label} id={listId} onKeyDown={event => {
      if (['ArrowDown','ArrowUp','Home','End'].includes(event.key)) { event.preventDefault(); setActive(optionIndex(active,event.key,languages.length)); }
      else if(event.key.length===1 && !event.ctrlKey && !event.metaKey && event.key!==' ') {
        const now=performance.now(); search.current.text=(now-search.current.time>800?'':search.current.text)+event.key.toLocaleLowerCase();search.current.time=now;
        const index=languages.findIndex(v=>v.name.toLocaleLowerCase().startsWith(search.current.text));
        if(index>=0){event.preventDefault();setActive(index);}
      }
    }}>{languages.map((language,i) => <button key={language.id} ref={el => { options.current[i]=el; }} type="button" role="option" aria-selected={value===language.id} tabIndex={-1} lang={language.id} onClick={() => { onChange(language.id); close(true); }}><span>{language.name}</span>{value===language.id && <Check size={15}/>}</button>)}</div>}
  </div>;
}
