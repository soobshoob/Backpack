# Формат файла темы

**Работает сейчас:** формат описан, схема и примеры есть в репозитории и проверяются автоматическими тестами валидатором JSON Schema. Программа файлы тем ещё не читает. Файл настроек стенда хранит внешний вид разделами той же формы (`keycap`, `colors`, `text`, `layout`, `border`, `group`, `animation`, `mouse`, `languageIndicator`).

**Запланировано:** чтение и проверка — этап 2.

Схема: `docs/themes/backpack-theme.schema.json` (JSON Schema 2020-12). Примеры: `docs/themes/examples/`.

## Минимальная тема

```json
{
  "format": "backpack-theme",
  "formatVersion": 1,
  "id": "ivan.blue-keys",
  "name": "Синие клавиши",
  "keycap": { "style": "lowprofile" },
  "colors": { "primary": "#2F6FEB", "secondary": "#173A7A" },
  "text": { "color": "#FFFFFF" }
}
```

Всё, что не указано, берётся по умолчанию.

## Обязательные поля

| Поле | Значение |
|---|---|
| `format` | всегда `"backpack-theme"` |
| `formatVersion` | всегда `1` для этой версии формата |
| `id` | идентификатор: строчные латинские буквы, цифры, точка и дефис, от 3 до 64 символов; не начинается и не кончается точкой или дефисом. Префикс `backpack.` занят встроенными темами |
| `name` | название, до 64 символов |
| `keycap.style` | `lowprofile`, `laptop`, `pbt` или `minimal` |

Необязательные: `description`, `author`, `version` (например `"1.0.0"`), `license`, `$schema`, `extensions` (данные для сторонних инструментов, программа их не трогает).

## Единицы

- Размеры — в единицах WPF при масштабе 100 %. При масштабе экрана 150 % размер 32 займёт 48 физических пикселей.
- `border.radius` — доля от 0 до 1, а не пиксели.
- Длительность — целые миллисекунды.
- Цвет — `#RRGGBB` или `#RRGGBBAA`. Последняя пара — прозрачность: `FF` непрозрачно, `00` полностью прозрачно, `99` — около 60 %. Названия цветов (`red`) и `rgb()` не принимаются.

## Параметры

| Поле | По умолчанию | Допустимо |
|---|---|---|
| `keycap.size` | 32 | 8–128 |
| `colors.primary` | `#FFFFFF` | цвет |
| `colors.secondary` | `#1A1A1A` | цвет |
| `colors.useGradient` | `true` | `true` / `false` |
| `text.color` | `#000000` | цвет |
| `text.caps` | `capitalize` | `uppercase`, `capitalize`, `lowercase` |
| `text.variant` | `text-short` | `text`, `text-short`, `icon` |
| `text.alignment` | `center` | `top-left`, `top-center`, `top-right`, `center-left`, `center`, `center-right`, `bottom-left`, `bottom-center`, `bottom-right` |
| `text.labelSet` | `windows` | `windows`, `macos`, `linux` — набор обозначений клавиш |
| `text.font.families` | `["Inter", "Segoe UI"]` | от 1 до 8 имён шрифтов |
| `text.font.weight` | 400 | 100–900 |
| `text.font.secondaryWeight` | 600 | 100–900 |
| `layout.showIcon` | `true` | `true` / `false` |
| `layout.showSymbol` | `true` | `true` / `false` |
| `layout.showPressCount` | `true` | `true` / `false` |
| `layout.sideMarkers` | `true` | метки «R» и «num» |
| `layout.iconAlignment` | `end` | `start`, `center`, `end` |
| `modifier.highlight` | `false` | `true` / `false` |
| `modifier.primary` | `#3A86FF` | цвет |
| `modifier.secondary` | `#000000` | цвет |
| `modifier.text` | `#000000` | цвет |
| `modifier.border` | `#000000` | цвет |
| `border.enabled` | `true` | `true` / `false` |
| `border.width` | 2 | 0.5–16 |
| `border.color` | `#1A1A1A` | цвет |
| `border.radius` | 0.5 | 0–1 |
| `group.background.enabled` | `true` | `true` / `false` |
| `group.background.color` | `#FFFFFF99` | цвет |
| `animation.type` | `fade` | `none`, `fade`, `zoom`, `float`, `slide` |
| `animation.durationMs` | 250 | 50–1000 |
| `animation.press` | `true` | анимация нажатия; `false` — грань опускается сразу |
| `mouse.clickColor` | `#009DFF` | цвет |
| `mouse.clickSize` | 150 | 20–400 |
| `mouse.indicatorSize` | 50 | 16–200 |
| `mouse.indicatorBackground` | `#00000080` | цвет |
| `mouse.indicatorForeground` | `#FFFFFF` | цвет |
| `mouse.indicatorAccent` | `#00FF6A` | цвет нажатой кнопки |
| `languageIndicator.background` | подложка группы темы | цвет |
| `languageIndicator.foreground` | цвет текста темы | цвет |
| `languageIndicator.border` | цвет рамки темы | цвет |
| `languageIndicator.borderWidth` | 1 | 0–16, 0 — без рамки |
| `languageIndicator.radius` | 0.5 | 0–1, 1 — «пилюля» |
| `languageIndicator.textSize` | 36 | 12–160 |
| `languageIndicator.showLayoutName` | `ambiguous` | `always`, `ambiguous` (только если у языка несколько раскладок), `never` |

Значения `mouse.*` по умолчанию в таблице — как у Keyviz (кольцо клика). В настройках стенда для режима «мышь у курсора» свои значения по умолчанию: круг жёлтый с непрозрачностью 35 %, диаметр 48, значок 30, нажатая кнопка #22C55E. Прозрачность круга — альфа `clickColor`.

Какие параметры использует каждый стиль, описано в разделе «Что запланировано» справки (`docs/help/ru/10-planned.md`).

## Пример с выделением модификаторов

```json
{
  "format": "backpack-theme",
  "formatVersion": 1,
  "id": "ivan.night-mods",
  "name": "Ночь, модификаторы",
  "keycap": { "style": "lowprofile", "size": 30 },
  "colors": { "primary": "#2E3138", "secondary": "#16181C" },
  "text": { "color": "#F2E6D0", "caps": "uppercase" },
  "modifier": { "highlight": true, "primary": "#1F6F78", "secondary": "#0E3A3F", "text": "#E8FFFB", "border": "#0E3A3F" },
  "border": { "width": 1.5, "color": "#0B0C0E", "radius": 0.35 },
  "group": { "background": { "enabled": true, "color": "#0B0C0ECC" } },
  "animation": { "type": "float", "durationMs": 200 }
}
```

## Чего в теме быть не может

Поля `monitor`, `hotkey`, `source`, `filter`, положение и отступы оверлея, выдержка. Такой файл не загрузится с сообщением «Тема не может задавать …». Эти параметры меняются в настройках.

## Ошибки и предупреждения

| Ситуация | Что произойдёт |
|---|---|
| ошибка синтаксиса JSON | тема не загрузится; будут показаны строка и символ |
| нет `format` или `formatVersion` | «Это не тема Backpack» |
| `formatVersion` больше 1 | «Тема создана для более новой версии программы» |
| неверный тип, значение вне диапазона, неизвестный стиль, неверный цвет | тема не загрузится; будут показаны поле и причина |
| неизвестное поле | поле пропускается, тема загружается, показывается предупреждение |
| шрифт не установлен | используется следующий из списка или шрифт по умолчанию, показывается предупреждение |

Тема с хотя бы одной ошибкой не применяется частично: либо вся, либо никак.
