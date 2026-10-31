import fs from 'node:fs';
import {localeData} from './locale-data.mjs';
const counts={nav:4,hero:11,labels:18,tools:19,cards:12,release:13,faq:16,demos:25,choices:23,steps:12};
for(const [id,raw] of Object.entries(localeData)){
 const p={};for(const [group,count] of Object.entries(counts)){p[group]=raw[group].split('|').map(v=>v.trim().normalize('NFC'));if(p[group].length!==count||p[group].some(v=>!v))throw Error(`${id}.${group}: ${p[group].length}, expected ${count}`);}
 const [badge,h0,h1,h2,intro,primary,secondary,o0,o1,overviewText,toolsTitle]=p.hero;
 const [menu,language,skip,close,pause,play,reduced,toolkit,previewLabel,signedLabel,unsignedLabel,publishedLabel,unpublishedLabel,development,concept,author,back,preview]=p.labels;
 const categories=p.tools.slice(0,5),[home,appearance,keys,mouse,focus,drawing,profiles,lens,dimming,zoom,size,radius,border,tray]=p.tools.slice(5);
 const [preparing,packageLabel,release,download,checksum,checksumNote,license,dt0,dt1,downloadText,helpTitle,footer,notice]=p.release;
 const faq=Array.from({length:8},(_,i)=>p.faq.slice(i*2,i*2+2));
 const [cursorTag,cursorTitle,cursorDescription,cursorCaption,windowLabel,windowCaption,profileHeading,rightShift,normalField,passwordField,safePhrase,returnPhrase,selected,boardTag,boardHeading,lensScale,keySequence,keyExamples,space,shift,layout,privacyStatic,privacyNote,pluginTitle,pluginText]=p.demos;
 const families=p.choices.slice(0,5),profileChoices=p.choices.slice(5,8),mouseStyles=p.choices.slice(8,11),board=p.choices.slice(11,14),boardRows=p.choices.slice(14,17),context=p.choices.slice(17,20),styles=p.choices.slice(20,23);
 const cards=Array.from({length:6},(_,i)=>[i===4?passwordField:categories[i===5?4:i],...p.cards.slice(i*2,i*2+2)]);
 const content={title:`Backpack — ${toolsTitle}`,description:intro,nav:p.nav,skip,menu,language,close,sceneNote:[h0,h2],toolkit,previewLabel,signedLabel,unsignedLabel,publishedLabel,unpublishedLabel,eyebrow:toolsTitle,badge,hero:[h0,h1,h2],intro,primary,secondary,platform:`Windows 10 / 11 · x64 · ${previewLabel}`,pause,play,reduced,categories,overviewTag:toolkit,overviewTitle:[o0,o1],overviewText,explore:primary,interface:previewLabel,interfaceNote:faq[2][1],home,appearance,keys,mouse,focus,drawing,profiles,lens,dimming,zoom,size,radius,border,tray,toolsTag:toolkit,toolsTitle,toolsText:privacyStatic,preview,development,concept,cards,styles,renderers:[families[2],families[3],families[1]],chooseStyle:appearance,chooseProfile:profiles,chooseFocus:focus,dimNote:`${dimming}: ${development}`,numberNote:concept,safe:passwordField,ordinary:normalField,privacyNote,pluginTitle,pluginText,downloadTag:packageLabel,downloadTitle:[dt0,dt1],downloadText,preparing,packageLabel,unsigned:`${unsignedLabel} · ${unpublishedLabel}`,release,download,checksum,checksumNote,license,helpTag:p.nav[3],helpTitle,faq,footer,author,back,notice};
 const demo={cursorTag,cursorTitle,cursorDescription,cursorCaption,windowTag:categories[2],windowTitle:cards[2][1],windowDescription:cards[2][2],windowLabel,windowCaption,profileHeading,rightShift,layoutCode:id==='nb'?'NO':id.toUpperCase(),families,familyPresets:families,profiles:profileChoices,profileNames:[`${families[2]} · ${profileChoices[0]}`,`${families[0]} · ${profileChoices[1]}`,`${families[4]} · ${profileChoices[2]}`],mouseStyles,mouseNames:mouseStyles,mouseStyle:categories[1],normalField,passwordField,safePhrase,returnPhrase,privacySteps:p.steps.slice(0,4),mouseSteps:p.steps.slice(4,7),selected,context,board,boardTag,boardHeading,boardRows,lensScale,drawSteps:p.steps.slice(7,9),interfaceSteps:p.steps.slice(9,12),keySequence,keyExamples,space,shift,layout,privacyStatic};
 fs.writeFileSync(`src/locales/${id}.json`,JSON.stringify({content,demo},null,2)+'\n');
}
console.log(`Generated ${Object.keys(localeData).length} complete local translations.`);

await import('./apply-model-copy.mjs');
