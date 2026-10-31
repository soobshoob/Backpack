import fs from 'node:fs';
import {modelCopy} from './model-copy.mjs';
import {extraModelCopy} from './model-extra-copy.mjs';
import {programLabels,programKeys} from './program-copy.mjs';
import {carouselLabels} from './carousel-copy.mjs';
import {updateReleaseCopy} from './update-release-copy.mjs';
const models={...extraModelCopy,...modelCopy};
const madeIn={en:'Made in Ukraine.',uk:'Виготовлено в Україні.',de:'Hergestellt in der Ukraine.',fr:'Fabriqué en Ukraine.',es:'Hecho en Ucrania.',it:'Realizzato in Ucraina.',pt:'Feito na Ucrânia.',nl:'Gemaakt in Oekraïne.',pl:'Wyprodukowano w Ukrainie.',cs:'Vyrobeno na Ukrajině.',hu:'Ukrajnában készült.',ro:'Fabricat în Ucraina.',sv:'Tillverkat i Ukraina.',da:'Fremstillet i Ukraine.',nb:'Laget i Ukraina.',fi:'Valmistettu Ukrainassa.',lt:'Pagaminta Ukrainoje.',el:'Κατασκευάστηκε στην Ουκρανία.'};
for(const file of fs.readdirSync('src/locales')){
 const id=file.slice(0,-5),path=`src/locales/${file}`,locale=JSON.parse(fs.readFileSync(path,'utf8')),model=models[id];
 if(!model)throw Error(`Missing architecture translation: ${id}`);
 locale.model=model;locale.content.carousel={previous:carouselLabels[id][0],next:carouselLabels[id][1]};
 const labels=programLabels[id]?.split('|');if(labels?.length!==programKeys.length)throw Error(`Missing program labels: ${id}`);locale.content.program=Object.fromEntries(programKeys.map((key,i)=>[key,labels[i]]));
 if(!madeIn[id])throw Error(`Missing country attribution: ${id}`);
 locale.content.madeIn=madeIn[id];
 Object.assign(locale.content,{title:model.title,description:`${model.intro} ${model.extension}`,intro:model.intro,overviewText:model.coreText,overviewTag:model.coreTag,toolsTitle:model.coreTitle,interfaceNote:model.workflowNote,pluginText:model.pluginsText,pluginTitle:model.pluginsTitle,notice:model.workflowTitle,faq:model.faq,privacyNote:model.privacyText,downloadText:model.notPublished,interface:model.workflowTitle,numberNote:model.workflowTitle,dimNote:model.availability.source});
 locale.content.categories[2]=model.pluginsTab;locale.content.categories[3]=model.developerTag;
 locale.content.cards[2]=[model.pluginNames[2],model.pluginNames[2],model.pluginBenefits[2]];
 locale.content.cards[3]=[model.pluginNames[0],model.pluginNames[0],model.pluginBenefits[0]];
 locale.content.cards[4]=[model.privacyTitle,model.privacyTitle,model.privacyText];
 locale.content.cards[5]=[locale.content.categories[4],model.applicationTitle,model.applicationText];
 Object.assign(locale.demo,{cursorTag:model.pluginNames[4],cursorTitle:model.pluginNames[4],cursorDescription:model.pluginBenefits[4],windowTag:model.pluginNames[2],windowTitle:model.pluginNames[2],windowDescription:model.pluginBenefits[2]});
 fs.writeFileSync(path,JSON.stringify(updateReleaseCopy(locale),null,2)+'\n');
}
console.log(`Applied product model to ${Object.keys(models).length} local translations.`);
