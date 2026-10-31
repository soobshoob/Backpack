import { useDemo } from '../../hooks/useDemo';
import { samplePrivacy } from '../../animation/timeline';
import { attr, text, move } from '../../animation/dom';
import { Key, KeyGroup } from '../Controls';
import { DemoPointer } from '../Pointer';
import type { SceneProps } from './KeysDemo';
export function PrivacyDemo({ d, paused, reduced }: SceneProps) {
  const demo = useDemo({ paused, reduced, duration: 13200, staticTime: 7000, render(root, time) {
    const s = samplePrivacy(time, d.safePhrase, d.returnPhrase);
    text(root, '.normal-value', s.normalText); text(root, '.password-value', '•'.repeat(s.passwordDots));
    move(root, '.demo-pointer', s.x, s.y); attr(root, '.cursor-click', 'opacity', s.click ? .9 : 0);
    attr(root, '.privacy-shield', 'opacity', s.protectedInput ? 1 : 0);
    attr(root, '.password-background', 'stroke', s.protectedInput ? '#50d49b' : '#41574a'); attr(root, '.normal-background', 'stroke', s.caret === 'normal' ? '#80bca0' : '#41574a');
    attr(root, '.normal-caret', 'opacity', s.caret === 'normal' ? 1 : 0);
    const normalEnd=36+(root.querySelector<SVGTextElement>('.normal-value')?.getComputedTextLength()??0);
    attr(root, '.normal-caret', 'x1', normalEnd); attr(root, '.normal-caret', 'x2', normalEnd);
    attr(root, '.password-caret', 'opacity', s.caret === 'password' ? 1 : 0);
    const passwordEnd=36+(root.querySelector<SVGTextElement>('.password-value')?.getComputedTextLength()??0);
    attr(root, '.password-caret', 'x1', passwordEnd); attr(root, '.password-caret', 'x2', passwordEnd);
    root.querySelectorAll<HTMLElement>('.privacy-slot').forEach((slot, index) => {
      const key = s.keys[index] ?? ''; slot.style.visibility = key ? 'visible' : 'hidden'; slot.dataset.pressed = String(index === s.keys.length - 1 && s.pressed); text(slot, '.key-label', key === ' ' ? '␣' : key);
    });
    root.querySelector<HTMLElement>('.privacy-stage')!.style.opacity = String(s.opacity);
    root.dataset.phase = s.phase; root.dataset.keyCount = String(s.keys.length); root.dataset.passwordCount = String(s.passwordDots);
    text(root, '.scene-caption', reduced ? d.privacyStatic : d.privacySteps[s.phase === 'protected' ? 2 : s.phase === 'return' ? 3 : s.phase === 'approach' ? 0 : 1]);
  } });
  return <div {...demo} className="demo-v2 privacy-scene" data-scene="privacy">
    <div className="privacy-stage" aria-hidden="true"><svg className="scene-svg" viewBox="0 0 360 160">
      <text x="20" y="17" className="field-label">{d.normalField}</text><rect className="normal-background" x="20" y="26" width="320" height="43" rx="8" fill="#24322a" stroke="#41574a"/>
      <text className="normal-value" x="34" y="53" fontSize="13.5" fill="#f2f6f3" style={{fontFamily:'var(--font-body)'}}/>
      <g className="caret-blink"><line className="normal-caret" x1="34" x2="34" y1="36" y2="57" stroke="#c3e9d2" strokeWidth="1.5" opacity="0"/></g>
      <text x="20" y="89" className="field-label">{d.passwordField}</text><rect className="password-background" x="20" y="99" width="320" height="43" rx="8" fill="#19271f" stroke="#41574a"/>
      <text className="password-value" x="34" y="127" fontSize="21" letterSpacing="4" fill="#d3f2df"/>
      <g className="caret-blink"><line className="password-caret" x1="34" x2="34" y1="109" y2="132" stroke="#c3e9d2" strokeWidth="1.5" opacity="0"/></g>
      <g className="privacy-shield" opacity="0" transform="translate(310 111)"><path d="M0 0 9-3 18 0V8Q18 15 9 19Q0 15 0 8Z" fill="#133d28" stroke="#59d79d"/><path d="m4 7 4 4 6-7" fill="none" stroke="#9df0c5" strokeWidth="1.5"/></g>
      <DemoPointer/>
    </svg><div className="privacy-keystrokes"><KeyGroup preset="cream">{[0, 1, 2].map(i => <Key preset="cream" className="privacy-slot" key={i}>{' '}</Key>)}</KeyGroup></div></div>
    <p className="scene-caption">{d.privacySteps[0]}</p>
  </div>;
}

