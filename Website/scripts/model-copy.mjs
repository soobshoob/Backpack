// Complete primary product-model translations.
export const modelCopy = {
  "en": {
    "title": "Backpack — Clear actions. Tools of your own.",
    "intro": "Make presentations, lessons and screen recordings easier to follow. Show each key and click clearly.",
    "extension": "Choose extra tools as independent plugins — or explore how to build one for your own task.",
    "coreTag": "BUILT INTO BACKPACK",
    "coreTitle": "Start with the essentials.",
    "coreText": "Keys and mouse visualization are the two tools at the heart of Backpack. Keep your audience with you, one action at a time.",
    "pluginsTag": "PLUGINS BY THE AUTHOR",
    "pluginsTitle": "Add the tools your story needs.",
    "pluginsText": "Pick a tool and add it independently. These extensions by the Backpack author are useful additions and reference examples for future plugin development.",
    "transitionNote": "This is the target plugin catalog. In the verified preview, drawing and layout are still built in; magnifier tools are also being moved to independent plugins. The browser scenes illustrate that direction, not a completed migration.",
    "builtin": "Built in",
    "authorPlugin": "Author plugin",
    "notPublished": "Public package not yet available",
    "byAuthor": "Author",
    "statuses": {
      "planned": "Plugin planned",
      "source": "Plugin in source",
      "verified": "Plugin verified in package"
    },
    "availability": {
      "planned": "The tool works in preview.3, but its independent plugin is not yet implemented or packaged.",
      "source": "SDK 1.1 sample exists in the current source. This standalone plugin is not verified in preview.3.",
      "verified": "Timer 1.0.0 / SDK 1.0 is verified in the preview.3 Samples folder. The current source is newer; no public download is configured."
    },
    "pluginNames": [
      "Drawing",
      "Layout indicator",
      "Lens",
      "Focus",
      "Cursor magnifier",
      "Timer"
    ],
    "pluginBenefits": [
      "Make temporary notes, shapes and text over your screen while explaining an idea.",
      "Make a change of input language visible without interrupting your lesson.",
      "Move a separate magnified view of a selected screen area.",
      "Dim the surroundings while keeping the chosen area clear at its original size.",
      "Bring details closer with a magnifier that follows your pointer.",
      "Keep a countdown over the screen, then pause and continue when you need to."
    ],
    "developerTag": "CREATE YOUR OWN TOOL",
    "developerTitle": "Your task. Your next tool.",
    "developerText": "Build tools freely for your own tasks. The author’s plugins show how Backpack can be extended, and a detailed step-by-step guide with working examples is being prepared.",
    "developerSteps": [
      [
        "Commands and lifecycle",
        "The SDK connects a plugin’s start, stop and commands to Backpack. Commands can be assigned shortcuts."
      ],
      [
        "Settings and interface",
        "The manifest describes settings; Backpack builds their controls and draws overlay panels. Panel colors are explicit parameters, not a promised automatic theme API."
      ],
      [
        "Localization and examples",
        "Localized manifest labels and Context.Locale support the app’s languages. The included help explains the timer sample, building, packaging and .bppkg import."
      ]
    ],
    "docsNotice": "In the application, open F1 → Plugins for the available contract, build and import instructions. Public SDK documentation and example-source links are not configured yet.",
    "licenseNote": "SDK terms are being updated to support freely creating your own plugins. The current package still uses its existing license; the updated terms, detailed build/import instructions and working examples are coming.",
    "applicationTag": "PART OF THE APPLICATION",
    "applicationTitle": "A setup that stays yours.",
    "applicationText": "Appearance, saved profiles, input privacy and plugin management belong to the application. They are infrastructure, not separate plugins.",
    "privacyTitle": "Input privacy, separately.",
    "privacyText": "The verified preview hides displayed keys when protected input is detected. Detection has limitations and a known focus-notification race: hide the overlay manually before sensitive input. This is separate from trusting plugin code.",
    "workflowTitle": "Demonstration of the new model",
    "workflowNote": "A synthetic walkthrough of the intended interface. Import and trust are implemented in preview.3; this scene is not a screenshot or proof that every catalog plugin is ready.",
    "workflowSteps": [
      "Start with built-in keys and mouse",
      "Open the plugin section",
      "Choose the author’s timer",
      "Confirm trust, then turn it on",
      "The additional tool appears in the scene"
    ],
    "coreTab": "Built in",
    "pluginsTab": "Plugins",
    "enable": "Turn on",
    "trust": "Only enable code from a source you trust. A separate process is not a sandbox.",
    "enabled": "Enabled",
    "start": "Start",
    "pause": "Pause",
    "resume": "Continue",
    "reset": "Reset",
    "finished": "Finished",
    "timerCaption": "Synthetic countdown · pause · continue",
    "layoutCaption": "Synthetic input-language change",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "What comes with the application?",
        "The target core contains keys and mouse visualization. Profiles, styling, input privacy and plugin management are application features. Preview.3 still includes legacy tools during the plugin migration; only the timer sample is verified as an independent plugin."
      ],
      [
        "Can I download Backpack or these plugins?",
        "The verified Backpack 0.4.0-preview.3 package is unsigned and unpublished. A local verified package is not a public download. Catalog statuses distinguish planned plugins, source examples and a packaged sample."
      ],
      [
        "How are extensions installed and enabled?",
        "In the verified preview: Plugins → Import a plugin → select .bppkg. Import validates and unpacks the package without running code or granting trust. A new or updated plugin starts disabled. First enable requires the application’s trust confirmation."
      ],
      [
        "Are plugins isolated safely?",
        "Install only from trusted sources. Plugins run in separate user-rights processes; that helps manage faults but is not a security sandbox. Manifest capabilities limit the Backpack interface, not everything the plugin can do on the computer."
      ],
      [
        "Where do I start making a tool?",
        "Start with the author’s presentation timer. A detailed guide with working examples will cover the contract, lifecycle, commands, settings, localization, building and .bppkg import. Current instructions are in F1 → Plugins. Public SDK/source links and updated SDK terms are being prepared."
      ],
      [
        "Does key privacy protect every field?",
        "No universal detection is promised. Protected and unknown input can be hidden, but asynchronous focus detection has limitations. Manually hide the overlay before sensitive input. Plugin trust is a separate decision."
      ],
      [
        "Which systems and license apply?",
        "The verified package targets Windows 10/11 x64 and includes .NET. Backpack is under PolyForm Strict 1.0.0; it is not open source. SDK terms are being updated for freely creating plugins."
      ]
    ]
  },
  "uk": {
    "title": "Backpack — Зрозумілі дії. Власні інструменти.",
    "intro": "Робіть презентації, уроки та записи екрана зрозумілішими. Показуйте кожну клавішу й клацання.",
    "extension": "Додавайте потрібні інструменти окремими плагінами — або дізнайтеся, як створити власний для своєї задачі.",
    "coreTag": "ВБУДОВАНО В BACKPACK",
    "coreTitle": "Почніть з основного.",
    "coreText": "Клавіші та візуалізація миші — два основні інструменти Backpack. Допоможіть аудиторії стежити за кожною дією.",
    "pluginsTag": "ПЛАГІНИ АВТОРА",
    "pluginsTitle": "Додайте інструменти для своєї розповіді.",
    "pluginsText": "Обирайте інструмент і додавайте його незалежно. Плагіни автора Backpack — корисні доповнення та приклади для майбутніх власних розширень.",
    "transitionNote": "Це цільовий каталог плагінів. У перевіреній версії малювання та розкладка ще вбудовані; інструменти збільшення також переносяться в окремі плагіни. Сцени сайту ілюструють цей напрям, а не завершений перенос.",
    "builtin": "Вбудовано",
    "authorPlugin": "Плагін автора",
    "notPublished": "Публічний пакет ще не доступний",
    "byAuthor": "Автор",
    "statuses": {
      "planned": "Плагін заплановано",
      "source": "Плагін у вихідному коді",
      "verified": "Плагін перевірено в пакеті"
    },
    "availability": {
      "planned": "Інструмент працює в preview.3, але окремий плагін ще не реалізовано й не запаковано.",
      "source": "Приклад SDK 1.1 є в поточному коді. Цей окремий плагін не перевірено в preview.3.",
      "verified": "Таймер 1.0.0 / SDK 1.0 перевірено в папці Samples пакета preview.3. Поточний код новіший; публічного завантаження немає."
    },
    "pluginNames": [
      "Малювання",
      "Індикатор розкладки",
      "Лінза",
      "Фокус",
      "Збільшення біля курсора",
      "Таймер"
    ],
    "pluginBenefits": [
      "Робіть тимчасові помітки, фігури й текст поверх екрана під час пояснення.",
      "Показуйте зміну мови введення, не перериваючи урок.",
      "Переміщуйте окреме збільшене зображення обраної ділянки екрана.",
      "Затемнюйте оточення, залишаючи обрану область ясною в початковому масштабі.",
      "Наближайте деталі за допомогою збільшення, що слідує за вказівником.",
      "Тримайте відлік поверх екрана, зупиняйте й продовжуйте його за потреби."
    ],
    "developerTag": "СТВОРІТЬ ВЛАСНИЙ ІНСТРУМЕНТ",
    "developerTitle": "Ваша задача. Ваш новий інструмент.",
    "developerText": "Вільно створюйте інструменти для своїх задач. Плагіни автора показують, як розширювати Backpack; готується докладна покрокова інструкція з робочими прикладами.",
    "developerSteps": [
      [
        "Команди й життєвий цикл",
        "SDK поєднує запуск, зупинку та команди плагіна з Backpack. Командам можна призначати сполучення клавіш."
      ],
      [
        "Налаштування й інтерфейс",
        "Маніфест описує налаштування; Backpack створює елементи керування й малює панелі поверх екрана. Кольори панелей задаються явно; автоматичний API теми не обіцяється."
      ],
      [
        "Локалізація й приклади",
        "Локалізовані підписи маніфесту та Context.Locale підтримують мови програми. Вбудована довідка пояснює приклад таймера, збірку, пакування та імпорт .bppkg."
      ]
    ],
    "docsNotice": "У програмі відкрийте F1 → Плагіни: там є контракт та інструкції зі збірки й імпорту. Публічні посилання на документацію SDK і код прикладів ще не задано.",
    "licenseNote": "Умови SDK оновлюються для вільного створення власних плагінів. Поточний пакет зберігає чинну ліцензію; нові умови, докладна інструкція зі збірки й імпорту та робочі приклади готуються.",
    "applicationTag": "МОЖЛИВОСТІ ПРОГРАМИ",
    "applicationTitle": "Налаштування, які залишаються вашими.",
    "applicationText": "Оформлення, збережені профілі, приватність введення й керування плагінами належать програмі. Це інфраструктура, а не окремі плагіни.",
    "privacyTitle": "Приватність введення — окремо.",
    "privacyText": "Перевірена версія приховує клавіші, коли визначає захищене введення. Визначення має обмеження й відому гонку сповіщення про фокус: перед особистим введенням приховуйте відображення вручну. Довіра до коду плагіна — окреме питання.",
    "workflowTitle": "Демонстрація нової моделі",
    "workflowNote": "Штучний сценарій цільового інтерфейсу. Імпорт і довіру реалізовано в preview.3; ця сцена не є знімком програми чи доказом готовності всіх плагінів.",
    "workflowSteps": [
      "Починаємо з вбудованих клавіш і миші",
      "Відкриваємо розділ плагінів",
      "Обираємо таймер автора",
      "Підтверджуємо довіру й вмикаємо",
      "Додатковий інструмент з’являється в сцені"
    ],
    "coreTab": "Вбудоване",
    "pluginsTab": "Плагіни",
    "enable": "Увімкнути",
    "trust": "Вмикайте лише код із довірених джерел. Окремий процес — не безпечна пісочниця.",
    "enabled": "Увімкнено",
    "start": "Старт",
    "pause": "Пауза",
    "resume": "Продовжити",
    "reset": "Скинути",
    "finished": "Завершено",
    "timerCaption": "Штучний відлік · пауза · продовження",
    "layoutCaption": "Штучна зміна мови введення",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Що входить до програми?",
        "Цільове ядро містить клавіші та візуалізацію миші. Профілі, оформлення, приватність і керування плагінами — можливості програми. У preview.3 під час переносу залишаються старі інструменти; лише приклад таймера перевірено як окремий плагін."
      ],
      [
        "Чи можна завантажити Backpack і плагіни?",
        "Перевірений Backpack 0.4.0-preview.3 не підписано й не опубліковано. Наявність локального пакета не означає публічного завантаження. Статуси каталогу відрізняють плани, вихідний код і перевірений приклад у пакеті."
      ],
      [
        "Як установлюються й вмикаються розширення?",
        "У перевіреній версії: Плагіни → Імпортувати плагін → файл .bppkg. Імпорт перевіряє й розпаковує пакет, не запускаючи код і не надаючи довіри. Новий або оновлений плагін вимкнено. Перше ввімкнення потребує підтвердження довіри в програмі."
      ],
      [
        "Чи безпечно ізольовано плагіни?",
        "Установлюйте лише з довірених джерел. Плагіни працюють в окремих процесах із правами користувача; це допомагає керувати збоями, але не є захищеною пісочницею. Можливості маніфесту обмежують інтерфейс Backpack, а не всі дії коду на комп’ютері."
      ],
      [
        "З чого почати власний інструмент?",
        "Почніть із таймера автора. Докладний посібник із робочими прикладами охопить контракт, життєвий цикл, команди, налаштування, локалізацію, збірку та імпорт .bppkg. Поточні інструкції є у F1 → Плагіни. Публічні посилання й оновлені умови SDK готуються."
      ],
      [
        "Чи захищено введення в усіх полях?",
        "Універсальне визначення не обіцяється. Захищене й невідоме введення можуть приховуватися, але асинхронне визначення фокуса має обмеження. Перед чутливим введенням приховуйте відображення вручну. Довіра до плагінів — окреме рішення."
      ],
      [
        "Які системи й ліцензія?",
        "Перевірений пакет призначено для Windows 10/11 x64 і містить .NET. Backpack використовує PolyForm Strict 1.0.0 і не є open source. Умови SDK оновлюються для вільного створення плагінів."
      ]
    ]
  }
};
