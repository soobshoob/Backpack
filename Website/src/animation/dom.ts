export function attr(root: Element, selector: string, name: string, value: string | number) {
  const element = root.querySelector(selector), next = String(value);
  if (element && element.getAttribute(name) !== next) element.setAttribute(name, next);
}
export function text(root: Element, selector: string, value: string) {
  const element = root.querySelector(selector);
  if (element && element.textContent !== value) element.textContent = value;
}
export function move(root: Element, selector: string, x: number, y: number) {
  attr(root, selector, 'transform', `translate(${x.toFixed(2)} ${y.toFixed(2)})`);
}
