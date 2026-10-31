import settings from './integrations/settings.json';
export type Availability = 'planned' | 'source' | 'verified';
export const product = {
 name:'Backpack',packageName:'Backpack',version:'0.4.0-preview.4',channel:'Preview',
 platforms:['Windows 10','Windows 11'],architecture:'x64',published:false,signed:false,
 downloadUrl:null as string|null,repositoryUrl:null as string|null,siteUrl:null as string|null,sizeBytes:92634242 as number|null,
 releaseNotesUrl:'/release-notes.txt',
 sha256:'7010d5f6d5c4e9a0ea464227dd87549dba09677c91252ea288bc4b15a3ab3679',
 author:{name:'soobshoob',url:'https://github.com/soobshoob'},
 license:{...settings.licensing.releases['0.4.0-preview.4'],localUrl:'/licenses/PolyForm-Strict.txt'},
 release:{version:'0.4.0-preview.4',verified:true,public:false,verifiedOn:'2026-10-30',evidence:'Phase4G-Implementation-Report'},
 core:[{id:'keys',implemented:true,verified:true},{id:'mouse',implemented:true,verified:true}],
 plugins:[
  {id:'drawing',status:'planned',implementedAsPlugin:false,verifiedAsPlugin:false,legacyToolVerified:true,version:null,sdk:null},
  {id:'layout',status:'planned',implementedAsPlugin:false,verifiedAsPlugin:false,legacyToolVerified:true,version:null,sdk:null},
  {id:'lens',status:'verified',implementedAsPlugin:true,verifiedAsPlugin:true,legacyToolVerified:true,version:'1.0.0',sdk:'1.1'},
  {id:'focus',status:'verified',implementedAsPlugin:true,verifiedAsPlugin:true,legacyToolVerified:true,version:'1.0.0',sdk:'1.1'},
  {id:'cursorLens',status:'verified',implementedAsPlugin:true,verifiedAsPlugin:true,legacyToolVerified:true,version:'1.0.0',sdk:'1.1'},
  {id:'timer',status:'verified',implementedAsPlugin:true,verifiedAsPlugin:true,legacyToolVerified:false,version:'2.0.0',sdk:'1.1'},
 ].map(plugin=>({...plugin,status:plugin.status as Availability,author:'soobshoob',public:false,packageUrl:null as string|null,documentationUrl:null as string|null,exampleUrl:null as string|null})),
 infrastructure:{profiles:true,appearance:true,inputPrivacy:true,pluginManagement:true},
 sdk:{verifiedVersion:'1.1',sourceVersion:'1.1',documentationUrl:null as string|null,exampleUrl:null as string|null,packageUrl:null as string|null,licenseResolved:false},
 verifiedOn:'2026-10-30',
} as const;
