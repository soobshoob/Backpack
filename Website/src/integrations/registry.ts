import {integrations,configIssues} from './config';
import {safeHttps,SourceError,versionPattern,compareVersions} from './validation';
import {cachedResource,fetchText,type StorageLike} from './resource';
import {categories} from '../catalog';
export type Localized=Record<string,string>;
export interface RegistryPlugin{
 id:string;slug:string;name:Localized;author:{name:string;url?:string};summary:Localized;description:Localized;version:string;category:string;tags:string[];
 distribution:{kind:'package'|'release';url:string;sha256?:string;sizeBytes?:number};
 compatibility:{minBackpack:string;maxBackpack?:string;sdk:string;platforms:string[]};license:{name:string;url?:string};
 repositoryUrl?:string;icon?:string;images?:{url:string;alt:Localized}[];updatedAt?:string;capabilities?:string[];changelog?:Localized;
}
export interface Registry{schemaVersion:1;plugins:RegistryPlugin[]}
export function localized(value:Localized,lang:string){return value[lang]??value[lang.split('-')[0]]??value.en;}
function fail(path:string,kind='malformed'):never{throw new SourceError(kind,`Registry field: ${path}`);}
function object(value:unknown,path:string):Record<string,unknown>{if(!value||typeof value!=='object'||Array.isArray(value))fail(path);return value as Record<string,unknown>;}
function text(value:unknown,path:string,max=200){if(typeof value!=='string'||!value.trim()||value.length>max||/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/.test(value))fail(path);return value as string;}
function locale(value:unknown,path:string,max:number):Localized{const v=object(value,path);if(!('en'in v)||Object.keys(v).length>40)fail(path);return Object.fromEntries(Object.entries(v).map(([key,s])=>{if(!/^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$/.test(key))fail(path);return[key,text(s,path+'.'+key,max)];}));}
function url(value:unknown,path:string,image=false){if(!safeHttps(value)||image&&!/\.(png|jpe?g|webp)$/i.test(new URL(value).pathname))fail(path);return value as string;}
function strings(value:unknown,path:string,max=20){if(!Array.isArray(value)||value.length>max)fail(path);return value.map((s,i)=>text(s,`${path}[${i}]`,80));}
function keys(value:Record<string,unknown>,allowed:string[],path:string){if(Object.keys(value).some(k=>!allowed.includes(k)))fail(path+'.unknown-field');}
export function validateRegistry(raw:unknown):Registry{
 const root=object(raw,'root');if(root.schemaVersion!==1)fail('schemaVersion','schema');keys(root,['schemaVersion','plugins'],'root');if(!Array.isArray(root.plugins)||root.plugins.length>500)fail('plugins');
 const ids=new Set<string>(),slugs=new Set<string>();
 const plugins=root.plugins.map((value,i):RegistryPlugin=>{const p=object(value,`plugins[${i}]`),at=`plugins[${i}]`,id=text(p.id,at+'.id',150),slug=text(p.slug,at+'.slug',80);
  if(!/^[a-z0-9]+(?:[.-][a-z0-9]+)+$/.test(id)||!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug)||['publish','_route','__registry_plugin__'].includes(slug))fail(at+'.id/slug');if(ids.has(id)||slugs.has(slug))fail(at+'.id/slug','duplicate');ids.add(id);slugs.add(slug);
  const author=object(p.author,at+'.author'),distribution=object(p.distribution,at+'.distribution'),compat=object(p.compatibility,at+'.compatibility'),license=object(p.license,at+'.license');
  keys(p,['id','slug','name','author','summary','description','version','category','tags','distribution','compatibility','license','repositoryUrl','icon','images','updatedAt','capabilities','changelog'],at);keys(author,['name','url'],at+'.author');keys(distribution,['kind','url','sha256','sizeBytes'],at+'.distribution');keys(compat,['minBackpack','maxBackpack','sdk','platforms'],at+'.compatibility');keys(license,['name','url'],at+'.license');
  if(distribution.kind!=='package'&&distribution.kind!=='release')fail(at+'.distribution.kind');const packageUrl=url(distribution.url,at+'.distribution.url');if(distribution.kind==='package'&&!/\.bppkg$/i.test(new URL(packageUrl).pathname)||distribution.kind==='release'&&/\.(bppkg|zip|exe|msi)$/i.test(new URL(packageUrl).pathname))fail(at+'.distribution.url');
  if(distribution.sha256!==undefined&&(distribution.kind!=='package'||typeof distribution.sha256!=='string'||!/^[a-fA-F0-9]{64}$/.test(distribution.sha256)))fail(at+'.distribution.sha256');if(distribution.sizeBytes!==undefined&&(!Number.isSafeInteger(distribution.sizeBytes)||Number(distribution.sizeBytes)<=0))fail(at+'.distribution.sizeBytes');
  const version=text(p.version,at+'.version',80),minBackpack=text(compat.minBackpack,at+'.compatibility.minBackpack',80),sdk=text(compat.sdk,at+'.compatibility.sdk',20);if(!versionPattern.test(version)||!versionPattern.test(minBackpack)||!/^\d+\.\d+$/.test(sdk))fail(at+'.version/compatibility');
  if(compat.maxBackpack!==undefined&&(!versionPattern.test(String(compat.maxBackpack))||compareVersions(String(compat.maxBackpack),minBackpack)<0))fail(at+'.compatibility.maxBackpack');
  const platforms=strings(compat.platforms,at+'.compatibility.platforms');if(!platforms.length||platforms.some(v=>!['windows-x64','windows-arm64'].includes(v)))fail(at+'.compatibility.platforms');
  const category=text(p.category,at+'.category');if(!categories.slice(1).includes(category as never))fail(at+'.category');
  if(p.images!==undefined&&(!Array.isArray(p.images)||p.images.length>8))fail(at+'.images');
  if(p.updatedAt!==undefined&&(typeof p.updatedAt!=='string'||!/^\d{4}-\d{2}-\d{2}T/.test(p.updatedAt)||!Number.isFinite(Date.parse(p.updatedAt))))fail(at+'.updatedAt');
  const result:RegistryPlugin={id,slug,name:locale(p.name,at+'.name',100),author:{name:text(author.name,at+'.author.name',100),...(author.url?{url:url(author.url,at+'.author.url')}:{})},summary:locale(p.summary,at+'.summary',300),description:locale(p.description,at+'.description',12000),version,category,tags:strings(p.tags,at+'.tags'),distribution:{kind:distribution.kind,url:packageUrl,...(distribution.sha256?{sha256:String(distribution.sha256).toLowerCase()}:{}),...(distribution.sizeBytes?{sizeBytes:Number(distribution.sizeBytes)}:{})},compatibility:{minBackpack,sdk,platforms,...(compat.maxBackpack?{maxBackpack:String(compat.maxBackpack)}:{})},license:{name:text(license.name,at+'.license.name',200),...(license.url?{url:url(license.url,at+'.license.url')}:{})}};
  if(p.repositoryUrl!==undefined)result.repositoryUrl=url(p.repositoryUrl,at+'.repositoryUrl');if(p.icon!==undefined)result.icon=url(p.icon,at+'.icon',true);if(p.images)result.images=(p.images as unknown[]).map((item,n)=>{const image=object(item,at+'.images');keys(image,['url','alt'],at+'.images');return {url:url(image.url,at+`.images[${n}].url`,true),alt:locale(image.alt,at+`.images[${n}].alt`,300)};});if(p.updatedAt)result.updatedAt=String(p.updatedAt);if(p.capabilities!==undefined)result.capabilities=strings(p.capabilities,at+'.capabilities',50);if(p.changelog!==undefined)result.changelog=locale(p.changelog,at+'.changelog',12000);return result;
 });return {schemaVersion:1,plugins};
}
export const registryKey=integrations.registry.url&&!configIssues.includes('registry.url')?integrations.registry.url:null;
export function loadRegistry({url=registryKey,ttl=integrations.registry.cacheMinutes*60000,fetcher=fetch,force=false,store,now}:{url?:string|null;ttl?:number;fetcher?:typeof fetch;force?:boolean;store?:StorageLike;now?:()=>number}={}){
 if(!safeHttps(url))return Promise.reject(new SourceError('unconfigured','Registry URL not configured'));
 return cachedResource({key:'backpack:registry:v1:'+url,ttl,force,store,now,validate:validateRegistry,load:async()=>{let data;try{data=JSON.parse(await fetchText(url,fetcher));}catch(e){if(e instanceof SourceError)throw e;throw new SourceError('malformed','Invalid registry JSON');}return data;}});
}
export function filterRegistry(entries:RegistryPlugin[],query:string,category:string,lang:string){const q=query.trim().toLocaleLowerCase();return entries.filter(p=>(category==='all'||p.category===category)&&(!q||[localized(p.name,lang),localized(p.summary,lang),localized(p.description,lang),p.author.name,p.id,...p.tags].join(' ').toLocaleLowerCase().includes(q)));}
export function matchesKnownHost(entry:RegistryPlugin,version:string){const sdk=({'0.4.0-preview.4':'1.1','0.4.0-preview.3':'1.0'} as Record<string,string>)[version];if(!sdk)return false;const [major,minor]=entry.compatibility.sdk.split('.').map(Number),[hostMajor,hostMinor]=sdk.split('.').map(Number);return major===hostMajor&&minor<=hostMinor&&compareVersions(version,entry.compatibility.minBackpack)>=0&&(!entry.compatibility.maxBackpack||compareVersions(version,entry.compatibility.maxBackpack)<=0)&&entry.compatibility.platforms.includes('windows-x64');}
