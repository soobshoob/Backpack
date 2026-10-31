import settings from './settings.json';
import {safeHttps} from './validation';
export interface IntegrationConfig{
 store:{enabled:boolean;published:boolean;url:string|null};
 github:{owner:string|null;repository:string|null;artifact:{filenameTemplate:string;platform:string;architecture:string;packageType:string;checksumFilename:string};cacheMinutes:number};
 registry:{url:string|null;catalogGithubUrl:string|null;contributeUrl:string|null;cacheMinutes:number};
 support:{monobank:{enabled:boolean;url:string|null};buyMeACoffee:{enabled:boolean;url:string|null}};
 licensing:{releases:Record<string,{name:string;url:string;signature:string}>;sdk:{name:string;url:string;decisionPending:boolean};examples:{name:string;url:string;decisionPending:boolean}};
}
export function validateConfig(config:IntegrationConfig){
 const issues:string[]=[];const url=(v:unknown,key:string,host?:string)=>{if(v!==null&&(!safeHttps(v)||host&&!([host,...(host==='buymeacoffee.com'?['www.buymeacoffee.com']:[])].includes(new URL(String(v)).hostname))))issues.push(key);};
 url(config.store.url,'store.url','apps.microsoft.com');url(config.registry.url,'registry.url');url(config.registry.catalogGithubUrl,'registry.catalogGithubUrl','github.com');url(config.registry.contributeUrl,'registry.contributeUrl','github.com');url(config.support.monobank.url,'support.monobank.url','send.monobank.ua');url(config.support.buyMeACoffee.url,'support.buyMeACoffee.url','buymeacoffee.com');
 if(config.store.url&&safeHttps(config.store.url)&&!/^\/(?:detail|store\/detail)\/(?:[^/]+\/)?[A-Za-z0-9]{12}\/?$/.test(new URL(config.store.url).pathname))issues.push('store.url');
 const g=config.github;if((g.owner===null)!==(g.repository===null))issues.push('github.owner/repository');
 if(g.owner!==null&&(!/^[A-Za-z0-9][A-Za-z0-9-]{0,38}$/.test(g.owner)||!g.repository||!/^[A-Za-z0-9_.-]+$/.test(g.repository)))issues.push('github.owner/repository');
 if(!/^[A-Za-z0-9_.-]+\{version\}-win-x64\.zip$/.test(g.artifact.filenameTemplate)||g.artifact.packageType!=='portable-zip'||g.artifact.architecture!=='x64'||!/^[A-Za-z0-9_.-]+$/.test(g.artifact.checksumFilename))issues.push('github.artifact');
 for(const [key,value]of [['github.cacheMinutes',g.cacheMinutes],['registry.cacheMinutes',config.registry.cacheMinutes]] as const)if(!Number.isFinite(value)||value<1||value>60)issues.push(key);
 for(const [v,license]of Object.entries(config.licensing.releases)){if(!license.name||!safeHttps(license.url)||!['unsigned','signed','unknown'].includes(license.signature))issues.push('licensing.releases.'+v);}
 return issues;
}
export const integrations=settings as IntegrationConfig;
export const configIssues=validateConfig(integrations);
export function githubUrls(config:IntegrationConfig=integrations){const g=config.github;if(!g.owner||!g.repository||validateConfig(config).some(v=>v.startsWith('github.')))return null;const repo=`https://github.com/${g.owner}/${g.repository}`;return {repo,releases:repo+'/releases',api:`https://api.github.com/repos/${g.owner}/${g.repository}/releases/latest`};}
export function storeBadgeUrl(lang:string){const locale:Record<string,string>={en:'en-us',uk:'uk',de:'de',fr:'fr',es:'es',it:'it',pt:'pt-pt',nl:'nl',pl:'pl',cs:'cs',hu:'hu',ro:'ro',sv:'sv',da:'da',nb:'en-us',fi:'fi',lt:'lt',el:'el'};return `https://get.microsoft.com/images/${locale[lang]??'en-us'}%20dark.svg`;}
