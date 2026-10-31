export function sampleTimer(time:number){
 const t=((time%18000)+18000)%18000;
 const remaining=t<1000?12:t<4000?12-(t-1000)/1000:t<6500?9:Math.max(0,9-(t-6500)/1000);
 return {remaining,running:t>=1000&&t<4000||t>=6500&&t<15500,paused:t>=4000&&t<6500,finished:t>=15500,progress:1-remaining/12};
}
export function sampleLayout(time:number){
 const t=((time%10800)+10800)%10800,index=Math.floor(t/3600),phase=t%3600;
 return {index,transition:phase<400,typing:Math.min(1,Math.max(0,(phase-550)/1500))};
}
