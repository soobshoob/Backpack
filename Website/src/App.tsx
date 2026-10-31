import {DownloadBlock,SupportBlock} from './components/Distribution';
import {useEffect,useRef,useState} from 'react';
import {Backpack,ArrowUpRight,ArrowDown,Pause,Play,Menu,X} from 'lucide-react';
import {english,loadLocale,type Language} from './i18n';
import {resolveLanguage} from './language';
import {product} from './product';
import {LanguagePicker} from './components/LanguagePicker';
import {useRoute} from './router';
import {routeKind} from './catalog';
import {Home} from './Home';
import {CatalogPage,PluginPage,NotFound} from './pages/CatalogPages';
import {PublishPage} from './pages/PublishPage';
import {DevelopersPage} from './pages/DevelopersPage';
import {siteCopy} from './siteCopy';
export function App({initialPath='/'}:{initialPath?:string}){
 const route=useRoute(initialPath),kind=routeKind(route.path);
 const [locale,setLocale]=useState({lang:'en' as Language,bundle:english});
 const {lang,bundle}=locale,t=bundle.content,d=bundle.demo,a=bundle.model;
 const request=useRef(0),menuButton=useRef<HTMLButtonElement>(null);
 const [paused,setPaused]=useState(false),[reduced,setReduced]=useState(false),[menu,setMenu]=useState(false);
 async function selectLanguage(value:Language,persist=true){const revision=++request.current;try{const bundle=await loadLocale(value);if(revision!==request.current)return;setLocale({lang:value,bundle});if(persist)try{localStorage.setItem('backpack-language',value);}catch{}}catch{}}
 useEffect(()=>{let stored:null|string=null;try{stored=localStorage.getItem('backpack-language');}catch{}void selectLanguage(resolveLanguage(stored,navigator.languages?.length?navigator.languages:[navigator.language]),false);const mq=matchMedia('(prefers-reduced-motion: reduce)'),change=()=>setReduced(mq.matches);change();mq.addEventListener('change',change);return()=>mq.removeEventListener('change',change);},[]);
 useEffect(()=>{document.documentElement.lang=lang;document.title=kind==='home'?t.title:`${kind==='catalog'?c.catalogTitle:kind==='publish'?c.publicationTitle:kind==='developers'?c.devTitle:a.pluginsTab} — Backpack`;document.querySelector('meta[name="description"]')?.setAttribute('content',t.description);document.querySelector('meta[property="og:title"]')?.setAttribute('content',t.title);document.querySelector('meta[property="og:description"]')?.setAttribute('content',t.description);},[lang,t,route.path]);
 useEffect(()=>{if(!("IntersectionObserver" in window))return;const observer=new IntersectionObserver(entries=>entries.forEach(e=>{if(e.isIntersecting){e.target.setAttribute('data-revealed','true');observer.unobserve(e.target);}}),{threshold:.08});document.querySelectorAll('.reveal').forEach(el=>observer.observe(el));return()=>observer.disconnect();},[route.path]);
 useEffect(()=>{if(menu)document.querySelector<HTMLAnchorElement>('#navigation a')?.focus();},[menu]);
 useEffect(()=>{if(location.hash){const id=decodeURIComponent(location.hash.slice(1)),frame=requestAnimationFrame(()=>document.getElementById(id)?.scrollIntoView());return()=>cancelAnimationFrame(frame);}},[]);
 useEffect(()=>{const header=document.querySelector<HTMLElement>('.header'),nav=header?.querySelector<HTMLElement>('.navigation'),brand=header?.querySelector<HTMLElement>('.brand'),actions=header?.querySelector<HTMLElement>('.header-actions');if(!header||!nav||!brand||!actions)return;const fit=()=>{const width=Array.from(nav.children).reduce((sum,item)=>sum+item.getBoundingClientRect().width,0)+parseFloat(getComputedStyle(nav).columnGap||'0')*(nav.children.length-1);header.classList.toggle('stacked',header.clientWidth<width+Math.max(brand.scrollWidth,actions.scrollWidth)*2+64);};const observer=new ResizeObserver(fit);[header,nav,brand,actions].forEach(el=>observer.observe(el));fit();return()=>observer.disconnect();},[lang]);
 function closeMenu(returnFocus=false){setMenu(false);if(returnFocus)menuButton.current?.focus();}
 const still=paused||reduced,props={t,d,paused,reduced},c=siteCopy(lang);
 return <div className={`site ${still?'paused':''}`} id="top">
  {import.meta.env.MODE==='integration-test'&&<aside className="fixture-warning">TEST FIXTURES — not project releases, plugins or payment links.</aside>}
  <a className="skip" href="#main">{t.skip}</a>
  <header className="header wrap">
   <a className="brand" href="/" aria-label="Backpack"><Backpack/><span>Backpack<span className="brand-dot">.</span></span></a>
   <nav id="navigation" className={menu?'navigation open':'navigation'} aria-label={t.menu} onKeyDown={e=>{if(e.key==='Escape')closeMenu(true);}}>{[a.coreTab,a.pluginsTab,t.nav[2],t.nav[3]].map((label,i)=><a key={i} href={['/#tools','/plugins','/download','/#help'][i]} onClick={()=>closeMenu(true)}>{label}</a>)}</nav>
   <div className="header-actions"><button className="icon-button motion-button" aria-label={reduced?t.reduced:paused?t.play:t.pause} aria-pressed={still} disabled={reduced} onClick={()=>setPaused(v=>!v)}>{still?<Play size={16}/>:<Pause size={16}/>}</button><button className="icon-button menu-button" ref={menuButton} aria-expanded={menu} aria-controls="navigation" aria-label={menu?t.close:t.menu} onClick={()=>setMenu(v=>!v)}>{menu?<X/>:<Menu/>}</button></div>
  </header>
  <main id="main" tabIndex={-1}>{kind==='download'?<div className="download-page route-page"><DownloadBlock lang={lang} asPage/><SupportBlock lang={lang}/></div>:kind==='home'?<Home {...props} a={a} lang={lang}/>:kind==='catalog'?<CatalogPage {...props} a={a} lang={lang} params={route.params} onQuery={route.query}/>:kind==='publish'?<PublishPage lang={lang}/>:kind==='detail'?<PluginPage {...props} a={a} lang={lang} slug={route.path.split('/')[2]}/>:kind==='developers'?<DevelopersPage lang={lang} a={a}/>:<NotFound lang={lang}/>}</main>
  <footer className="footer"><div className="wrap footer-main"><div><a className="brand" href="/"><Backpack/><span>Backpack<span className="brand-dot">.</span></span></a><p>{t.footer}</p></div><div className="footer-controls"><LanguagePicker value={lang} onChange={value=>void selectLanguage(value)} label={t.language}/><div className="footer-links"><a href={product.author.url}>{t.author}: {product.author.name}<ArrowUpRight size={13}/></a><a href="/developers">{a.developerTag}</a><a href="/#help">{t.nav[3]}</a><a href={product.license.url}>{product.version} · {product.license.name}</a><a href="/" aria-label={t.back}><ArrowDown className="up" size={17}/></a></div></div></div><div className="wrap footer-bottom"><span>© {product.author.name}</span><span className="made-in" lang={lang}><span className="origin-flag" aria-hidden="true">🇺🇦</span> {t.madeIn}</span><span>{t.notice}</span></div></footer>
 </div>;
}

