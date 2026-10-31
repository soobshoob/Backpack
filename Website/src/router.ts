import {useEffect,useState} from 'react';
export function useRoute(initialPath='/'){
 const [url,setUrl]=useState(initialPath);
 useEffect(()=>{
  const update=()=>{setUrl(location.pathname+location.search);};
  const click=(e:MouseEvent)=>{
   if(e.defaultPrevented||e.button!==0||e.metaKey||e.ctrlKey||e.shiftKey||e.altKey)return;
   const a=(e.target as Element).closest<HTMLAnchorElement>('a[href]');if(!a||a.target||a.download)return;
   const u=new URL(a.href,location.href);if(u.origin!==location.origin||!['/','/plugins','/plugins/publish','/developers','/download'].includes(u.pathname)&&!u.pathname.startsWith('/plugins/'))return;
   if(u.pathname===location.pathname&&u.search===location.search&&u.hash)return;
   e.preventDefault();history.pushState({},'',u);update();
   requestAnimationFrame(()=>{if(u.hash)document.getElementById(decodeURIComponent(u.hash.slice(1)))?.scrollIntoView({behavior:'instant'});else{scrollTo({top:0,behavior:'instant'});document.querySelector<HTMLElement>('main')?.focus({preventScroll:true});}});
  };
  addEventListener('popstate',update);document.addEventListener('click',click);update();return()=>{removeEventListener('popstate',update);document.removeEventListener('click',click);};
 },[]);
 function query(params:URLSearchParams){const next=location.pathname+(params.size?'?'+params:'');history.pushState({},'',next);setUrl(next);}
 return{path:url.split('?')[0].replace(/\/$/,'')||'/',params:new URLSearchParams(url.split('?')[1]),query};
}
