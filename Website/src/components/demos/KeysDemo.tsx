import { useState } from 'react';
import type { Copy } from '../../i18n';
import type { DemoCopy } from '../../demoCopy';
import { useDemo } from '../../hooks/useDemo';
import { sampleKeys } from '../../animation/timeline';
import { familyPresets } from '../../data/toolPresets';
import { Key, KeyGroup, Switcher } from '../Controls';
export interface SceneProps { t: Copy; d: DemoCopy; paused: boolean; reduced: boolean; portrait?: boolean }

export function KeysDemo({ t, d, paused, reduced }: SceneProps) {
  const [family, setFamily] = useState(2), preset = familyPresets[family];
  const demo = useDemo({ paused, reduced, duration: 7200, staticTime: 2000, render(root, time) {
    const s = sampleKeys(time);
    root.querySelectorAll<HTMLElement>('.keys-main .tool-key').forEach((el, i) => { el.dataset.pressed = String(s.pressed[i]); });
    root.querySelector<HTMLElement>('.keys-main')!.style.opacity = String(s.opacity);
    root.querySelector<HTMLElement>('.key-samples')!.style.opacity = s.layoutVisible ? '1' : '0';
  } });
  return <div {...demo} className="demo-v2 keys-demo" data-scene="keys">
    <div className="key-stage" aria-hidden="true">
      <KeyGroup preset={preset} className="keys-main">{['Ctrl', 'Shift', 'S'].map((label, i) => <Key preset={preset} kind={i < 2 ? 'modifier' : 'normal'} key={i}>{label}</Key>)}</KeyGroup>
      <KeyGroup preset={preset} className="key-samples"><Key preset={preset} kind="enter">Enter ↵</Key><Key preset={preset} kind="space">{d.space}</Key><Key preset={preset} kind="long">{d.rightShift}</Key></KeyGroup>
    </div>
    <p className="scene-caption">{d.keySequence}</p>
    <Switcher labels={d.families} value={family} onChange={setFamily} label={t.chooseStyle}/>
    <p className="preset-name">{d.familyPresets[family]}</p>
  </div>;
}
