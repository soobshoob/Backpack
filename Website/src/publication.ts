export const semver=/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?(?:\+[0-9A-Za-z.-]+)?$/;
export const draftKeys=['name','summary','description','category','version','minVersion','platforms','repository','icon','screenshots','package','capabilities','author','license'] as const;
export type Draft={name:string;summary:string;description:string;category:string;version:string;minVersion:string;platforms:string[];repository:string;capabilities:string;author:string;license:string};
export type DraftFiles={icon:File[];screenshots:File[];package:File[]};
export const emptyDraft:Draft={name:'',summary:'',description:'',category:'tools',version:'',minVersion:'',platforms:['Windows 10','Windows 11'],repository:'',capabilities:'',author:'',license:''};
export function validateDraft(d:Draft):Record<string,'required'|'versionError'|'urlError'>{
 const errors:Record<string,'required'|'versionError'|'urlError'>={};
 for(const key of ['name','summary','description','version','minVersion','author','license','capabilities'] as const)if(!d[key].trim())errors[key]='required';
 if(!d.platforms.length)errors.platforms='required';
 for(const key of ['version','minVersion'] as const)if(d[key]&&!semver.test(d[key]))errors[key]='versionError';
 if(d.repository){try{const u=new URL(d.repository);if(u.protocol!=='https:'||u.username||u.password)errors.repository='urlError';}catch{errors.repository='urlError';}}
 return errors;
}
export async function validImage(file:File){const a=new Uint8Array(await file.slice(0,12).arrayBuffer());return file.type==='image/png'&&[137,80,78,71,13,10,26,10].every((v,i)=>a[i]===v)||file.type==='image/jpeg'&&a[0]===255&&a[1]===216&&a[2]===255||file.type==='image/webp'&&String.fromCharCode(...a.slice(0,4))==='RIFF'&&String.fromCharCode(...a.slice(8,12))==='WEBP';}
export async function validateFiles(files:DraftFiles){const errors:Record<string,'fileError'|'required'>={};for(const key of ['icon','screenshots','package'] as const){const list=files[key];if(!list.length){errors[key]='required';continue;}if(list.length>(key==='screenshots'?5:1)){errors[key]='fileError';continue;}for(const f of list){if(!f.size||f.size>(key==='package'?100:key==='icon'?2:5)*1024*1024){errors[key]='fileError';continue;}if(key==='package'){const bytes=new Uint8Array(await f.slice(0,4).arrayBuffer());if(!f.name.toLowerCase().endsWith('.bppkg')||bytes[0]!==80||bytes[1]!==75||bytes[2]!==3||bytes[3]!==4)errors[key]='fileError';}else if(!await validImage(f))errors[key]='fileError';}}return errors;}
export function exportableDraft(draft:Draft,files:DraftFiles){return{format:'backpack-catalog-draft',formatVersion:1,publicationState:'local-draft',metadata:draft,files:Object.fromEntries(Object.entries(files).map(([key,items])=>[key,items.map((f:File)=>({name:f.name,size:f.size,type:f.type}))])),note:'Metadata draft only. Files are not embedded; nothing is submitted or published.'};}
