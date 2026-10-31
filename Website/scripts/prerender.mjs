import { createServer } from 'vite';
import { renderToString } from 'react-dom/server';
import { createElement } from 'react';
import { readFile, writeFile,mkdir } from 'node:fs/promises';
const output=process.env.BACKPACK_BUILD_DIR??'dist';
const server=await createServer({mode:process.env.BACKPACK_SSR_MODE??'development',server:{middlewareMode:true},appType:'custom'});
try {
 const {App}=await server.ssrLoadModule('/src/App.tsx');
 const {product}=await server.ssrLoadModule('/src/product.ts');
 let html=await readFile(output+'/index.html','utf8');
 if(product.siteUrl){
  const url=new URL(product.siteUrl);if(url.protocol!=='https:')throw new Error('siteUrl must be an HTTPS URL');
  const canonical=url.href.endsWith('/')?url.href:url.href+'/';
  html=html.replace('</head>',`<link rel="canonical" href="${canonical}"/><meta property="og:url" content="${canonical}"/><meta property="og:image" content="${new URL('social-preview.png',canonical).href}"/></head>`);
  await writeFile(output+'/sitemap.xml',`<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><url><loc>${canonical}</loc></url></urlset>`);
 }
 const {catalogEntries}=await server.ssrLoadModule('/src/catalog.ts');
 const {english}=await server.ssrLoadModule('/src/i18n.ts');
 const {registryKey}=await server.ssrLoadModule('/src/integrations/registry.ts');
 const routes=['/','/download','/plugins','/plugins/publish','/developers',...catalogEntries.map(p=>`/plugins/${p.slug}`)];
 for(const route of [...routes,'/not-found','/plugins/__registry_plugin__']){
  const target=route==='/not-found'?output+'/404.html':route==='/plugins/__registry_plugin__'?output+'/plugin-route.html':`${output}${route==='/'?'':route}/index.html`;
  await mkdir(target.slice(0,target.lastIndexOf('/')),{recursive:true});
  const entry=catalogEntries.find(p=>route===`/plugins/${p.slug}`);
  const title=route==='/plugins'?'Extend Backpack with plugins.':route==='/plugins/publish'?'Prepare your extension.':route==='/developers'?'Backpack developer guide':route==='/not-found'?'Page not found':entry?english.model.pluginNames[entry.index]:null;
  let page=html.replace('<div id="root"></div>',`<div id="root">${renderToString(createElement(App,{initialPath:route}))}</div>`);
  if(title){page=page.replace(/<title>.*?<\/title>/,`<title>${title} — Backpack</title>`).replace(/<meta property="og:title" content="[^"]*"\/>/,`<meta property="og:title" content="${title} — Backpack"/>`);}
  await writeFile(target,page);
 }
 await writeFile(output+'/_redirects',(registryKey?'/plugins/* /plugin-route.html 200\n':'')+'/* /404.html 404\n');
 await writeFile(output+'/route-config.json',JSON.stringify({remotePluginRoutes:!!registryKey}));
} finally {await server.close();}
