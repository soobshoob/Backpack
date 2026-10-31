import {progress, smooth} from './timeline';

export const onboardingDuration = 32000;
export const onboardingStages = ['home','style','catalog','detail','workspace','detail','home'] as const;
/** One coherent, preconfigured demo profile. Focus remains enabled across loops. */
export function sampleOnboarding(elapsed:number) {
 const time=((elapsed%onboardingDuration)+onboardingDuration)%onboardingDuration;
 const stage=time<2500?0:time<9200?1:time<12800?2:time<16900?3:time<26500?4:time<28700?5:6;
 const page=onboardingStages[stage];
 const clicks=[2500,6500,9200,12800,16900,22400,26500,28700];
 const click=clicks.find(at=>time>=at&&time<at+420);
 const legs:readonly (readonly [number,string])[]=[[0,'home-rest'],[1000,'home-rest'],[2300,'choose-style'],[2900,'choose-style'],[4200,'style-scroll'],[5100,'style-scroll'],[6250,'cream'],[6900,'cream'],[8400,'plugins'],[9600,'plugins'],[11100,'focus-info'],[13200,'focus-info'],[14600,'command'],[15500,'command'],[16600,'minimize'],[17300,'minimize'],[18300,'area-start'],[19000,'area-start'],[20700,'area-end'],[21600,'area-end'],[22200,'check'],[23800,'check'],[26000,'taskbar'],[27000,'taskbar'],[28400,'brand'],[29200,'brand'],[31200,'home-rest'],[32000,'home-rest']];
 const next=legs.findIndex(s=>s[0]>time),a=legs[Math.max(0,next-1)],b=legs[next<0?legs.length-1:next];
 return {time,stage,page,from:a[1],to:b[1],travel:smooth(progress(time,a[0],b[0]||1)),
  click:click===undefined?-1:progress(time,click,click+420),
  styleScroll:smooth(progress(time,3800,5000)),catalogScroll:smooth(progress(time,9800,11300)),detailScroll:stage===5?1:smooth(progress(time,13700,15300)),
  cream:time>=6500,pressed:time>=7500&&time<8400,
  selecting:time>=19000&&time<20700,selection:smooth(progress(time,19000,20700)),
  dim:time>=20700&&time<25000,checked:time>=22400,
  shortcut:time>=17500&&time<18800,escape:time>=25000&&time<26000,
  opacity:Math.min(1,time/400,(32000-time)/400),
  // Scene crossfades conceal layout changes while the pointer stays on its target.
  transition:Math.min(1,...[2500,9200,12800,16900,26500,28700].map(at=>Math.abs(time-at)/180)),
 };
}
