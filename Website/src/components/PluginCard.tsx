import type {SceneProps} from './demos/KeysDemo';
import type {ModelCopy,Language} from '../i18n';
import type {CatalogEntry} from '../catalog';
import {categories} from '../catalog';
import {siteCopy} from '../siteCopy';
import {ArrowUpRight} from 'lucide-react';
import {DemoSurface,pluginIcons} from './DemoSurface';
export function PluginCard({entry,a,lang,...props}:SceneProps&{entry:CatalogEntry;a:ModelCopy;lang:Language}){
 const c=siteCopy(lang),Icon=pluginIcons[entry.index];
 return <article className="extension-card" data-role="plugin" data-slug={entry.slug}>
  <div className="extension-preview"><DemoSurface {...props} index={entry.index} a={a}/></div>
  <div className="extension-body"><div className="extension-title"><span className="extension-icon"><Icon/></span><div><h3><a href={`/plugins/${entry.slug}`}>{a.pluginNames[entry.index]}</a></h3><span>{a.byAuthor}: {entry.author}</span></div></div><p>{a.pluginBenefits[entry.index]}</p><div className="extension-tags">{entry.categories.map(cat=><span key={cat}>{c.categories[categories.indexOf(cat)]}</span>)}</div><div className="extension-card-bottom"><span className={`availability ${entry.status}`}>{entry.status==='planned'?c.planned:entry.status==='verified'?c.verified:c.source}</span><a href={`/plugins/${entry.slug}`} aria-label={`${c.details}: ${a.pluginNames[entry.index]}`}><ArrowUpRight size={22}/></a></div></div>
 </article>;
}
