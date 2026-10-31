import {DownloadBlock,SupportBlock} from './components/Distribution';
import {Backpack,Keyboard,Mouse,ArrowUpRight,ArrowDown,ArrowRight,Monitor,ShieldCheck,ChevronDown} from 'lucide-react';
import {product} from './product';
import {HeroScene} from './components/HeroScene';
import {OnboardingDemo} from './components/OnboardingDemo';
import {onboardingCopy} from './onboardingCopy';
import {KeysDemo,type SceneProps} from './components/demos/KeysDemo';
import {MouseDemo} from './components/demos/MouseDemo';
import {DrawingDemo} from './components/demos/DrawingDemo';
import {PrivacyDemo} from './components/demos/PrivacyDemo';
import {ProfilesDemo} from './components/demos/ProfilesDemo';
import {ShowcaseCard,ShowcaseRail} from './components/Showcase';
import {PluginCard} from './components/PluginCard';
import {FocusCompare} from './components/FocusCompare';
import {catalogEntries} from './catalog';
import {siteCopy} from './siteCopy';
import type {ModelCopy,Language} from './i18n';
export function Home({a,lang,...props}:SceneProps&{a:ModelCopy;lang:Language}){const {t,d,reduced}=props,c=siteCopy(lang);return <>

   <section className="hero wrap"><div className="hero-copy"><div className="hero-badge"><i/>{t.badge}</div><h1>{t.hero[0]}<br/>{t.hero[1]}<br/><span>{t.hero[2]}</span></h1><p>{a.intro}</p><p className="hero-extension">{a.extension}</p><div className="hero-actions"><a className="button primary" href="#download">{product.published&&product.downloadUrl?t.download:t.secondary}<ArrowDown size={18}/></a><a className="button secondary" href="#tools">{t.primary}<ArrowRight size={17}/></a></div><div className="platform"><Monitor size={14}/>{product.platforms.join(' / ')} · {product.architecture} · {t.previewLabel}</div></div><HeroScene {...props}/></section>
   <section className="overview wrap reveal" id="features"><div className="overview-copy"><div className="eyebrow">{a.coreTag}</div><h2>{t.overviewTitle[0]}<br/><span>{t.overviewTitle[1]}</span></h2><p>{onboardingCopy(lang).description}</p></div><OnboardingDemo {...props} a={a} lang={lang}/></section>
   <section className="tools core-section wrap" id="tools"><div className="section-heading reveal"><div><div className="eyebrow">{a.coreTag}</div><h2>{a.coreTitle}</h2></div><p>{t.toolsText}</p></div><ShowcaseRail label={a.coreTitle} labels={t.carousel} reduced={reduced}>{[KeysDemo,MouseDemo].map((Scene,i)=>{const Icon=[Keyboard,Mouse][i];return <ShowcaseCard key={i} id={`feature-${i}`} kind="core" title={t.cards[i][0]} description={t.cards[i][2]} tag={a.builtin} status={t.preview} icon={<Icon size={20}/>} metadata={<span>{product.name} · {product.version}</span>} detailsLabel={d.context[2]} optionsLabel={i===0?t.chooseStyle:d.mouseStyle} info={<><h4>{t.cards[i][0]}</h4><p>{a.coreText}</p><p>{t.preview} · {product.version}</p></>}><Scene {...props}/></ShowcaseCard>;})}</ShowcaseRail></section>
   <section className="home-plugin-scenes wrap"><div className="extension-scene" id="feature-2"><div><span className="eyebrow">{c.plugin}</span><h3>{a.pluginNames[2]} / {a.pluginNames[3]}</h3><p>{c.focusCompare}</p></div><FocusCompare {...props} a={a}/></div><div className="extension-scene" id="feature-3"><div><span className="eyebrow">{c.plugin} · {c.planned}</span><h3>{a.pluginNames[0]}</h3><p>{a.pluginBenefits[0]}</p></div><DrawingDemo {...props}/></div></section>
   <section className="catalog wrap" id="plugins"><div className="section-heading reveal"><div><div className="eyebrow">{a.pluginsTag}</div><h2>{a.pluginsTitle}</h2></div><a className="text-link" href="/plugins">{c.explore}<ArrowUpRight size={16}/></a></div><p className="catalog-home-note">{c.publicEmpty} {c.publicNote}</p><div className="featured-plugins">{[0,2,3,5].map(i=><div key={i} id={`plugin-${product.plugins[i].id}`}><PluginCard {...props} entry={catalogEntries[i]} a={a} lang={lang}/></div>)}</div><div className="legacy-plugin-links"><a id="plugin-layout" href="/plugins/layout-indicator">{a.pluginNames[1]}<ArrowUpRight size={14}/></a><a id="plugin-cursorLens" href="/plugins/cursor-magnifier">{a.pluginNames[4]}<ArrowUpRight size={14}/></a></div></section>
   <section className="infrastructure wrap reveal" id="profiles"><div className="profiles-band" id="feature-5"><div><div className="eyebrow">{a.applicationTag}</div><h2>{a.applicationTitle}</h2><p>{a.applicationText}</p><p className="demo-example-note">{c.demoExamples}</p></div><ProfilesDemo {...props} d={{...d,profiles:c.profileNames}}/></div><div className="privacy-band" id="feature-4"><div><h3>{a.privacyTitle}</h3><p>{a.privacyText}</p></div><PrivacyDemo {...props}/></div></section>
   <section className="developers wrap reveal" id="developers"><div className="eyebrow">{a.developerTag}</div><h2>{a.developerTitle}</h2><div className="developer-grid">{c.shortDev.map((title,i)=><div className="developer-step" key={i}><span>0{i+1}</span><h3>{title}</h3><p>{c.shortDevText[i]}</p></div>)}</div><div className="developer-notice"><p>{c.legal}</p><a className="text-link" href="/developers">{c.devLink}<ArrowUpRight size={17}/></a></div></section>
   <DownloadBlock lang={lang}/><SupportBlock lang={lang}/>
   <section className="help wrap reveal" id="help"><div className="eyebrow">{t.helpTag}</div><h2>{t.helpTitle}</h2><div className="faq">{a.faq.map(([q,answer])=><details key={q}><summary>{q}<span>+</span></summary><p>{answer}</p></details>)}</div></section>

</>;}
