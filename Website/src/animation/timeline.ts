/** Advances only while a scene is active. No catch-up after suspension. */
export class ActiveClock {
  elapsed = 0;
  private previous: number | null = null;
  advance(now: number) {
    if (this.previous !== null) this.elapsed += Math.max(0, Math.min(now - this.previous, 100));
    this.previous = now;
    return this.elapsed;
  }
  suspend() { this.previous = null; }
}
export const clamp = (v: number, min = 0, max = 1) => Math.max(min, Math.min(max, v));
export const progress = (time: number, start: number, end: number) => clamp((time - start) / (end - start));
export const smooth = (p: number) => { const t = clamp(p); return t * t * (3 - 2 * t); };
export const mix = (a: number, b: number, t: number) => a + (b - a) * smooth(t);
export function point(time: number, stops: readonly (readonly [number, number, number])[]) {
  const next = stops.findIndex(s => s[0] > time);
  if (next === 0) return { x: stops[0][1], y: stops[0][2] };
  if (next === -1) return { x: stops.at(-1)![1], y: stops.at(-1)![2] };
  const a = stops[next - 1], b = stops[next], p = progress(time, a[0], b[0]);
  return { x: mix(a[1], b[1], p), y: mix(a[2], b[2], p) };
}
export function sampleKeys(time: number) {
  const t = time % 7200;
  return { pressed: [t >= 1100 && t < 3000, t >= 1320 && t < 3200, t >= 1540 && t < 2800],
    opacity: t < 700 ? smooth(t / 700) : t < 3800 ? 1 : 1 - smooth(progress(t, 3800, 4400)),
    layoutVisible: t >= 4700 && t < 6600 };
}
export function sampleMouse(time: number) {
  const t = time % 10000;
  const cursor = point(t, [[0, 55, 130], [1300, 128, 65], [2700, 128, 65], [4200, 228, 78], [5900, 228, 78], [7100, 258, 135], [8800, 258, 135], [10000, 55, 130]]);
  return { ...cursor, left: t >= 1550 && t < 1930, right: t >= 4350 && t < 4750,
    selected: t >= 1550 && t < 9200, menu: t >= 4520 && t < 6100,
    wheel: t >= 7200 && t < 8150 ? -1 : t>=8400&&t<9300?1:0, scroll: mix(0, -30, progress(t, 7200, 8150))+mix(0,30,progress(t,8400,9300)),
    stage: t < 3200 ? 0 : t < 6500 ? 1 : 2 };
}
/** No password string exists, only a count of synthetic masking dots. */
export function samplePrivacy(time: number, phrase: string, returnPhrase: string) {
  const t = time % 13200, chars = Array.from(phrase), next = Array.from(returnPhrase);
  const count = Math.floor(progress(t, 1800, 4400) * chars.length);
  const returning = t >= 10400, nextCount = Math.floor(progress(t, 10400, 11900) * next.length);
  const protectedInput = t >= 5100 && t < 9600;
  const typing = t >= 1800 && t < 4550 || t >= 10400 && t < 12200;
  const source = returning ? next : chars, n = returning ? nextCount : count;
  const lastEvent = (returning ? 10400 : 1800) + n / source.length * (returning ? 1500 : 2600);
  const keys = !protectedInput && typing && n > 0 ? source.slice(Math.max(0, n - 3), n) : [];
  return {
    ...point(t, [[0, 308, 175], [1100, 96, 50], [4200, 96, 50], [5400, 124, 117], [8800, 124, 117], [10100, 140, 50], [12200, 140, 50], [13200, 308, 175]]),
    phase: protectedInput ? 'protected' : returning ? 'return' : t < 1500 ? 'approach' : 'ordinary',
    protectedInput, normalText: returning ? next.slice(0, nextCount).join('') : chars.slice(0, count).join(''),
    passwordDots: t >= 5800 ? Math.min(8, Math.floor(progress(t, 5800, 8000) * 8)) : 0,
    keys, pressed: n > 0 && t - lastEvent < 140,
    caret: t >= 1300 && (t < 5100 || t >= 10100) ? 'normal' : protectedInput ? 'password' : null,
    click: t >= 1120 && t < 1380 || t >= 5460 && t < 5680 || t >= 10100 && t < 10300,
    opacity: smooth(progress(t, 0, 700)) * (1 - smooth(progress(t, 12400, 13200))),
  };
}
export function sampleFocus(time: number) {
  const t = time % 12000;
  return { ...point(t, [[0, 69, 77], [1800, 69, 77], [5100, 79, 113], [8100, 79, 113], [10600, 69, 77], [12000, 69, 77]]), dimmed: t >= 6900 && t < 10400 };
}
export function sampleDrawing(time: number) {
  const t = time % 10400, line = progress(t, 1800, 3000), head = progress(t, 3300, 3900), box = progress(t, 6100, 8100);
  let x = 326, y = 111;
  if (t < 1700) ({ x, y } = point(t, [[0, 285, 153], [1000, 326, 73], [1350, 326, 73], [1700, 47, 147]]));
  else if (t < 3000) { x = 47 + 215 * line; y = 147 - 155 * line + 67 * line * line; }
  else if (t < 5000) ({ x, y } = point(t, [[3000, 262, 59], [3300, 244, 48], [3600, 262, 59], [3900, 242, 75], [5000, 242, 75]]));
  else if (t < 6100) ({ x, y } = point(t, [[5000, 242, 75], [5500, 326, 111], [6100, 125, 116]]));
  else if (t < 8100) {
    const p = box * 256;
    if (p < 78) { x = 125 + p; y = 116; }
    else if (p < 128) { x = 203; y = 116 + p - 78; }
    else if (p < 206) { x = 203 - (p - 128); y = 166; }
    else { x = 125; y = 166 - (p - 206); }
  } else ({ x, y } = point(t, [[8100, 125, 116], [8900, 285, 153], [10400, 285, 153]]));
  return { x, y, line, head, box, tool: t < 5250 ? 'arrow' : 'rectangle', opacity: 1 - smooth(progress(t, 9100, 10000)), selecting: t < 1550 || t >= 5000 && t < 5650 };
}
export function sampleInterface(time: number) {
  const t = time % 14600;
  const zoom = t < 11800 ? mix(1, 2.5, progress(t, 2200, 4100)) : mix(2.5, 1, progress(t, 11800, 13600));
  const lens = point(t, [[0, 74, 91], [4900, 74, 91], [7400, 75, 139], [10800, 75, 139], [13800, 74, 91], [14600, 74, 91]]);
  let cursor;
  if (t < 4400) cursor = point(t, [[0, 216, 245], [1500, 286, 164], [2200, 286, 164], [4100, 392, 164], [4400, 392, 164]]);
  else if (t < 8000) cursor = t < 5000 ? point(t, [[4400, 392, 164], [5000, 458 + lens.x, 91 + lens.y * .92]]) : {x:458 + lens.x,y:91 + lens.y * .92};
  else if (t < 10400) cursor = point(t, [[8000, 533, 218.88], [9100, 393, 94], [10400, 393, 94]]);
  else if (t < 11900) cursor = point(t, [[10400, 393, 94], [11200, 533, 218.88], [11900, 533, 218.88]]);
  else cursor = point(t, [[11900, 533, 218.88], [13000, 392, 164], [13600, 286, 164], [14600, 216, 245]]);
  return { ...cursor, zoom, lensX: lens.x, lensY: lens.y, knob: 286 + (zoom - 1) / 1.5 * 106,
    dimmed: t >= 9500 && t < 11800, stage: t < 4400 ? 0 : t < 8800 ? 1 : 2,
    click: t >= 2120 && t < 2320 || t >= 9350 && t < 9700 };
}
