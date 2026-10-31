// Canonical text refresh after the product-model generator, based on Phase 4G.
export function updateReleaseCopy(locale){
 function walk(value){if(typeof value==='string')return value.replaceAll('0.4.0-preview.3','0.4.0-preview.4').replaceAll('preview.3','preview.4').replaceAll('Preview.3','Preview.4');if(Array.isArray(value))return value.map(walk);if(value&&typeof value==='object')return Object.fromEntries(Object.entries(value).map(([k,v])=>[k,walk(v)]));return value;}
 const result=walk(locale),m=result.model;
 const facts=`${m.pluginNames.slice(2).join(', ')} — ${m.statuses.verified} · Backpack 0.4.0-preview.4 / SDK 1.1. ${m.pluginNames.slice(0,2).join(', ')} — ${m.statuses.planned}.`;
 m.transitionNote=facts;m.availability.verified=`${m.statuses.verified} · Backpack 0.4.0-preview.4 / SDK 1.1. ${m.notPublished}`;m.availability.source=facts;
 m.faq[0][1]=`${m.coreText} ${facts} ${m.applicationText}`;
 m.faq[1][1]=`Backpack 0.4.0-preview.4 · Windows 10 / 11 x64. ${m.notPublished} ${facts}`;
 result.content.faq=m.faq;result.content.dimNote=m.availability.verified;
 return result;
}
