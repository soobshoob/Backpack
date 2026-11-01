"""
Генерирует lab/src/Backpack.Visual/Icons/LucideIcons.g.cs из icon-nodes.json пакета lucide-static.

Использование (из корня репозитория):
    python tools/gen-lucide.py research/deps/lucide-static-1.59.0/package

Пакет скачивается вручную (см. docs/release/THIRD-PARTY-NOTICES.md, раздел Lucide): версия фиксирована, в репозиторий
попадает только сгенерированный файл с геометрией использованных иконок. Элементы SVG переводятся в язык путей WPF.
"""
import json, os, sys

ICONS = [
    # навигация
    'house', 'settings-2', 'palette', 'keyboard', 'mouse', 'languages', 'move', 'focus', 'pencil', 'command',
    'layers', 'circle-question-mark', 'stethoscope', 'info', 'puzzle', 'star', 'app-window', 'monitor',
    # действия и состояния
    'lock', 'lock-open', 'shield-check', 'mouse-pointer-2', 'zoom-in', 'scan-search', 'hash', 'minus', 'x', 'pause', 'play',
    'chevron-down', 'chevron-right', 'external-link', 'package', 'plus', 'trash', 'download', 'upload', 'square', 'circle',
    'arrow-up-right', 'type', 'undo-2', 'redo-2', 'camera', 'eraser', 'timer', 'search', 'eye-off', 'sun', 'moon',
    'refresh-cw', 'folder-open', 'sparkles', 'wand-sparkles', 'book-open', 'circle-alert', 'check', 'power', 'minimize-2',
    'pin', 'scan', 'circle-dot', 'presentation', 'spline', 'list-ordered', 'monitor-smartphone',
    # Phase 4E: инструменты рисования, радиальное меню, оболочка окна
    'slash', 'move-up-right', 'rectangle-horizontal', 'square-round-corner', 'hexagon', 'shapes', 'arrow-left', 'sun-dim',
    'circle-dashed', 'list-restart', 'mouse-pointer-click', 'pipette', 'droplet', 'chevron-left', 'chevron-up', 'grip-vertical',
    'blend', 'case-sensitive', 'sliders-horizontal', 'ellipsis', 'crosshair', 'gem', 'paintbrush', 'circle-x', 'maximize-2',
    'panel-left', 'sparkle', 'square-dashed-mouse-pointer',
    # Phase 4F: карточки плагинов, захват линзы и источника, уведомления
    'layout-grid', 'list', 'hand', 'move-diagonal-2', 'triangle-alert', 'file-down', 'file-up', 'circle-check',
    # Phase 4H: компактные панели действий (стили, профили, плагины), отметка «От создателя», оформление
    'save', 'copy', 'copy-plus', 'text-cursor-input', 'import', 'badge-check', 'sun-moon',
    # Phase 4I: каталог значков плагинов (выбор в настройках отображения), ручки преобразования рисунков
    'pen-tool', 'pencil-line', 'brush', 'highlighter', 'pen-line', 'globe', 'telescope', 'aperture', 'locate-fixed', 'hourglass',
    'alarm-clock', 'clock', 'bell', 'bookmark', 'box', 'briefcase', 'bug', 'calculator', 'calendar', 'chart-column', 'clipboard',
    'cloud', 'code', 'coffee', 'compass', 'cpu', 'database', 'file-text', 'film', 'flag', 'flame', 'gamepad-2', 'gauge', 'gift',
    'git-branch', 'graduation-cap', 'headphones', 'heart', 'image', 'lightbulb', 'link', 'map', 'megaphone', 'message-square',
    'mic', 'paperclip', 'phone', 'printer', 'radio', 'rocket', 'ruler', 'scissors', 'send', 'server', 'shield', 'shopping-cart',
    'tag', 'target', 'terminal', 'thermometer', 'thumbs-up', 'trophy', 'tv', 'video', 'volume-2', 'wifi', 'wrench', 'zap', 'user',
    'users', 'eye', 'dice-5', 'rotate-cw', 'triangle-right', 'scaling', 'bot', 'brain', 'leaf', 'wand', 'clapperboard',
    'screen-share', 'mouse-pointer', 'magnet', 'lamp', 'sticky-note', 'notebook-pen',
]


def num(v):
    f = float(v)
    return ('%g' % f)


def to_path(tag, a):
    if tag == 'path':
        return a['d']
    if tag == 'circle':
        cx, cy, r = float(a['cx']), float(a['cy']), float(a['r'])
        return f"M{num(cx - r)},{num(cy)} A{num(r)},{num(r)} 0 1 0 {num(cx + r)},{num(cy)} A{num(r)},{num(r)} 0 1 0 {num(cx - r)},{num(cy)} Z"
    if tag == 'ellipse':
        cx, cy, rx, ry = float(a['cx']), float(a['cy']), float(a['rx']), float(a['ry'])
        return f"M{num(cx - rx)},{num(cy)} A{num(rx)},{num(ry)} 0 1 0 {num(cx + rx)},{num(cy)} A{num(rx)},{num(ry)} 0 1 0 {num(cx - rx)},{num(cy)} Z"
    if tag == 'rect':
        x, y = float(a.get('x', 0)), float(a.get('y', 0))
        w, h = float(a['width']), float(a['height'])
        rx = float(a.get('rx', a.get('ry', 0)))
        ry = float(a.get('ry', rx))
        if rx <= 0:
            return f"M{num(x)},{num(y)} H{num(x + w)} V{num(y + h)} H{num(x)} Z"
        return (f"M{num(x + rx)},{num(y)} H{num(x + w - rx)} A{num(rx)},{num(ry)} 0 0 1 {num(x + w)},{num(y + ry)} "
                f"V{num(y + h - ry)} A{num(rx)},{num(ry)} 0 0 1 {num(x + w - rx)},{num(y + h)} H{num(x + rx)} "
                f"A{num(rx)},{num(ry)} 0 0 1 {num(x)},{num(y + h - ry)} V{num(y + ry)} A{num(rx)},{num(ry)} 0 0 1 {num(x + rx)},{num(y)} Z")
    if tag == 'line':
        return f"M{a['x1']},{a['y1']} L{a['x2']},{a['y2']}"
    if tag in ('polyline', 'polygon'):
        pts = a['points'].replace(',', ' ').split()
        pairs = [f"{pts[i]},{pts[i + 1]}" for i in range(0, len(pts), 2)]
        return 'M' + ' L'.join(pairs) + (' Z' if tag == 'polygon' else '')
    raise ValueError(tag)


def main():
    pkg = sys.argv[1]
    nodes = json.load(open(os.path.join(pkg, 'icon-nodes.json'), encoding='utf-8'))
    version = json.load(open(os.path.join(pkg, 'package.json'), encoding='utf-8'))['version']
    out = []
    out.append('// <auto-generated>')
    out.append(f'// Сгенерировано tools/gen-lucide.py из lucide-static {version} (icon-nodes.json). Не редактировать вручную.')
    out.append('// Lucide — ISC License, Copyright (c) Lucide Icons and Contributors; часть иконок происходит из Feather')
    out.append('// (MIT, Copyright (c) 2013-present Cole Bemis). Полный текст — docs/release/THIRD-PARTY-NOTICES.md.')
    out.append('// </auto-generated>')
    out.append('namespace Backpack.Visual.Icons;')
    out.append('')
    out.append('public static partial class Lucide')
    out.append('{')
    out.append(f'    public const string Version = "{version}";')
    out.append('')
    out.append('    private static readonly Dictionary<string, string[]> Data = new()')
    out.append('    {')
    for name in ICONS:
        if name not in nodes:
            raise SystemExit('missing icon: ' + name)
        parts = [to_path(tag, attrs) for tag, attrs in nodes[name]]
        joined = ', '.join('"' + p.replace('"', '\\"') + '"' for p in parts)
        out.append(f'        ["{name}"] = [{joined}],')
    out.append('    };')
    out.append('}')
    dst = os.path.join('lab', 'src', 'Backpack.Visual', 'Icons', 'LucideIcons.g.cs')
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    open(dst, 'w', encoding='utf-8', newline='\n').write('\n'.join(out) + '\n')
    print('icons', len(ICONS), 'version', version, '->', dst)


main()
