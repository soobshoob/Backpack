import {integrations,githubUrls,type IntegrationConfig} from './config';
import {safeHttps,SourceError} from './validation';
import {cachedResource,fetchText,type StorageLike} from './resource';
export interface StableRelease{releaseId:number;version:string;tag:string;releaseUrl:string;assetName:string;assetUrl:string;size:number;sha256:string|null;hashSource:'digest'|'checksums'|null;checksumWarning:boolean}
const hashPattern=/^[a-fA-F0-9]{64}$/;
function releaseAssetUrl(value:unknown,config:IntegrationConfig,tag:string,name:string){if(!safeHttps(value))return false;const u=new URL(value);return u.origin==='https://github.com'&&decodeURIComponent(u.pathname)===`/${config.github.owner}/${config.github.repository}/releases/download/${tag}/${name}`&&!u.search&&!u.hash;}
export function parseRelease(raw:unknown,config:IntegrationConfig){
 const r=raw as Record<string,unknown>;if(!r||typeof r!=='object'||!Array.isArray(r.assets)||!Number.isSafeInteger(r.id)||typeof r.draft!=='boolean'||typeof r.prerelease!=='boolean')throw new SourceError('malformed','Release payload structure');
 if(r.draft||r.prerelease||typeof r.tag_name!=='string'||!/^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(r.tag_name))throw new SourceError('no-stable','Latest endpoint did not return an accepted stable tag');
 const tag=r.tag_name,version=tag.replace(/^v/,''),urls=githubUrls(config);if(!urls||r.html_url!==urls.releases+'/tag/'+tag)throw new SourceError('malformed','Release repository/tag URL mismatch');
 const assetName=config.github.artifact.filenameTemplate.replace('{version}',version),matches=r.assets.filter((asset:unknown)=>!!asset&&typeof asset==='object'&&(asset as Record<string,unknown>).name===assetName) as Record<string,unknown>[];
 if(matches.length===0)throw new SourceError('no-asset','No artifact matches the configured filename/platform/architecture');if(matches.length!==1)throw new SourceError('ambiguous-asset','More than one artifact matched');
 const a=matches[0];if(a.state!=='uploaded'||!Number.isSafeInteger(a.size)||Number(a.size)<=0||!releaseAssetUrl(a.browser_download_url,config,tag,assetName))throw new SourceError('malformed','Selected release asset is invalid');
 if(a.digest!==null&&a.digest!==undefined&&(typeof a.digest!=='string'||!/^sha256:[a-fA-F0-9]{64}$/.test(a.digest)))throw new SourceError('malformed','Invalid asset digest');
 const data:StableRelease={releaseId:Number(r.id),version,tag,releaseUrl:String(r.html_url),assetName,assetUrl:String(a.browser_download_url),size:Number(a.size),sha256:typeof a.digest==='string'?a.digest.slice(7).toLowerCase():null,hashSource:typeof a.digest==='string'?'digest':null,checksumWarning:false};
 const sums=r.assets.filter((asset:unknown)=>asset&&typeof asset==='object'&&(asset as Record<string,unknown>).name===config.github.artifact.checksumFilename) as Record<string,unknown>[];
 return {data,checksumUrl:sums.length===1&&sums[0].state==='uploaded'&&releaseAssetUrl(sums[0].browser_download_url,config,tag,config.github.artifact.checksumFilename)?String(sums[0].browser_download_url):null};
}
export function parseChecksum(text:string,filename:string){const matches=text.split(/\r?\n/).map(line=>/^([a-fA-F0-9]{64})[ \t]+\*?(.+?)\s*$/.exec(line)).filter((m):m is RegExpExecArray=>!!m&&m[2]===filename);if(matches.length!==1)return null;return matches[0][1].toLowerCase();}
export function validateStableCache(value:unknown,config:IntegrationConfig){const s=value as StableRelease,urls=githubUrls(config);if(!s||!urls||!Number.isSafeInteger(s.releaseId)||!/^\d+\.\d+\.\d+$/.test(s.version)||s.tag.replace(/^v/,'')!==s.version||s.releaseUrl!==urls.releases+'/tag/'+s.tag||s.assetName!==config.github.artifact.filenameTemplate.replace('{version}',s.version)||!releaseAssetUrl(s.assetUrl,config,s.tag,s.assetName)||!Number.isSafeInteger(s.size)||s.size<=0||s.sha256!==null&&!hashPattern.test(s.sha256)||!['digest','checksums',null].includes(s.hashSource)||typeof s.checksumWarning!=='boolean')throw new SourceError('malformed','Invalid cached release');return s;}
export function loadStableRelease({config=integrations,fetcher=fetch,force=false,store,now}:{config?:IntegrationConfig;fetcher?:typeof fetch;force?:boolean;store?:StorageLike;now?:()=>number}={}){
 const urls=githubUrls(config);if(!urls)return Promise.reject(new SourceError('unconfigured','GitHub release repository not configured'));
 return cachedResource({key:`backpack:releases:v1:${config.github.owner}/${config.github.repository}:${config.github.artifact.filenameTemplate}`,ttl:config.github.cacheMinutes*60000,force,store,now,validate:v=>validateStableCache(v,config),load:async()=>{
  let raw:unknown;try{raw=JSON.parse(await fetchText(urls.api,fetcher,{'Accept':'application/vnd.github+json','X-GitHub-Api-Version':'2026-03-10'}));}catch(e){if(e instanceof SourceError){if(e.kind==='not-found')throw new SourceError('no-stable','No public stable release (or repository unavailable)');throw e;}throw new SourceError('malformed','Invalid release JSON');}
  const {data,checksumUrl}=parseRelease(raw,config);if(!data.sha256&&checksumUrl){try{data.sha256=parseChecksum(await fetchText(checksumUrl,fetcher,{},256*1024),data.assetName);data.hashSource=data.sha256?'checksums':null;data.checksumWarning=!data.sha256;}catch{data.checksumWarning=true;}}return data;
 }});
}
