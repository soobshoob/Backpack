import {useEffect,useRef,useState} from 'react';
import {X,ArrowLeft,ArrowRight} from 'lucide-react';
import {siteCopy} from '../siteCopy';
import type {Language} from '../i18n';
export function Gallery({slug,name,lang,images}:{slug:string;name:string;lang:Language;images?:{url:string;alt:string}[]}){
 const c=siteCopy(lang),[selected,setSelected]=useState(0),dialog=useRef<HTMLDialogElement>(null),opener=useRef<HTMLButtonElement|null>(null);
 useEffect(()=>{const node=dialog.current;return()=>{if(node?.open)node.close();};},[]);
 function close(){dialog.current?.close();opener.current?.focus();}
 const items=images??[0,1].map(i=>({url:`/previews/${slug}-${i+1}.webp`,alt:`${name} · ${c.synthetic} ${i+1}`}));
 const src=(i:number)=>items[i]?.url??items[0]?.url;
 return <section className="plugin-gallery" aria-label={c.gallery}><h2>{c.gallery}</h2><div className="gallery-thumbnails">{items.map((item,i)=><button key={i} type="button" aria-label={`${c.enlarge}: ${name} ${i+1}`} onClick={e=>{opener.current=e.currentTarget;setSelected(i);dialog.current?.showModal();}}><img src={src(i)} alt={item.alt} loading="lazy" width="960" height="540"/><span>{c.enlarge} ↗</span></button>)}</div>{!images&&<p>{c.synthetic}</p>}<dialog ref={dialog} className="gallery-dialog" aria-label={`${c.gallery}: ${name}`} onCancel={e=>{e.preventDefault();close();}} onClick={e=>{if(e.target===e.currentTarget)close();}} onKeyDown={e=>{if(e.key==='ArrowRight'||e.key==='ArrowLeft'){e.preventDefault();setSelected(v=>(v+(e.key==='ArrowRight'?1:items.length-1))%items.length);}}}><button type="button" className="icon-button gallery-close" onClick={close} aria-label={c.close}><X/></button><img src={src(selected)} alt={items[selected]?.alt??items[0]?.alt} width="960" height="540"/><div className="gallery-toolbar"><button type="button" className="icon-button" aria-label={c.previous} onClick={()=>setSelected(v=>(v+items.length-1)%items.length)}><ArrowLeft/></button><span>{selected+1} / {items.length}</span><button type="button" className="icon-button" aria-label={c.next} onClick={()=>setSelected(v=>(v+1)%items.length)}><ArrowRight/></button></div></dialog></section>;
}
