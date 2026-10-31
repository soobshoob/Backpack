import { useState } from 'react';
import { useDemo } from '../../hooks/useDemo';
import { profilePresets } from '../../data/toolPresets';
import { Key, KeyGroup, Switcher } from '../Controls';
import type { SceneProps } from './KeysDemo';
export function ProfilesDemo({ t, d, paused, reduced }: SceneProps) {
  const [manual, setManual] = useState<number | null>(null), [automatic, setAutomatic] = useState(0);
  const selected = manual ?? automatic, preset = profilePresets[selected];
  const demo = useDemo({ paused, reduced, duration: 12000, staticTime: 1500, render(_root, time) { const next = Math.floor(time / 4000); if (manual === null && next !== automatic) setAutomatic(next); } });
  return <div {...demo} className="demo-v2 profiles-scene" data-scene="profiles" data-selection={selected}>
    <div className="profile-key-preview" aria-hidden="true"><KeyGroup preset={preset}><Key preset={preset} kind="modifier">Ctrl</Key><Key preset={preset} kind="enter">Enter ↵</Key></KeyGroup></div>
    <p className="scene-caption">{d.profileNames[selected]}</p><Switcher labels={d.profiles} value={selected} onChange={setManual} label={t.chooseProfile}/><p className="preset-name">{t.cards[5][0]}</p>
  </div>;
}


