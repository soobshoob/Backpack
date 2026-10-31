import { useEffect, useRef, useState } from 'react';
import { ActiveClock } from '../animation/timeline';

export interface DemoOptions {
  paused: boolean;
  reduced?: boolean;
  duration: number;
  staticTime: number;
  render: (root: HTMLDivElement, time: number) => void;
}

/** Animate only this scene's DOM. React renders on input/lifecycle changes. */
export function useDemo({ paused, reduced = false, duration, staticTime, render }: DemoOptions) {
  const ref = useRef<HTMLDivElement>(null);
  const latest = useRef(render);
  latest.current = render;
  const clock = useRef(new ActiveClock());
  const [visible, setVisible] = useState(false);
  const [tabVisible, setTabVisible] = useState(true);
  useEffect(() => {
    if(!('IntersectionObserver' in window)){setVisible(true);return;}
    const observer = new IntersectionObserver(([entry]) => setVisible(entry.isIntersecting), { threshold: .1 });
    if (ref.current) observer.observe(ref.current);
    const visibility = () => { clock.current.suspend(); setTabVisible(!document.hidden); };
    visibility(); document.addEventListener('visibilitychange', visibility);
    return () => { observer.disconnect(); document.removeEventListener('visibilitychange', visibility); };
  }, []);
  const running = visible && tabVisible && !paused && !reduced;
  // Reapply the same time to translated labels and a newly chosen manual mode.
  useEffect(() => {
    if (ref.current) latest.current(ref.current, reduced ? staticTime : clock.current.elapsed % duration);
  });
  useEffect(() => {
    clock.current.suspend();
    const root = ref.current;
    if (!root) return;
    if (!running) { root.getAnimations({subtree:true}).forEach(animation=>animation.pause()); return; }
    root.getAnimations({subtree:true}).forEach(animation=>{if(animation.playState==='paused')animation.play();});
    let request = 0;
    const tick = (now: number) => {
      const elapsed = clock.current.advance(now), time = elapsed % duration;
      root.dataset.time = String(Math.round(time));
      root.dataset.elapsed = String(Math.round(elapsed));
      latest.current(root, time);
      request = requestAnimationFrame(tick);
    };
    request = requestAnimationFrame(tick);
    return () => { cancelAnimationFrame(request); clock.current.suspend(); };
  }, [running, duration]);
  return { ref, 'data-running': running, 'data-reduced': reduced };
}

