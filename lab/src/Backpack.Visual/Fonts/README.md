# Шрифт Inter

| Поле | Значение |
|---|---|
| Проект | Inter, https://github.com/rsms/inter |
| Версия | 4.1, релиз https://github.com/rsms/inter/releases/tag/v4.1 |
| Архив | `Inter-4.1.zip`, SHA-256 `9883fdd4a49d4fb66bd8177ba6625ef9a64aa45899767dde3d36aa425756b11e`, загружен 2026-10-28 |
| Файлы | `extras/ttf/Inter-Regular.ttf` (`40d692fc…0c82`), `extras/ttf/Inter-SemiBold.ttf` (`78a843fa…fcde3`), `extras/ttf/Inter-Bold.ttf` (`288316…947f`) — без изменений |
| Лицензия | SIL Open Font License 1.1, текст — `Inter-LICENSE.txt` (копия `LICENSE.txt` из архива) |

Шрифт встроен в сборку `Backpack.Visual` как ресурс WPF и в системе не устанавливается. Keyviz использует вариативный Inter (`@fontsource-variable/inter`); здесь — статические начертания, поэтому метрики могут отличаться (Phase 2A, TX-05).

# Шрифт Spectral

| Поле | Значение |
|---|---|
| Проект | Spectral, Production Type, https://github.com/productiontype/Spectral |
| Источник | Google Fonts, `ofl/spectral`, коммит `8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5` (копия в `references/fonts/Spectral`, см. `SOURCE.txt`) |
| Файлы | `Spectral-Regular.ttf` (`c89021dc…8f5d`), `Spectral-Italic.ttf` (`7ec97244…f787`), `Spectral-SemiBold.ttf` (`5f86915a…e94b`), `Spectral-Bold.ttf` (`70ddb1ec…af9b`) — без изменений |
| Состав | латиница, latin-ext, кириллица, cyrillic-ext, вьетнамский (METADATA.pb); начертания 400, 400 курсив, 600, 700 — те, что использует интерфейс (обычный, полужирный, жирный; курсив — выделение в справке) |
| Лицензия | SIL Open Font License 1.1, текст — `Spectral-LICENSE.txt` (копия `OFL.txt`); Reserved Font Name не объявлено |

Шрифт встроен в сборку `Backpack.Visual` как ресурс WPF и в системе не устанавливается. Выбирается отдельно для интерфейса и для
подписей кейкапов («Стандартный» / «Spectral»). Символов, которых в Spectral нет (стрелки, ⌘ ⌥ ⇧, греческий и др.), WPF берёт из
следующих семейств цепочки: Inter → Segoe UI (Symbol). Иконки — векторные и от шрифта не зависят.
