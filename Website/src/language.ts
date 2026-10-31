export const languages = [
  { id: 'en', compact: 'EN', name: 'English' },
  { id: 'uk', compact: 'UK', name: 'Українська' },
  { id: 'de', compact: 'DE', name: 'Deutsch' },
  { id: 'fr', compact: 'FR', name: 'Français' },
  { id: 'es', compact: 'ES', name: 'Español' },
  { id: 'it', compact: 'IT', name: 'Italiano' },
  { id: 'pt', compact: 'PT', name: 'Português' },
  { id: 'nl', compact: 'NL', name: 'Nederlands' },
  { id: 'pl', compact: 'PL', name: 'Polski' },
  { id: 'cs', compact: 'CS', name: 'Čeština' },
  { id: 'hu', compact: 'HU', name: 'Magyar' },
  { id: 'ro', compact: 'RO', name: 'Română' },
  { id: 'sv', compact: 'SV', name: 'Svenska' },
  { id: 'da', compact: 'DA', name: 'Dansk' },
  { id: 'nb', compact: 'NO', name: 'Norsk bokmål' },
  { id: 'fi', compact: 'FI', name: 'Suomi' },
  { id: 'lt', compact: 'LT', name: 'Lietuvių' },
  { id: 'el', compact: 'EL', name: 'Ελληνικά' },
] as const;
export type Language = typeof languages[number]['id'];
export function isLanguage(value: string | null): value is Language {
  return languages.some(language => language.id === value);
}
export function resolveLanguage(stored: string | null, browserLanguages: string | readonly string[]): Language {
  if (isLanguage(stored)) return stored;
  for (const preference of typeof browserLanguages === 'string' ? [browserLanguages] : browserLanguages) {
    const base = preference.toLowerCase().split(/[-_]/)[0];
    const language = base === 'no' ? 'nb' : base;
    if (isLanguage(language)) return language;
  }
  return 'en';
}
export function optionIndex(current: number, key: string, length: number) {
  if (key === 'ArrowDown') return (current + 1) % length;
  if (key === 'ArrowUp') return (current - 1 + length) % length;
  if (key === 'Home') return 0;
  if (key === 'End') return length - 1;
  return current;
}
