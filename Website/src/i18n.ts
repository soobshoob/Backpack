import en from './locales/en.json';
export type { Language } from './language';
export type Copy = typeof en.content;
export type DemoCopy = typeof en.demo;
export type ModelCopy = typeof en.model;
export type LocaleBundle = typeof en;
export const english = en;
const loaders = import.meta.glob<LocaleBundle>(['./locales/*.json','!./locales/en.json'], { import: 'default' });
const cache = new Map<string, Promise<LocaleBundle>>();
const resolved=new Map<string,LocaleBundle>([['en',en]]);
export function peekLocale(language:string){return resolved.get(language)??en;}
export function loadLocale(language: import('./language').Language): Promise<LocaleBundle> {
  if (language === 'en') return Promise.resolve(english);
  if (!cache.has(language)) {
    const request = loaders[`./locales/${language}.json`]().then(bundle=>{resolved.set(language,bundle);return bundle;});
    cache.set(language, request);
    request.catch(() => cache.delete(language));
  }
  return cache.get(language)!;
}
