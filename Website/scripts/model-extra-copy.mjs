// Complete European product-model copy, including upcoming SDK terms and guides.
export const extraModelCopy = {
  "de": {
    "title": "Backpack — Klare Aktionen. Eigene Werkzeuge.",
    "intro": "Machen Sie Präsentationen, Unterricht und Bildschirmaufnahmen verständlicher. Zeigen Sie jede Taste und jeden Klick.",
    "extension": "Wählen Sie zusätzliche Werkzeuge als unabhängige Plugins — oder erkunden Sie die Entwicklung eigener Erweiterungen.",
    "coreTag": "IN BACKPACK INTEGRIERT",
    "coreTitle": "Beginnen Sie mit dem Wesentlichen.",
    "coreText": "Tasten und Mausvisualisierung bilden den Kern von Backpack. Ihr Publikum folgt jeder Aktion.",
    "pluginsTag": "PLUGINS DES AUTORS",
    "pluginsTitle": "Die passenden Werkzeuge für Ihre Geschichte.",
    "pluginsText": "Die Erweiterungen des Backpack-Autors lassen sich unabhängig hinzufügen und dienen zugleich als Beispiele für künftige eigene Plugins.",
    "transitionNote": "Dies ist der Zielkatalog. In der geprüften Vorschau sind Zeichnen und Layout noch integriert; die Vergrößerungswerkzeuge werden ebenfalls in Plugins überführt. Die Szenen zeigen die geplante Richtung, keine abgeschlossene Migration.",
    "builtin": "Integriert",
    "authorPlugin": "Plugin des Autors",
    "notPublished": "Noch kein öffentliches Paket",
    "byAuthor": "Autor",
    "statuses": {
      "planned": "Plugin geplant",
      "source": "Plugin im Quellcode",
      "verified": "Plugin im Paket geprüft"
    },
    "availability": {
      "planned": "Das Werkzeug funktioniert in preview.3. Ein eigenständiges Plugin ist noch nicht implementiert oder verpackt.",
      "source": "Ein SDK-1.1-Beispiel liegt im aktuellen Quellcode. Dieses eigenständige Plugin wurde nicht in preview.3 geprüft.",
      "verified": "Timer 1.0.0 / SDK 1.0 wurde im Samples-Ordner von preview.3 geprüft. Der aktuelle Quellcode ist neuer; kein öffentlicher Download."
    },
    "pluginNames": [
      "Zeichnen",
      "Layoutanzeige",
      "Lupe",
      "Fokus",
      "Zeigerlupe",
      "Timer"
    ],
    "pluginBenefits": [
      "Temporäre Notizen, Formen und Text während Ihrer Erklärung.",
      "Den Wechsel der Eingabesprache sichtbar machen.",
      "Eine separate vergrößerte Ansicht eines gewählten Bereichs verschieben.",
      "Die Umgebung abdunkeln, den gewählten Bereich in Originalgröße klar lassen.",
      "Details mit einer Lupe verfolgen, die dem Zeiger folgt.",
      "Countdown über dem Bildschirm, mit Pause und Fortsetzen."
    ],
    "developerTag": "EIGENES WERKZEUG ERSTELLEN",
    "developerTitle": "Ihre Aufgabe. Ihr nächstes Werkzeug.",
    "developerText": "Die Autoren-Plugins zeigen den Vertrag mit Backpack. Detaillierte Anleitungen und funktionierende Beispiele werden auf Grundlage des Timers vorbereitet.",
    "developerSteps": [
      [
        "Befehle und Lebenszyklus",
        "Das SDK verbindet Start, Stopp und Befehle mit Backpack. Befehlen können Tastenkürzel zugewiesen werden."
      ],
      [
        "Einstellungen und Oberfläche",
        "Das Manifest beschreibt Einstellungen; Backpack zeichnet Bedienelemente und Overlay-Panels. Panelfarben sind explizite Parameter, keine zugesicherte automatische Theme-API."
      ],
      [
        "Lokalisierung und Beispiele",
        "Lokalisierte Manifesttexte und Context.Locale unterstützen die App-Sprachen. Die Hilfe erklärt Timer, Build, Verpackung und .bppkg-Import."
      ]
    ],
    "docsNotice": "F1 → Plugins erklärt Vertrag, Build und Import. Öffentliche Links zu SDK-Dokumentation und Beispielquellen fehlen noch.",
    "licenseNote": "Die SDK-Bedingungen werden aktualisiert, damit Sie eigene Plugins frei erstellen können. Detaillierte Schritt-für-Schritt-Anleitungen für Build und Import sowie funktionierende Beispiele werden vorbereitet.",
    "applicationTag": "TEIL DER ANWENDUNG",
    "applicationTitle": "Ihre Einrichtung bleibt Ihre.",
    "applicationText": "Darstellung, gespeicherte Profile, Eingabeprivatsphäre und Pluginverwaltung sind Anwendungsinfrastruktur, keine separaten Plugins.",
    "privacyTitle": "Eingabeprivatsphäre — getrennt betrachtet.",
    "privacyText": "Die geprüfte Vorschau verbirgt Tasten bei erkannter geschützter Eingabe. Die Erkennung hat Grenzen und eine bekannte verzögerte Fokusmeldung: vor sensibler Eingabe manuell ausblenden. Vertrauen in Plugin-Code ist davon getrennt.",
    "workflowTitle": "Demonstration des neuen Modells",
    "workflowNote": "Simulierter Ablauf der geplanten Oberfläche. Import und Vertrauen sind in preview.3 implementiert; die Szene ist kein Screenshot oder Nachweis, dass alle Plugins fertig sind.",
    "workflowSteps": [
      "Mit integrierten Tasten und Maus beginnen",
      "Pluginbereich öffnen",
      "Timer des Autors wählen",
      "Vertrauen bestätigen und einschalten",
      "Das zusätzliche Werkzeug erscheint"
    ],
    "coreTab": "Integriert",
    "pluginsTab": "Plugins",
    "enable": "Einschalten",
    "trust": "Nur Code aus vertrauenswürdigen Quellen einschalten. Ein separater Prozess ist keine Sandbox.",
    "enabled": "Aktiviert",
    "start": "Start",
    "pause": "Pause",
    "resume": "Fortsetzen",
    "reset": "Zurücksetzen",
    "finished": "Beendet",
    "timerCaption": "Simulierter Countdown · Pause · Fortsetzen",
    "layoutCaption": "Simulierter Wechsel der Eingabesprache",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Was gehört zur Anwendung?",
        "Zielkern: Tasten und Mausvisualisierung. Profile, Darstellung, Eingabeprivatsphäre und Pluginverwaltung gehören zur App. preview.3 enthält während der Migration noch alte Werkzeuge; nur der Timer ist als unabhängiges Plugin geprüft."
      ],
      [
        "Sind Backpack und Plugins herunterladbar?",
        "Backpack 0.4.0-preview.3 ist geprüft, unsigniert und unveröffentlicht. Ein lokales Paket ist kein öffentlicher Download. Der Katalog unterscheidet Planung, Quellcode und geprüftes Paket."
      ],
      [
        "Wie werden Erweiterungen installiert?",
        "Plugins → Plugin importieren → .bppkg. Import prüft und entpackt ohne Codeausführung oder Vertrauensfreigabe. Neue und aktualisierte Plugins sind aus; erstes Einschalten erfordert die Vertrauensbestätigung."
      ],
      [
        "Sind Plugins sicher isoliert?",
        "Nur vertrauenswürdige Quellen verwenden. Separate Prozesse laufen mit Benutzerrechten und begrenzen Fehler, sind aber keine Sandbox. Manifestfähigkeiten begrenzen die Backpack-Schnittstelle, nicht alle Systemzugriffe."
      ],
      [
        "Wie beginne ich ein eigenes Werkzeug?",
        "F1 → Plugins erklärt Vertrag, Lebenszyklus, Befehle, Einstellungen, Lokalisierung und Timer-Build. Ausführliche Anleitungen und funktionierende Beispiele werden vorbereitet; die neuen SDK-Bedingungen sollen die freie Erstellung eigener Plugins ermöglichen. Öffentliche Links folgen."
      ],
      [
        "Schützt Eingabeprivatsphäre jedes Feld?",
        "Keine universelle Erkennung zugesichert. Geschützte und unbekannte Eingaben können verborgen werden, asynchrone Fokuserkennung hat Grenzen. Vor sensibler Eingabe manuell ausblenden. Pluginvertrauen separat entscheiden."
      ],
      [
        "Welche Systeme und Lizenz gelten?",
        "Geprüftes Paket für Windows 10/11 x64, mit .NET. Backpack verwendet PolyForm Strict 1.0.0 und ist nicht Open Source. Die SDK-/Beispielbedingungen werden aktualisiert."
      ]
    ]
  },
  "fr": {
    "title": "Backpack — Des actions claires. Vos propres outils.",
    "intro": "Rendez présentations, cours et enregistrements d’écran plus faciles à suivre. Montrez chaque touche et clic.",
    "extension": "Ajoutez des outils sous forme de plugins indépendants ou découvrez comment créer le vôtre.",
    "coreTag": "INTÉGRÉ À BACKPACK",
    "coreTitle": "Commencez par l’essentiel.",
    "coreText": "Les touches et la visualisation de la souris sont les deux outils du cœur de Backpack.",
    "pluginsTag": "PLUGINS DE L’AUTEUR",
    "pluginsTitle": "Les outils adaptés à votre récit.",
    "pluginsText": "Les extensions de l’auteur de Backpack s’ajoutent indépendamment et servent d’exemples pour de futurs plugins personnels.",
    "transitionNote": "Voici le catalogue cible. Dans l’aperçu vérifié, dessin et disposition restent intégrés ; les outils de grossissement migrent aussi vers des plugins. Les scènes illustrent cette direction, pas une migration terminée.",
    "builtin": "Intégré",
    "authorPlugin": "Plugin de l’auteur",
    "notPublished": "Paquet public indisponible",
    "byAuthor": "Auteur",
    "statuses": {
      "planned": "Plugin prévu",
      "source": "Plugin dans les sources",
      "verified": "Plugin vérifié dans le paquet"
    },
    "availability": {
      "planned": "L’outil fonctionne dans preview.3, mais son plugin indépendant n’est ni implémenté ni empaqueté.",
      "source": "Un exemple SDK 1.1 existe dans les sources actuelles. Ce plugin indépendant n’est pas vérifié dans preview.3.",
      "verified": "Minuteur 1.0.0 / SDK 1.0 est vérifié dans Samples de preview.3. Les sources actuelles sont plus récentes ; aucun téléchargement public."
    },
    "pluginNames": [
      "Dessin",
      "Indicateur de disposition",
      "Loupe",
      "Mise en évidence",
      "Loupe du pointeur",
      "Minuteur"
    ],
    "pluginBenefits": [
      "Ajoutez des notes, formes et textes temporaires pendant l’explication.",
      "Montrez le changement de langue de saisie.",
      "Déplacez une vue agrandie distincte d’une zone choisie.",
      "Assombrissez l’entourage en gardant la zone choisie claire à sa taille originale.",
      "Agrandissez les détails avec une loupe suivant le pointeur.",
      "Affichez un compte à rebours, mettez-le en pause et reprenez-le."
    ],
    "developerTag": "CRÉER VOTRE OUTIL",
    "developerTitle": "Votre tâche. Votre prochain outil.",
    "developerText": "Les plugins de l’auteur illustrent le contrat avec Backpack. Des instructions détaillées et des exemples fonctionnels sont en préparation à partir du minuteur.",
    "developerSteps": [
      [
        "Commandes et cycle de vie",
        "Le SDK relie démarrage, arrêt et commandes à Backpack. Les commandes acceptent des raccourcis."
      ],
      [
        "Réglages et interface",
        "Le manifeste décrit les réglages ; Backpack dessine leurs contrôles et les panneaux. Les couleurs sont des paramètres explicites, pas une API de thème automatique garantie."
      ],
      [
        "Localisation et exemples",
        "Les libellés localisés et Context.Locale suivent les langues de l’application. L’aide explique le minuteur, la compilation, l’empaquetage et l’import .bppkg."
      ]
    ],
    "docsNotice": "F1 → Plugins décrit le contrat, la compilation et l’import. Aucun lien public vers la documentation SDK ou les sources d’exemples n’est configuré.",
    "licenseNote": "Les conditions du SDK sont mises à jour pour permettre de créer librement vos propres plugins. Des guides détaillés étape par étape pour la compilation et l’import, ainsi que des exemples fonctionnels, sont en préparation.",
    "applicationTag": "PARTIE DE L’APPLICATION",
    "applicationTitle": "Une configuration qui vous appartient.",
    "applicationText": "Apparence, profils enregistrés, confidentialité de saisie et gestion des plugins sont une infrastructure, pas des plugins séparés.",
    "privacyTitle": "La confidentialité de saisie, à part.",
    "privacyText": "L’aperçu vérifié masque les touches lorsqu’une saisie protégée est détectée. La détection a des limites et une course connue des notifications de focus : masquez manuellement avant toute saisie sensible. La confiance dans le code est distincte.",
    "workflowTitle": "Démonstration du nouveau modèle",
    "workflowNote": "Parcours simulé de l’interface cible. Import et confiance sont implémentés dans preview.3 ; cette scène n’est ni une capture ni une preuve que tous les plugins sont prêts.",
    "workflowSteps": [
      "Commencer avec touches et souris intégrées",
      "Ouvrir les plugins",
      "Choisir le minuteur de l’auteur",
      "Confirmer la confiance et activer",
      "L’outil supplémentaire apparaît"
    ],
    "coreTab": "Intégré",
    "pluginsTab": "Plugins",
    "enable": "Activer",
    "trust": "Activez seulement du code de source fiable. Un processus séparé n’est pas un bac à sable.",
    "enabled": "Activé",
    "start": "Démarrer",
    "pause": "Pause",
    "resume": "Reprendre",
    "reset": "Réinitialiser",
    "finished": "Terminé",
    "timerCaption": "Compte à rebours simulé · pause · reprise",
    "layoutCaption": "Changement de langue de saisie simulé",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Que contient l’application ?",
        "Le cœur cible comprend touches et souris. Profils, apparence, confidentialité et gestion des plugins appartiennent à l’application. preview.3 garde des outils historiques pendant la migration ; seul le minuteur est vérifié comme plugin indépendant."
      ],
      [
        "Peut-on télécharger Backpack et les plugins ?",
        "Backpack 0.4.0-preview.3 est vérifié, non signé et non publié. Un paquet local ne constitue pas un téléchargement public. Le catalogue distingue projets, sources et exemple empaqueté."
      ],
      [
        "Comment installer les extensions ?",
        "Plugins → Importer un plugin → .bppkg. L’import vérifie et extrait sans exécuter de code ni accorder la confiance. Tout plugin nouveau ou mis à jour est désactivé ; sa première activation exige confirmation."
      ],
      [
        "Les plugins sont-ils isolés en sécurité ?",
        "Utilisez des sources fiables. Les processus séparés ont vos droits et facilitent la gestion des pannes, mais ne sont pas des bacs à sable. Les capacités du manifeste limitent l’interface Backpack, pas toutes les actions système."
      ],
      [
        "Comment commencer un outil ?",
        "F1 → Plugins explique contrat, cycle de vie, commandes, réglages, langues et compilation du minuteur. Des guides détaillés et exemples fonctionnels sont préparés ; les nouvelles conditions SDK permettront de créer librement vos plugins. Les liens publics suivront."
      ],
      [
        "La confidentialité protège-t-elle tous les champs ?",
        "Aucune détection universelle n’est promise. Les saisies protégées ou inconnues peuvent être masquées ; la détection asynchrone du focus a des limites. Masquez avant une saisie sensible. La confiance des plugins est distincte."
      ],
      [
        "Quels systèmes et quelle licence ?",
        "Paquet vérifié pour Windows 10/11 x64 avec .NET. Backpack utilise PolyForm Strict 1.0.0 et n’est pas open source. Les conditions SDK/exemples sont en cours de mise à jour."
      ]
    ]
  },
  "es": {
    "title": "Backpack — Acciones claras. Tus propias herramientas.",
    "intro": "Haz más fáciles de seguir las presentaciones, clases y grabaciones. Muestra cada tecla y clic.",
    "extension": "Añade herramientas como plugins independientes o descubre cómo crear la tuya.",
    "coreTag": "INTEGRADO EN BACKPACK",
    "coreTitle": "Empieza por lo esencial.",
    "coreText": "Las teclas y la visualización del ratón son las dos herramientas del núcleo de Backpack.",
    "pluginsTag": "PLUGINS DEL AUTOR",
    "pluginsTitle": "Las herramientas que tu historia necesita.",
    "pluginsText": "Las extensiones del autor de Backpack se añaden por separado y sirven de ejemplos para futuros plugins propios.",
    "transitionNote": "Este es el catálogo objetivo. En la versión verificada, dibujo y distribución siguen integrados; las lupas también se trasladan a plugins. Las escenas ilustran esa dirección, no una migración terminada.",
    "builtin": "Integrado",
    "authorPlugin": "Plugin del autor",
    "notPublished": "Paquete público aún no disponible",
    "byAuthor": "Autor",
    "statuses": {
      "planned": "Plugin previsto",
      "source": "Plugin en el código fuente",
      "verified": "Plugin verificado en el paquete"
    },
    "availability": {
      "planned": "La herramienta funciona en preview.3, pero su plugin independiente aún no está implementado ni empaquetado.",
      "source": "Existe un ejemplo SDK 1.1 en el código actual. Este plugin independiente no está verificado en preview.3.",
      "verified": "Temporizador 1.0.0 / SDK 1.0 está verificado en Samples de preview.3. El código actual es más reciente; no hay descarga pública."
    },
    "pluginNames": [
      "Dibujo",
      "Indicador de distribución",
      "Lupa",
      "Enfoque",
      "Lupa del cursor",
      "Temporizador"
    ],
    "pluginBenefits": [
      "Añade notas, formas y texto temporales mientras explicas.",
      "Haz visible el cambio de idioma de entrada.",
      "Mueve una vista ampliada independiente del área elegida.",
      "Oscurece el entorno y conserva el área elegida clara a tamaño original.",
      "Amplía detalles con una lupa que sigue al puntero.",
      "Muestra una cuenta atrás, pausa y continúa cuando lo necesites."
    ],
    "developerTag": "CREA TU HERRAMIENTA",
    "developerTitle": "Tu tarea. Tu próxima herramienta.",
    "developerText": "Los plugins del autor ilustran el contrato con Backpack. Se preparan instrucciones detalladas y ejemplos funcionales a partir del temporizador.",
    "developerSteps": [
      [
        "Comandos y ciclo de vida",
        "El SDK conecta inicio, parada y comandos con Backpack. Se pueden asignar atajos a los comandos."
      ],
      [
        "Ajustes e interfaz",
        "El manifiesto describe ajustes; Backpack crea controles y paneles superpuestos. Los colores son parámetros explícitos, no una API de temas automática garantizada."
      ],
      [
        "Localización y ejemplos",
        "Las etiquetas localizadas y Context.Locale admiten los idiomas de la aplicación. La ayuda explica el temporizador, compilación, empaquetado e importación .bppkg."
      ]
    ],
    "docsNotice": "F1 → Plugins explica contrato, compilación e importación. Aún faltan enlaces públicos a documentación SDK y código de ejemplos.",
    "licenseNote": "Se están actualizando los términos del SDK para permitir crear libremente tus propios plugins. Se preparan guías detalladas, paso a paso, de compilación e importación y ejemplos funcionales.",
    "applicationTag": "PARTE DE LA APLICACIÓN",
    "applicationTitle": "Una configuración que sigue siendo tuya.",
    "applicationText": "Apariencia, perfiles guardados, privacidad de entrada y gestión de plugins son infraestructura, no plugins separados.",
    "privacyTitle": "La privacidad de entrada, por separado.",
    "privacyText": "La versión verificada oculta teclas al detectar entrada protegida. Hay limitaciones y una carrera conocida de notificaciones de foco: oculta manualmente antes de datos sensibles. Confiar en código de plugins es otra decisión.",
    "workflowTitle": "Demostración del nuevo modelo",
    "workflowNote": "Recorrido simulado de la interfaz prevista. Importación y confianza están implementadas en preview.3; la escena no es una captura ni prueba de que todos los plugins estén listos.",
    "workflowSteps": [
      "Empezar con teclas y ratón integrados",
      "Abrir los plugins",
      "Elegir el temporizador del autor",
      "Confirmar confianza y activar",
      "Aparece la herramienta adicional"
    ],
    "coreTab": "Integrado",
    "pluginsTab": "Plugins",
    "enable": "Activar",
    "trust": "Activa solo código de fuentes fiables. Un proceso separado no es un entorno aislado seguro.",
    "enabled": "Activado",
    "start": "Iniciar",
    "pause": "Pausa",
    "resume": "Continuar",
    "reset": "Restablecer",
    "finished": "Finalizado",
    "timerCaption": "Cuenta atrás simulada · pausa · continuación",
    "layoutCaption": "Cambio simulado del idioma de entrada",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "¿Qué incluye la aplicación?",
        "El núcleo objetivo contiene teclas y ratón. Perfiles, aspecto, privacidad y gestión de plugins son funciones de la aplicación. preview.3 conserva herramientas anteriores durante la migración; solo el temporizador está verificado como plugin independiente."
      ],
      [
        "¿Se pueden descargar Backpack y los plugins?",
        "Backpack 0.4.0-preview.3 está verificado, sin firmar y sin publicar. Un paquete local no equivale a descarga pública. El catálogo distingue planes, código y ejemplo empaquetado."
      ],
      [
        "¿Cómo se instalan las extensiones?",
        "Plugins → Importar plugin → .bppkg. Se valida y extrae sin ejecutar código ni conceder confianza. Los plugins nuevos o actualizados quedan desactivados; la primera activación requiere confirmación."
      ],
      [
        "¿Están aislados de forma segura?",
        "Usa fuentes fiables. Los procesos separados tienen tus permisos y ayudan ante fallos, pero no son una zona segura aislada. Las capacidades limitan la interfaz Backpack, no todas las acciones del código en el equipo."
      ],
      [
        "¿Cómo empiezo una herramienta?",
        "F1 → Plugins explica contrato, ciclo de vida, comandos, ajustes, idiomas y compilación del temporizador. Se preparan guías detalladas y ejemplos funcionales; los nuevos términos SDK permitirán crear plugins propios libremente. Los enlaces públicos llegarán después."
      ],
      [
        "¿La privacidad protege todos los campos?",
        "No se promete detección universal. Las entradas protegidas o desconocidas pueden ocultarse, pero la detección asíncrona del foco tiene límites. Oculta antes de datos sensibles. La confianza de plugins es independiente."
      ],
      [
        "¿Qué sistemas y licencia se aplican?",
        "Paquete verificado para Windows 10/11 x64 con .NET. Backpack usa PolyForm Strict 1.0.0 y no es open source. Los términos SDK/ejemplos se están actualizando."
      ]
    ]
  },
  "it": {
    "title": "Backpack — Azioni chiare. Strumenti tuoi.",
    "intro": "Rendi più semplici da seguire presentazioni, lezioni e registrazioni. Mostra ogni tasto e clic.",
    "extension": "Aggiungi strumenti come plugin indipendenti o scopri come crearne uno per le tue esigenze.",
    "coreTag": "INTEGRATO IN BACKPACK",
    "coreTitle": "Parti dall’essenziale.",
    "coreText": "Tasti e visualizzazione del mouse sono i due strumenti al centro di Backpack.",
    "pluginsTag": "PLUGIN DELL’AUTORE",
    "pluginsTitle": "Gli strumenti per la tua storia.",
    "pluginsText": "Le estensioni dell’autore di Backpack si aggiungono indipendentemente e offrono esempi per futuri plugin personali.",
    "transitionNote": "Questo è il catalogo previsto. Nell’anteprima verificata, disegno e disposizione restano integrati; anche gli strumenti di ingrandimento passano ai plugin. Le scene illustrano la direzione, non una migrazione completata.",
    "builtin": "Integrato",
    "authorPlugin": "Plugin dell’autore",
    "notPublished": "Pacchetto pubblico non ancora disponibile",
    "byAuthor": "Autore",
    "statuses": {
      "planned": "Plugin previsto",
      "source": "Plugin nel codice sorgente",
      "verified": "Plugin verificato nel pacchetto"
    },
    "availability": {
      "planned": "Lo strumento funziona in preview.3, ma il plugin indipendente non è ancora implementato né impacchettato.",
      "source": "Un esempio SDK 1.1 esiste nel codice attuale. Questo plugin indipendente non è verificato in preview.3.",
      "verified": "Timer 1.0.0 / SDK 1.0 è verificato in Samples di preview.3. Il sorgente attuale è più recente; nessun download pubblico."
    },
    "pluginNames": [
      "Disegno",
      "Indicatore di disposizione",
      "Lente",
      "Messa a fuoco",
      "Lente del cursore",
      "Timer"
    ],
    "pluginBenefits": [
      "Note, forme e testo temporanei durante la spiegazione.",
      "Mostra il cambio di lingua di inserimento.",
      "Sposta una vista ingrandita separata dell’area scelta.",
      "Oscura i dintorni lasciando chiara l’area alle dimensioni originali.",
      "Ingrandisci i dettagli con una lente che segue il puntatore.",
      "Mostra un conto alla rovescia, poi sospendilo e riprendilo."
    ],
    "developerTag": "CREA IL TUO STRUMENTO",
    "developerTitle": "La tua esigenza. Il tuo prossimo strumento.",
    "developerText": "I plugin dell’autore illustrano il contratto con Backpack. Si preparano istruzioni dettagliate ed esempi funzionanti a partire dal timer.",
    "developerSteps": [
      [
        "Comandi e ciclo di vita",
        "Il SDK collega avvio, arresto e comandi a Backpack. Ai comandi si possono assegnare scorciatoie."
      ],
      [
        "Impostazioni e interfaccia",
        "Il manifesto descrive le impostazioni; Backpack crea controlli e pannelli sovrapposti. I colori sono parametri espliciti, non un’API automatica dei temi garantita."
      ],
      [
        "Localizzazione ed esempi",
        "Etichette localizzate e Context.Locale supportano le lingue dell’applicazione. La guida spiega timer, compilazione, pacchetti e importazione .bppkg."
      ]
    ],
    "docsNotice": "F1 → Plugin spiega contratto, compilazione e importazione. I collegamenti pubblici a documentazione SDK e sorgenti degli esempi non sono ancora configurati.",
    "licenseNote": "Le condizioni del SDK vengono aggiornate per consentire di creare liberamente i propri plugin. Si preparano guide dettagliate, passo dopo passo, per compilazione e importazione ed esempi funzionanti.",
    "applicationTag": "PARTE DELL’APPLICAZIONE",
    "applicationTitle": "Una configurazione che resta tua.",
    "applicationText": "Aspetto, profili salvati, privacy dell’inserimento e gestione dei plugin sono infrastruttura, non plugin separati.",
    "privacyTitle": "Privacy dell’inserimento, a parte.",
    "privacyText": "L’anteprima verificata nasconde i tasti quando rileva un inserimento protetto. Ci sono limiti e una nota condizione di competizione nelle notifiche del focus: nascondi manualmente prima dei dati sensibili. La fiducia nel codice è distinta.",
    "workflowTitle": "Dimostrazione del nuovo modello",
    "workflowNote": "Percorso simulato dell’interfaccia prevista. Importazione e fiducia sono implementate in preview.3; la scena non è uno screenshot né prova che tutti i plugin siano pronti.",
    "workflowSteps": [
      "Parti da tasti e mouse integrati",
      "Apri la sezione plugin",
      "Scegli il timer dell’autore",
      "Conferma la fiducia e attiva",
      "Appare lo strumento aggiuntivo"
    ],
    "coreTab": "Integrato",
    "pluginsTab": "Plugin",
    "enable": "Attiva",
    "trust": "Attiva solo codice da fonti fidate. Un processo separato non è una sandbox.",
    "enabled": "Attivato",
    "start": "Avvia",
    "pause": "Pausa",
    "resume": "Riprendi",
    "reset": "Reimposta",
    "finished": "Terminato",
    "timerCaption": "Conto alla rovescia simulato · pausa · ripresa",
    "layoutCaption": "Cambio simulato della lingua di inserimento",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Cosa comprende l’applicazione?",
        "Il nucleo previsto contiene tasti e mouse. Profili, aspetto, privacy e gestione dei plugin appartengono all’applicazione. preview.3 mantiene strumenti precedenti durante la migrazione; solo il timer è verificato come plugin indipendente."
      ],
      [
        "Si possono scaricare Backpack e i plugin?",
        "Backpack 0.4.0-preview.3 è verificato, non firmato e non pubblicato. Un pacchetto locale non è un download pubblico. Il catalogo distingue piani, sorgenti ed esempio impacchettato."
      ],
      [
        "Come si installano le estensioni?",
        "Plugin → Importa plugin → .bppkg. L’importazione verifica ed estrae senza eseguire codice o concedere fiducia. Plugin nuovi o aggiornati sono disattivati; la prima attivazione richiede conferma."
      ],
      [
        "I plugin sono isolati in sicurezza?",
        "Usa fonti fidate. I processi separati hanno i tuoi diritti e aiutano a gestire i guasti, ma non sono sandbox. Le capacità del manifesto limitano l’interfaccia Backpack, non tutte le azioni nel sistema."
      ],
      [
        "Come inizio uno strumento?",
        "F1 → Plugin spiega contratto, ciclo di vita, comandi, impostazioni, lingue e compilazione del timer. Si preparano guide dettagliate ed esempi funzionanti; le nuove condizioni SDK permetteranno di creare liberamente propri plugin. I collegamenti pubblici seguiranno."
      ],
      [
        "La privacy protegge ogni campo?",
        "Nessun rilevamento universale promesso. Inserimenti protetti o sconosciuti possono essere nascosti, ma il rilevamento asincrono del focus ha limiti. Nascondi prima dei dati sensibili. La fiducia nei plugin è separata."
      ],
      [
        "Quali sistemi e licenza?",
        "Pacchetto verificato per Windows 10/11 x64 con .NET. Backpack usa PolyForm Strict 1.0.0 e non è open source. Le condizioni SDK/esempi vengono aggiornate."
      ]
    ]
  },
  "pt": {
    "title": "Backpack — Ações claras. Ferramentas suas.",
    "intro": "Torne apresentações, aulas e gravações mais fáceis de seguir. Mostre cada tecla e clique.",
    "extension": "Adicione ferramentas como plugins independentes ou descubra como criar a sua.",
    "coreTag": "INTEGRADO NO BACKPACK",
    "coreTitle": "Comece pelo essencial.",
    "coreText": "Teclas e visualização do rato são as duas ferramentas no núcleo do Backpack.",
    "pluginsTag": "PLUGINS DO AUTOR",
    "pluginsTitle": "As ferramentas certas para a sua história.",
    "pluginsText": "As extensões do autor do Backpack são adicionadas de forma independente e servem de exemplos para futuros plugins próprios.",
    "transitionNote": "Este é o catálogo previsto. Na versão verificada, desenho e esquema ainda estão integrados; as ferramentas de ampliação também passam para plugins. As cenas ilustram esse rumo, não uma migração concluída.",
    "builtin": "Integrado",
    "authorPlugin": "Plugin do autor",
    "notPublished": "Pacote público ainda indisponível",
    "byAuthor": "Autor",
    "statuses": {
      "planned": "Plugin planeado",
      "source": "Plugin no código-fonte",
      "verified": "Plugin verificado no pacote"
    },
    "availability": {
      "planned": "A ferramenta funciona em preview.3, mas o plugin independente ainda não está implementado nem empacotado.",
      "source": "Existe um exemplo SDK 1.1 no código atual. Este plugin independente não foi verificado em preview.3.",
      "verified": "Temporizador 1.0.0 / SDK 1.0 verificado em Samples de preview.3. O código atual é mais recente; não há transferência pública."
    },
    "pluginNames": [
      "Desenho",
      "Indicador de esquema",
      "Lupa",
      "Destaque",
      "Lupa do cursor",
      "Temporizador"
    ],
    "pluginBenefits": [
      "Notas, formas e texto temporários durante a explicação.",
      "Mostre a mudança do idioma de introdução.",
      "Mova uma vista ampliada independente da área escolhida.",
      "Escureça a envolvente mantendo a área clara no tamanho original.",
      "Aproxime detalhes com uma lupa que segue o ponteiro.",
      "Mostre uma contagem decrescente, pause e retome."
    ],
    "developerTag": "CRIE A SUA FERRAMENTA",
    "developerTitle": "A sua tarefa. A sua próxima ferramenta.",
    "developerText": "Os plugins do autor demonstram o contrato com o Backpack. Estão a ser preparadas instruções detalhadas e exemplos funcionais a partir do temporizador.",
    "developerSteps": [
      [
        "Comandos e ciclo de vida",
        "O SDK liga início, paragem e comandos ao Backpack. Podem atribuir-se atalhos aos comandos."
      ],
      [
        "Definições e interface",
        "O manifesto descreve definições; o Backpack cria controlos e painéis sobrepostos. As cores são parâmetros explícitos, não uma API de temas automática garantida."
      ],
      [
        "Localização e exemplos",
        "Rótulos localizados e Context.Locale suportam os idiomas da aplicação. A ajuda explica temporizador, compilação, empacotamento e importação .bppkg."
      ]
    ],
    "docsNotice": "F1 → Plugins explica contrato, compilação e importação. Ainda não há ligações públicas para documentação SDK ou código dos exemplos.",
    "licenseNote": "As condições do SDK estão a ser atualizadas para permitir criar livremente os seus plugins. Estão a ser preparados guias detalhados, passo a passo, para compilação e importação e exemplos funcionais.",
    "applicationTag": "PARTE DA APLICAÇÃO",
    "applicationTitle": "Uma configuração que continua sua.",
    "applicationText": "Aspeto, perfis guardados, privacidade de introdução e gestão de plugins são infraestrutura, não plugins separados.",
    "privacyTitle": "Privacidade de introdução, à parte.",
    "privacyText": "A versão verificada oculta teclas ao detetar introdução protegida. Há limites e uma condição de corrida conhecida nas notificações de foco: oculte manualmente antes de dados sensíveis. Confiar no código é uma decisão distinta.",
    "workflowTitle": "Demonstração do novo modelo",
    "workflowNote": "Percurso simulado da interface prevista. Importação e confiança existem em preview.3; a cena não é uma captura nem prova de que todos os plugins estão prontos.",
    "workflowSteps": [
      "Começar com teclas e rato integrados",
      "Abrir os plugins",
      "Escolher o temporizador do autor",
      "Confirmar confiança e ativar",
      "Surge a ferramenta adicional"
    ],
    "coreTab": "Integrado",
    "pluginsTab": "Plugins",
    "enable": "Ativar",
    "trust": "Ative apenas código de fontes fiáveis. Um processo separado não é uma sandbox.",
    "enabled": "Ativado",
    "start": "Iniciar",
    "pause": "Pausa",
    "resume": "Retomar",
    "reset": "Repor",
    "finished": "Terminado",
    "timerCaption": "Contagem simulada · pausa · retomar",
    "layoutCaption": "Mudança simulada do idioma de introdução",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "O que inclui a aplicação?",
        "O núcleo previsto contém teclas e rato. Perfis, aspeto, privacidade e gestão de plugins pertencem à aplicação. preview.3 mantém ferramentas anteriores durante a migração; apenas o temporizador está verificado como plugin independente."
      ],
      [
        "É possível transferir o Backpack e plugins?",
        "Backpack 0.4.0-preview.3 está verificado, sem assinatura e não publicado. Um pacote local não equivale a transferência pública. O catálogo distingue planos, código e exemplo empacotado."
      ],
      [
        "Como instalar extensões?",
        "Plugins → Importar plugin → .bppkg. A importação valida e extrai sem executar código nem conceder confiança. Plugins novos ou atualizados ficam desativados; a primeira ativação exige confirmação."
      ],
      [
        "Os plugins estão isolados em segurança?",
        "Use fontes fiáveis. Os processos separados têm os seus direitos e ajudam a gerir falhas, mas não são uma sandbox. As capacidades limitam a interface Backpack, não todas as ações do código no computador."
      ],
      [
        "Como começar uma ferramenta?",
        "F1 → Plugins explica contrato, ciclo de vida, comandos, definições, idiomas e compilação do temporizador. Guias detalhados e exemplos funcionais estão em preparação; as novas condições SDK permitirão criar livremente os seus plugins. Seguir-se-ão ligações públicas."
      ],
      [
        "A privacidade protege todos os campos?",
        "Não há promessa de deteção universal. Introduções protegidas ou desconhecidas podem ocultar-se, mas a deteção assíncrona do foco tem limites. Oculte antes de dados sensíveis. A confiança nos plugins é separada."
      ],
      [
        "Que sistemas e licença se aplicam?",
        "Pacote verificado para Windows 10/11 x64 com .NET. O Backpack usa PolyForm Strict 1.0.0 e não é open source. As condições SDK/exemplos estão a ser atualizadas."
      ]
    ]
  },
  "nl": {
    "title": "Backpack — Duidelijke acties. Eigen hulpmiddelen.",
    "intro": "Maak presentaties, lessen en schermopnamen makkelijker te volgen. Toon elke toets en klik.",
    "extension": "Voeg hulpmiddelen toe als onafhankelijke plugins of ontdek hoe u er zelf een kunt maken.",
    "coreTag": "INGEBOUWD IN BACKPACK",
    "coreTitle": "Begin met de basis.",
    "coreText": "Toetsen en muisvisualisatie zijn de twee hulpmiddelen in de kern van Backpack.",
    "pluginsTag": "PLUGINS VAN DE AUTEUR",
    "pluginsTitle": "De hulpmiddelen voor uw verhaal.",
    "pluginsText": "Uitbreidingen van de Backpack-auteur zijn apart toe te voegen en dienen als voorbeelden voor toekomstige eigen plugins.",
    "transitionNote": "Dit is de beoogde catalogus. In de geverifieerde versie zijn tekenen en indeling nog ingebouwd; vergrotingshulpmiddelen verhuizen ook naar plugins. De scènes tonen de richting, geen voltooide migratie.",
    "builtin": "Ingebouwd",
    "authorPlugin": "Plugin van de auteur",
    "notPublished": "Openbaar pakket nog niet beschikbaar",
    "byAuthor": "Auteur",
    "statuses": {
      "planned": "Plugin gepland",
      "source": "Plugin in broncode",
      "verified": "Plugin in pakket geverifieerd"
    },
    "availability": {
      "planned": "Het hulpmiddel werkt in preview.3; de onafhankelijke plugin is nog niet geïmplementeerd of verpakt.",
      "source": "Een SDK-1.1-voorbeeld bestaat in de huidige broncode. Deze onafhankelijke plugin is niet geverifieerd in preview.3.",
      "verified": "Timer 1.0.0 / SDK 1.0 is geverifieerd in Samples van preview.3. De huidige broncode is nieuwer; geen openbare download."
    },
    "pluginNames": [
      "Tekenen",
      "Indelingsindicator",
      "Vergrootglas",
      "Focus",
      "Cursorvergrootglas",
      "Timer"
    ],
    "pluginBenefits": [
      "Tijdelijke notities, vormen en tekst tijdens uw uitleg.",
      "Maak een wisseling van invoertaal zichtbaar.",
      "Verplaats een afzonderlijk vergroot beeld van het gekozen gebied.",
      "Dim de omgeving en houd het gebied helder op de oorspronkelijke grootte.",
      "Vergroot details met een vergrootglas dat de aanwijzer volgt.",
      "Toon een aftelling, pauzeer en hervat wanneer nodig."
    ],
    "developerTag": "MAAK UW EIGEN HULPMIDDEL",
    "developerTitle": "Uw taak. Uw volgende hulpmiddel.",
    "developerText": "De auteursplugins tonen het contract met Backpack. Gedetailleerde instructies en werkende voorbeelden worden voorbereid op basis van de timer.",
    "developerSteps": [
      [
        "Opdrachten en levenscyclus",
        "De SDK verbindt starten, stoppen en opdrachten met Backpack. Opdrachten kunnen sneltoetsen krijgen."
      ],
      [
        "Instellingen en interface",
        "Het manifest beschrijft instellingen; Backpack tekent bedieningselementen en overlaypanelen. Kleuren zijn expliciete parameters, geen gegarandeerde automatische thema-API."
      ],
      [
        "Lokalisatie en voorbeelden",
        "Vertaalde manifestlabels en Context.Locale ondersteunen de app-talen. De help behandelt timer, bouwen, verpakken en .bppkg-import."
      ]
    ],
    "docsNotice": "F1 → Plugins bevat contract-, bouw- en importinstructies. Openbare links naar SDK-documentatie en voorbeeldbronnen ontbreken nog.",
    "licenseNote": "De SDK-voorwaarden worden bijgewerkt om het vrij maken van eigen plugins mogelijk te maken. Gedetailleerde stapsgewijze bouw- en importinstructies en werkende voorbeelden worden voorbereid.",
    "applicationTag": "ONDERDEEL VAN DE TOEPASSING",
    "applicationTitle": "Instellingen die van u blijven.",
    "applicationText": "Weergave, opgeslagen profielen, invoerprivacy en pluginbeheer zijn infrastructuur, geen losse plugins.",
    "privacyTitle": "Invoerprivacy, afzonderlijk.",
    "privacyText": "De geverifieerde versie verbergt toetsen bij gedetecteerde beschermde invoer. Detectie kent grenzen en een bekende race met focusmeldingen: verberg handmatig vóór gevoelige invoer. Vertrouwen in code is een aparte keuze.",
    "workflowTitle": "Demonstratie van het nieuwe model",
    "workflowNote": "Gesimuleerde rondleiding door de beoogde interface. Import en vertrouwen bestaan in preview.3; de scène is geen schermafbeelding of bewijs dat alle plugins klaar zijn.",
    "workflowSteps": [
      "Begin met ingebouwde toetsen en muis",
      "Open de pluginsectie",
      "Kies de timer van de auteur",
      "Bevestig vertrouwen en schakel in",
      "Het extra hulpmiddel verschijnt"
    ],
    "coreTab": "Ingebouwd",
    "pluginsTab": "Plugins",
    "enable": "Inschakelen",
    "trust": "Schakel alleen code van vertrouwde bronnen in. Een apart proces is geen sandbox.",
    "enabled": "Ingeschakeld",
    "start": "Start",
    "pause": "Pauze",
    "resume": "Hervatten",
    "reset": "Herstellen",
    "finished": "Voltooid",
    "timerCaption": "Gesimuleerde aftelling · pauze · hervatten",
    "layoutCaption": "Gesimuleerde wisseling van invoertaal",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Wat hoort bij de toepassing?",
        "De beoogde kern bevat toetsen en muis. Profielen, weergave, privacy en pluginbeheer horen bij de app. preview.3 bevat tijdens de migratie nog oude hulpmiddelen; alleen de timer is als onafhankelijke plugin geverifieerd."
      ],
      [
        "Kan ik Backpack en plugins downloaden?",
        "Backpack 0.4.0-preview.3 is geverifieerd, ongetekend en ongepubliceerd. Een lokaal pakket is geen openbare download. De catalogus onderscheidt plannen, broncode en een verpakt voorbeeld."
      ],
      [
        "Hoe installeer ik uitbreidingen?",
        "Plugins → Plugin importeren → .bppkg. Import controleert en pakt uit zonder code te starten of vertrouwen te geven. Nieuwe en bijgewerkte plugins staan uit; eerste inschakeling vraagt bevestiging."
      ],
      [
        "Zijn plugins veilig geïsoleerd?",
        "Gebruik vertrouwde bronnen. Aparte processen hebben uw rechten en helpen fouten afhandelen, maar zijn geen sandbox. Manifestmogelijkheden beperken de Backpack-interface, niet alle acties op de computer."
      ],
      [
        "Hoe begin ik een hulpmiddel?",
        "F1 → Plugins behandelt contract, levenscyclus, opdrachten, instellingen, talen en timerbouw. Gedetailleerde gidsen en werkende voorbeelden worden voorbereid; de nieuwe SDK-voorwaarden zullen het vrij maken van eigen plugins mogelijk maken. Openbare links volgen."
      ],
      [
        "Beschermt privacy elk veld?",
        "Geen universele detectie beloofd. Beschermde of onbekende invoer kan verborgen worden, maar asynchrone focusdetectie heeft grenzen. Verberg vóór gevoelige invoer. Pluginvertrouwen is een afzonderlijk besluit."
      ],
      [
        "Welke systemen en licentie?",
        "Geverifieerd pakket voor Windows 10/11 x64 met .NET. Backpack gebruikt PolyForm Strict 1.0.0 en is geen open source. SDK-/voorbeeldvoorwaarden worden bijgewerkt."
      ]
    ]
  },
  "pl": {
    "title": "Backpack — Czytelne działania. Własne narzędzia.",
    "intro": "Ułatw śledzenie prezentacji, lekcji i nagrań ekranu. Pokazuj każdy klawisz i kliknięcie.",
    "extension": "Dodawaj narzędzia jako niezależne wtyczki lub poznaj sposób tworzenia własnych.",
    "coreTag": "WBUDOWANE W BACKPACK",
    "coreTitle": "Zacznij od podstaw.",
    "coreText": "Klawisze i wizualizacja myszy to dwa narzędzia tworzące rdzeń Backpack.",
    "pluginsTag": "WTYCZKI AUTORA",
    "pluginsTitle": "Narzędzia potrzebne Twojej historii.",
    "pluginsText": "Rozszerzenia autora Backpack dodaje się niezależnie. Są też przykładami dla przyszłych własnych wtyczek.",
    "transitionNote": "To katalog docelowy. W zweryfikowanej wersji rysowanie i układ nadal są wbudowane; narzędzia powiększania również przechodzą do wtyczek. Sceny pokazują kierunek, nie zakończoną migrację.",
    "builtin": "Wbudowane",
    "authorPlugin": "Wtyczka autora",
    "notPublished": "Pakiet publiczny jeszcze niedostępny",
    "byAuthor": "Autor",
    "statuses": {
      "planned": "Wtyczka planowana",
      "source": "Wtyczka w kodzie źródłowym",
      "verified": "Wtyczka zweryfikowana w pakiecie"
    },
    "availability": {
      "planned": "Narzędzie działa w preview.3, lecz niezależna wtyczka nie jest jeszcze zaimplementowana ani spakowana.",
      "source": "Przykład SDK 1.1 istnieje w aktualnym kodzie. Ta niezależna wtyczka nie została zweryfikowana w preview.3.",
      "verified": "Minutnik 1.0.0 / SDK 1.0 zweryfikowano w Samples pakietu preview.3. Aktualny kod jest nowszy; brak publicznego pobierania."
    },
    "pluginNames": [
      "Rysowanie",
      "Wskaźnik układu",
      "Lupa",
      "Skupienie uwagi",
      "Lupa przy kursorze",
      "Minutnik"
    ],
    "pluginBenefits": [
      "Tymczasowe notatki, kształty i tekst podczas wyjaśniania.",
      "Pokaż zmianę języka wprowadzania.",
      "Przesuwaj osobny powiększony widok wybranego obszaru.",
      "Przyciemnij otoczenie, pozostawiając obszar czytelny w pierwotnej skali.",
      "Przybliżaj szczegóły lupą podążającą za kursorem.",
      "Wyświetl odliczanie, zatrzymaj je i wznów."
    ],
    "developerTag": "STWÓRZ WŁASNE NARZĘDZIE",
    "developerTitle": "Twoje zadanie. Twoje następne narzędzie.",
    "developerText": "Wtyczki autora pokazują kontrakt z Backpack. Na podstawie minutnika powstają szczegółowe instrukcje i działające przykłady.",
    "developerSteps": [
      [
        "Polecenia i cykl życia",
        "SDK łączy uruchamianie, zatrzymywanie i polecenia z Backpack. Poleceniom można przypisać skróty."
      ],
      [
        "Ustawienia i interfejs",
        "Manifest opisuje ustawienia; Backpack tworzy kontrolki i panele nakładek. Kolory są jawnymi parametrami, bez obietnicy automatycznego API motywów."
      ],
      [
        "Lokalizacja i przykłady",
        "Tłumaczenia manifestu i Context.Locale obsługują języki aplikacji. Pomoc opisuje minutnik, budowanie, pakowanie i import .bppkg."
      ]
    ],
    "docsNotice": "F1 → Wtyczki opisuje kontrakt, budowanie i import. Publiczne łącza do dokumentacji SDK i kodu przykładów nie są jeszcze skonfigurowane.",
    "licenseNote": "Warunki SDK są aktualizowane, aby umożliwić swobodne tworzenie własnych wtyczek. Powstają szczegółowe instrukcje budowania i importu krok po kroku oraz działające przykłady.",
    "applicationTag": "CZĘŚĆ APLIKACJI",
    "applicationTitle": "Ustawienia, które pozostają Twoje.",
    "applicationText": "Wygląd, zapisane profile, prywatność wpisywania i zarządzanie wtyczkami są infrastrukturą aplikacji, nie osobnymi wtyczkami.",
    "privacyTitle": "Prywatność wpisywania — osobno.",
    "privacyText": "Zweryfikowana wersja ukrywa klawisze po wykryciu chronionego wpisywania. Detekcja ma ograniczenia i znany wyścig powiadomień o fokusie: ukrywaj nakładkę ręcznie przed poufnymi danymi. Zaufanie do kodu to oddzielna decyzja.",
    "workflowTitle": "Demonstracja nowego modelu",
    "workflowNote": "Symulacja docelowego interfejsu. Import i zaufanie działają w preview.3; scena nie jest zrzutem ani dowodem gotowości wszystkich wtyczek.",
    "workflowSteps": [
      "Zacznij od wbudowanych klawiszy i myszy",
      "Otwórz sekcję wtyczek",
      "Wybierz minutnik autora",
      "Potwierdź zaufanie i włącz",
      "Pojawia się dodatkowe narzędzie"
    ],
    "coreTab": "Wbudowane",
    "pluginsTab": "Wtyczki",
    "enable": "Włącz",
    "trust": "Włączaj wyłącznie kod z zaufanych źródeł. Osobny proces nie jest bezpieczną piaskownicą.",
    "enabled": "Włączono",
    "start": "Start",
    "pause": "Pauza",
    "resume": "Wznów",
    "reset": "Resetuj",
    "finished": "Zakończono",
    "timerCaption": "Symulowane odliczanie · pauza · wznowienie",
    "layoutCaption": "Symulowana zmiana języka wprowadzania",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Co zawiera aplikacja?",
        "Docelowy rdzeń to klawisze i mysz. Profile, wygląd, prywatność i zarządzanie wtyczkami należą do aplikacji. preview.3 zachowuje starsze narzędzia na czas migracji; tylko minutnik zweryfikowano jako niezależną wtyczkę."
      ],
      [
        "Czy mogę pobrać Backpack i wtyczki?",
        "Backpack 0.4.0-preview.3 jest zweryfikowany, niepodpisany i nieopublikowany. Lokalny pakiet nie oznacza publicznego pobierania. Katalog odróżnia plany, kod i spakowany przykład."
      ],
      [
        "Jak instaluje się rozszerzenia?",
        "Wtyczki → Importuj wtyczkę → .bppkg. Import sprawdza i rozpakowuje bez wykonywania kodu lub nadawania zaufania. Nowe i aktualizowane wtyczki są wyłączone; pierwsze włączenie wymaga potwierdzenia."
      ],
      [
        "Czy wtyczki są bezpiecznie izolowane?",
        "Korzystaj z zaufanych źródeł. Osobne procesy mają Twoje uprawnienia i pomagają obsługiwać awarie, ale nie są piaskownicą. Możliwości manifestu ograniczają interfejs Backpack, nie wszystkie działania kodu w systemie."
      ],
      [
        "Od czego zacząć własne narzędzie?",
        "F1 → Wtyczki opisuje kontrakt, cykl życia, polecenia, ustawienia, języki i budowanie minutnika. Powstają szczegółowe poradniki i działające przykłady; nowe warunki SDK pozwolą swobodnie tworzyć własne wtyczki. Publiczne łącza pojawią się później."
      ],
      [
        "Czy prywatność chroni każde pole?",
        "Brak obietnicy uniwersalnej detekcji. Chronione lub nieznane wpisywanie może być ukrywane, ale asynchroniczna detekcja fokusu ma ograniczenia. Ukrywaj nakładkę przed poufnymi danymi. Zaufanie do wtyczek jest osobną decyzją."
      ],
      [
        "Jakie systemy i licencja?",
        "Zweryfikowany pakiet dla Windows 10/11 x64 z .NET. Backpack używa PolyForm Strict 1.0.0 i nie jest open source. Warunki SDK/przykładów są aktualizowane."
      ]
    ]
  },
  "cs": {
    "title": "Backpack — Jasné akce. Vlastní nástroje.",
    "intro": "Zpřehledněte prezentace, výuku a záznamy obrazovky. Ukažte každou klávesu a kliknutí.",
    "extension": "Přidejte nástroje jako nezávislé doplňky nebo objevte tvorbu vlastního.",
    "coreTag": "VESTAVĚNO DO BACKPACK",
    "coreTitle": "Začněte tím podstatným.",
    "coreText": "Klávesy a vizualizace myši jsou dva nástroje tvořící jádro Backpack.",
    "pluginsTag": "DOPLŇKY AUTORA",
    "pluginsTitle": "Nástroje pro váš příběh.",
    "pluginsText": "Rozšíření autora Backpack se přidávají samostatně a slouží jako příklady pro budoucí vlastní doplňky.",
    "transitionNote": "Toto je cílový katalog. V ověřené verzi jsou kreslení a rozložení stále vestavěné; nástroje zvětšení se také převádějí na doplňky. Scény ukazují směr, nikoli dokončený převod.",
    "builtin": "Vestavěno",
    "authorPlugin": "Doplněk autora",
    "notPublished": "Veřejný balíček zatím není dostupný",
    "byAuthor": "Autor",
    "statuses": {
      "planned": "Doplněk plánován",
      "source": "Doplněk ve zdrojovém kódu",
      "verified": "Doplněk ověřen v balíčku"
    },
    "availability": {
      "planned": "Nástroj funguje v preview.3, ale samostatný doplněk zatím není implementován ani zabalen.",
      "source": "Příklad SDK 1.1 existuje v aktuálním kódu. Tento samostatný doplněk není ověřen v preview.3.",
      "verified": "Časovač 1.0.0 / SDK 1.0 je ověřen ve složce Samples balíčku preview.3. Aktuální kód je novější; veřejné stažení chybí."
    },
    "pluginNames": [
      "Kreslení",
      "Ukazatel rozložení",
      "Lupa",
      "Zaměření",
      "Lupa u kurzoru",
      "Časovač"
    ],
    "pluginBenefits": [
      "Dočasné poznámky, tvary a text při vysvětlování.",
      "Zviditelněte změnu jazyka zadávání.",
      "Přesouvejte samostatný zvětšený pohled na vybranou oblast.",
      "Ztmavte okolí a ponechte oblast čitelnou v původní velikosti.",
      "Přibližte detaily lupou sledující kurzor.",
      "Odpočítávejte na obrazovce, pozastavte a pokračujte."
    ],
    "developerTag": "VYTVOŘTE VLASTNÍ NÁSTROJ",
    "developerTitle": "Váš úkol. Váš další nástroj.",
    "developerText": "Doplňky autora ukazují kontrakt s Backpack. Podle časovače se připravují podrobné návody a funkční příklady.",
    "developerSteps": [
      [
        "Příkazy a životní cyklus",
        "SDK propojuje spuštění, zastavení a příkazy s Backpack. Příkazům lze přiřadit zkratky."
      ],
      [
        "Nastavení a rozhraní",
        "Manifest popisuje nastavení; Backpack vykresluje ovládací prvky a překryvné panely. Barvy jsou výslovné parametry, nikoli slíbené automatické API motivů."
      ],
      [
        "Lokalizace a příklady",
        "Přeložené popisky a Context.Locale podporují jazyky aplikace. Nápověda vysvětluje časovač, sestavení, balení a import .bppkg."
      ]
    ],
    "docsNotice": "F1 → Doplňky obsahuje kontrakt, sestavení a import. Veřejné odkazy na dokumentaci SDK a kód příkladů zatím nejsou nastavené.",
    "licenseNote": "Podmínky SDK se aktualizují, aby umožnily volnou tvorbu vlastních doplňků. Připravují se podrobné návody k sestavení a importu krok za krokem i funkční příklady.",
    "applicationTag": "SOUČÁST APLIKACE",
    "applicationTitle": "Nastavení, která zůstávají vaše.",
    "applicationText": "Vzhled, uložené profily, soukromí zadávání a správa doplňků jsou infrastruktura aplikace, nikoli samostatné doplňky.",
    "privacyTitle": "Soukromí zadávání samostatně.",
    "privacyText": "Ověřená verze skryje klávesy při zjištění chráněného zadávání. Detekce má limity a známý souběh oznámení fokusu: před citlivým zadáváním ručně skryjte zobrazení. Důvěra v kód je jiné rozhodnutí.",
    "workflowTitle": "Ukázka nového modelu",
    "workflowNote": "Simulovaný průchod cílovým rozhraním. Import a důvěra jsou implementovány v preview.3; scéna není snímkem ani důkazem připravenosti všech doplňků.",
    "workflowSteps": [
      "Začněte vestavěnými klávesami a myší",
      "Otevřete doplňky",
      "Vyberte časovač autora",
      "Potvrďte důvěru a zapněte",
      "Další nástroj se objeví"
    ],
    "coreTab": "Vestavěné",
    "pluginsTab": "Doplňky",
    "enable": "Zapnout",
    "trust": "Zapínejte jen kód z důvěryhodných zdrojů. Samostatný proces není bezpečná izolace.",
    "enabled": "Zapnuto",
    "start": "Start",
    "pause": "Pauza",
    "resume": "Pokračovat",
    "reset": "Obnovit",
    "finished": "Dokončeno",
    "timerCaption": "Simulované odpočítávání · pauza · pokračování",
    "layoutCaption": "Simulovaná změna jazyka zadávání",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Co je součástí aplikace?",
        "Cílové jádro obsahuje klávesy a myš. Profily, vzhled, soukromí a správa doplňků patří aplikaci. preview.3 během převodu ponechává starší nástroje; pouze časovač je ověřen jako samostatný doplněk."
      ],
      [
        "Lze Backpack a doplňky stáhnout?",
        "Backpack 0.4.0-preview.3 je ověřený, nepodepsaný a nezveřejněný. Místní balíček neznamená veřejné stažení. Katalog odlišuje plány, kód a zabalený příklad."
      ],
      [
        "Jak se rozšíření instalují?",
        "Doplňky → Importovat doplněk → .bppkg. Import kontroluje a rozbaluje bez spuštění kódu či udělení důvěry. Nové i aktualizované doplňky jsou vypnuté; první zapnutí vyžaduje potvrzení."
      ],
      [
        "Jsou doplňky bezpečně izolovány?",
        "Používejte důvěryhodné zdroje. Oddělené procesy mají vaše práva a pomáhají zvládat chyby, ale nejsou sandbox. Možnosti manifestu omezují rozhraní Backpack, ne všechny činnosti kódu v systému."
      ],
      [
        "Jak začít vlastní nástroj?",
        "F1 → Doplňky vysvětluje kontrakt, životní cyklus, příkazy, nastavení, jazyky a sestavení časovače. Připravují se podrobné návody a funkční příklady; nové podmínky SDK umožní volnou tvorbu vlastních doplňků. Veřejné odkazy budou doplněny."
      ],
      [
        "Chrání soukromí každé pole?",
        "Univerzální detekce se neslibuje. Chráněné či neznámé zadávání může být skryto, ale asynchronní detekce fokusu má limity. Před citlivým zadáváním ručně skryjte zobrazení. Důvěra v doplňky je samostatná."
      ],
      [
        "Jaké systémy a licence?",
        "Ověřený balíček pro Windows 10/11 x64 s .NET. Backpack používá PolyForm Strict 1.0.0 a není open source. Podmínky SDK/příkladů se aktualizují."
      ]
    ]
  },
  "hu": {
    "title": "Backpack — Világos műveletek. Saját eszközök.",
    "intro": "Tegye követhetőbbé a bemutatókat, órákat és képernyőfelvételeket. Mutasson minden billentyűt és kattintást.",
    "extension": "Adjon hozzá önálló bővítményeket, vagy ismerje meg saját eszköze elkészítését.",
    "coreTag": "A BACKPACK RÉSZE",
    "coreTitle": "Kezdje az alapokkal.",
    "coreText": "A billentyűk és az egér megjelenítése a Backpack két alapvető eszköze.",
    "pluginsTag": "A SZERZŐ BŐVÍTMÉNYEI",
    "pluginsTitle": "Eszközök a történetéhez.",
    "pluginsText": "A Backpack szerzőjének bővítményei külön adhatók hozzá, és példát mutatnak a jövőbeli saját eszközökhöz.",
    "transitionNote": "Ez a tervezett katalógus. Az ellenőrzött verzióban a rajzolás és a kiosztásjelző még beépített; a nagyítóeszközök is bővítményekké alakulnak. A jelenetek az irányt mutatják, nem a befejezett átállást.",
    "builtin": "Beépített",
    "authorPlugin": "A szerző bővítménye",
    "notPublished": "Nyilvános csomag még nincs",
    "byAuthor": "Szerző",
    "statuses": {
      "planned": "Tervezett bővítmény",
      "source": "Bővítmény a forráskódban",
      "verified": "Csomagban ellenőrzött bővítmény"
    },
    "availability": {
      "planned": "Az eszköz működik a preview.3-ban, de önálló bővítménye még nincs megvalósítva vagy csomagolva.",
      "source": "SDK 1.1 példa található a jelenlegi forrásban. Ezt az önálló bővítményt nem ellenőrizték a preview.3-ban.",
      "verified": "Az Időzítő 1.0.0 / SDK 1.0 ellenőrzött a preview.3 Samples mappájában. A jelenlegi forrás újabb; nyilvános letöltés nincs."
    },
    "pluginNames": [
      "Rajzolás",
      "Kiosztásjelző",
      "Nagyító",
      "Kiemelés",
      "Kurzornagyító",
      "Időzítő"
    ],
    "pluginBenefits": [
      "Ideiglenes jegyzetek, alakzatok és szöveg magyarázat közben.",
      "Tegye láthatóvá a beviteli nyelv változását.",
      "Mozgassa a kijelölt terület külön nagyított nézetét.",
      "Sötétítse a környezetet, a terület eredeti méretben maradjon tiszta.",
      "Nagyítsa a részleteket a mutatót követő nagyítóval.",
      "Mutasson visszaszámlálást, szüneteltesse és folytassa."
    ],
    "developerTag": "KÉSZÍTSEN SAJÁT ESZKÖZT",
    "developerTitle": "Saját feladat. Saját következő eszköz.",
    "developerText": "A szerző bővítményei megmutatják a Backpack szerződését. Az időzítő példájához részletes útmutatók készülnek.",
    "developerSteps": [
      [
        "Parancsok és életciklus",
        "Az SDK az indítást, leállítást és parancsokat kapcsolja a Backpackhez. A parancsokhoz gyorsbillentyűk rendelhetők."
      ],
      [
        "Beállítások és felület",
        "A manifeszt leírja a beállításokat; a Backpack rajzolja a vezérlőket és paneleket. A színek külön paraméterek, automatikus téma-API nincs ígérve."
      ],
      [
        "Honosítás és példák",
        "A honosított feliratok és a Context.Locale támogatják az alkalmazás nyelveit. A súgó leírja az időzítőt, fordítást, csomagolást és .bppkg-importot."
      ]
    ],
    "docsNotice": "Az F1 → Bővítmények leírja a szerződést, fordítást és importálást. Nyilvános SDK-dokumentáció és példaforrás-hivatkozás még nincs.",
    "licenseNote": "Az SDK feltételeit frissítik, hogy saját bővítményeket szabadon lehessen készíteni. Részletes, lépésenkénti fordítási és importálási útmutató, valamint működő példák készülnek.",
    "applicationTag": "AZ ALKALMAZÁS RÉSZE",
    "applicationTitle": "Beállítások, amelyek az Önéi maradnak.",
    "applicationText": "Megjelenés, mentett profilok, beviteli adatvédelem és bővítménykezelés alkalmazás-infrastruktúra, nem külön bővítmény.",
    "privacyTitle": "A bevitel adatvédelme külön téma.",
    "privacyText": "Az ellenőrzött verzió elrejti a billentyűket, ha védett bevitelt észlel. Vannak korlátok és ismert versenyhelyzet a fókuszértesítéseknél: érzékeny bevitel előtt kézzel rejtse el a kijelzést. A kódba vetett bizalom külön döntés.",
    "workflowTitle": "Az új modell bemutatója",
    "workflowNote": "A tervezett felület szimulált bejárása. Import és bizalom működik a preview.3-ban; a jelenet nem képernyőkép és nem bizonyítja minden bővítmény kész állapotát.",
    "workflowSteps": [
      "Kezdés a beépített billentyűkkel és egérrel",
      "Bővítmények megnyitása",
      "A szerző időzítőjének kiválasztása",
      "Bizalom megerősítése és bekapcsolás",
      "Megjelenik a további eszköz"
    ],
    "coreTab": "Beépített",
    "pluginsTab": "Bővítmények",
    "enable": "Bekapcsolás",
    "trust": "Csak megbízható forrás kódját kapcsolja be. A külön folyamat nem biztonságos homokozó.",
    "enabled": "Bekapcsolva",
    "start": "Indítás",
    "pause": "Szünet",
    "resume": "Folytatás",
    "reset": "Visszaállítás",
    "finished": "Befejeződött",
    "timerCaption": "Szimulált visszaszámlálás · szünet · folytatás",
    "layoutCaption": "Szimulált beviteli nyelvváltás",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Mit tartalmaz az alkalmazás?",
        "A tervezett mag billentyűket és egeret tartalmaz. Profilok, megjelenés, adatvédelem és bővítménykezelés az alkalmazás részei. Az átállás alatt a preview.3 régi eszközöket is tartalmaz; csak az időzítő ellenőrzött önálló bővítmény."
      ],
      [
        "Letölthető a Backpack és a bővítmények?",
        "A Backpack 0.4.0-preview.3 ellenőrzött, aláíratlan és nem nyilvános. A helyi csomag nem nyilvános letöltés. A katalógus külön kezeli a terveket, forrást és csomagolt példát."
      ],
      [
        "Hogyan telepíthetők a bővítmények?",
        "Bővítmények → Importálás → .bppkg. Az import ellenőriz és kicsomagol kódfuttatás vagy bizalom megadása nélkül. Új vagy frissített bővítmény kikapcsolva marad; az első bekapcsolás megerősítést kér."
      ],
      [
        "Biztonságosan elszigeteltek a bővítmények?",
        "Megbízható forrást használjon. A külön folyamatok a felhasználó jogaival futnak, és segítenek a hibakezelésben, de nem homokozók. A manifeszt képességei a Backpack felületét korlátozzák, nem minden rendszerműveletet."
      ],
      [
        "Hol kezdjem saját eszközömet?",
        "F1 → Bővítmények: szerződés, életciklus, parancsok, beállítások, nyelvek és időzítőfordítás. Részletes útmutatók és működő példák készülnek; az SDK feltételei a szabad saját bővítménykészítést fogják lehetővé tenni. Nyilvános hivatkozások még nincsenek."
      ],
      [
        "Minden mezőt véd az adatvédelem?",
        "Nincs univerzális felismerési ígéret. Védett vagy ismeretlen bevitel elrejthető, de az aszinkron fókuszfelismerés korlátozott. Érzékeny bevitel előtt kézzel rejtse el. A bővítménybizalom külön döntés."
      ],
      [
        "Milyen rendszerek és licenc?",
        "Ellenőrzött csomag Windows 10/11 x64 rendszerhez, .NET-tel. PolyForm Strict 1.0.0, nem nyílt forrású. Az SDK és példák feltételeinek frissítése folyamatban van."
      ]
    ]
  },
  "ro": {
    "title": "Backpack — Acțiuni clare. Instrumentele tale.",
    "intro": "Fă prezentările, lecțiile și înregistrările mai ușor de urmărit. Arată fiecare tastă și clic.",
    "extension": "Adaugă instrumente ca pluginuri independente sau descoperă cum să creezi unul propriu.",
    "coreTag": "INTEGRAT ÎN BACKPACK",
    "coreTitle": "Începe cu esențialul.",
    "coreText": "Tastele și vizualizarea mouse-ului sunt cele două instrumente din nucleul Backpack.",
    "pluginsTag": "PLUGINURILE AUTORULUI",
    "pluginsTitle": "Instrumentele potrivite poveștii tale.",
    "pluginsText": "Extensiile autorului Backpack se adaugă independent și sunt exemple pentru viitoare pluginuri proprii.",
    "transitionNote": "Acesta este catalogul țintă. În versiunea verificată, desenul și aranjamentul sunt încă integrate; instrumentele de mărire trec și ele la pluginuri. Scenele ilustrează direcția, nu o migrare încheiată.",
    "builtin": "Integrat",
    "authorPlugin": "Plugin al autorului",
    "notPublished": "Pachet public încă indisponibil",
    "byAuthor": "Autor",
    "statuses": {
      "planned": "Plugin planificat",
      "source": "Plugin în codul-sursă",
      "verified": "Plugin verificat în pachet"
    },
    "availability": {
      "planned": "Instrumentul funcționează în preview.3, dar pluginul independent nu este încă implementat sau împachetat.",
      "source": "Un exemplu SDK 1.1 există în codul actual. Acest plugin independent nu este verificat în preview.3.",
      "verified": "Cronometrul 1.0.0 / SDK 1.0 este verificat în Samples din preview.3. Codul actual este mai nou; nu există descărcare publică."
    },
    "pluginNames": [
      "Desen",
      "Indicator de aranjament",
      "Lupă",
      "Focalizare",
      "Lupă la cursor",
      "Cronometru"
    ],
    "pluginBenefits": [
      "Note, forme și text temporare în timpul explicației.",
      "Fă vizibilă schimbarea limbii de introducere.",
      "Mută o vedere mărită separată a zonei alese.",
      "Întunecă împrejurimile, păstrând zona clară la dimensiunea originală.",
      "Apropie detaliile cu o lupă care urmărește cursorul.",
      "Afișează numărătoarea inversă, întrerupe și continuă."
    ],
    "developerTag": "CREEAZĂ PROPRIUL INSTRUMENT",
    "developerTitle": "Sarcina ta. Următorul tău instrument.",
    "developerText": "Pluginurile autorului ilustrează contractul cu Backpack. Se pregătesc instrucțiuni detaliate pornind de la exemplul cronometrului.",
    "developerSteps": [
      [
        "Comenzi și ciclu de viață",
        "SDK conectează pornirea, oprirea și comenzile cu Backpack. Comenzilor li se pot atribui scurtături."
      ],
      [
        "Setări și interfață",
        "Manifestul descrie setările; Backpack desenează controalele și panourile suprapuse. Culorile sunt parametri expliciți, nu un API automat de teme garantat."
      ],
      [
        "Localizare și exemple",
        "Etichetele localizate și Context.Locale acceptă limbile aplicației. Ajutorul explică cronometrul, compilarea, împachetarea și importul .bppkg."
      ]
    ],
    "docsNotice": "F1 → Pluginuri explică contractul, compilarea și importul. Legăturile publice către documentația SDK și sursele exemplelor nu sunt încă configurate.",
    "licenseNote": "Condițiile SDK sunt actualizate pentru a permite crearea liberă a propriilor pluginuri. Se pregătesc ghiduri detaliate, pas cu pas, pentru compilare și import, precum și exemple funcționale.",
    "applicationTag": "PARTE A APLICAȚIEI",
    "applicationTitle": "O configurație care rămâne a ta.",
    "applicationText": "Aspectul, profilurile salvate, confidențialitatea introducerii și gestionarea pluginurilor sunt infrastructură, nu pluginuri separate.",
    "privacyTitle": "Confidențialitatea introducerii, separat.",
    "privacyText": "Versiunea verificată ascunde tastele când detectează introducere protejată. Există limite și o condiție de concurență cunoscută a notificărilor de focalizare: ascunde manual înainte de date sensibile. Încrederea în cod este o decizie distinctă.",
    "workflowTitle": "Demonstrația noului model",
    "workflowNote": "Parcurs simulat al interfeței țintă. Importul și încrederea sunt implementate în preview.3; scena nu este o captură sau dovada că toate pluginurile sunt gata.",
    "workflowSteps": [
      "Începe cu tastele și mouse-ul integrate",
      "Deschide pluginurile",
      "Alege cronometrul autorului",
      "Confirmă încrederea și activează",
      "Apare instrumentul suplimentar"
    ],
    "coreTab": "Integrat",
    "pluginsTab": "Pluginuri",
    "enable": "Activează",
    "trust": "Activează numai cod din surse de încredere. Un proces separat nu este un mediu izolat de securitate.",
    "enabled": "Activat",
    "start": "Start",
    "pause": "Pauză",
    "resume": "Continuă",
    "reset": "Resetează",
    "finished": "Încheiat",
    "timerCaption": "Numărătoare simulată · pauză · continuare",
    "layoutCaption": "Schimbare simulată a limbii de introducere",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Ce include aplicația?",
        "Nucleul țintă conține taste și mouse. Profilurile, aspectul, confidențialitatea și gestionarea pluginurilor aparțin aplicației. preview.3 păstrează instrumente vechi în timpul migrării; doar cronometrul este verificat ca plugin independent."
      ],
      [
        "Pot descărca Backpack și pluginurile?",
        "Backpack 0.4.0-preview.3 este verificat, nesemnat și nepublicat. Un pachet local nu înseamnă descărcare publică. Catalogul distinge planuri, cod și exemplu împachetat."
      ],
      [
        "Cum se instalează extensiile?",
        "Pluginuri → Importă plugin → .bppkg. Importul verifică și extrage fără executare de cod sau acordarea încrederii. Pluginurile noi sau actualizate sunt dezactivate; prima activare cere confirmare."
      ],
      [
        "Sunt pluginurile izolate sigur?",
        "Folosește surse de încredere. Procesele separate au drepturile tale și ajută la gestionarea erorilor, dar nu sunt un mediu izolat de securitate. Capacitățile limitează interfața Backpack, nu toate acțiunile codului în sistem."
      ],
      [
        "Cum încep un instrument?",
        "F1 → Pluginuri explică contractul, ciclul de viață, comenzile, setările, limbile și compilarea cronometrului. Se pregătesc ghiduri detaliate și exemple funcționale; condițiile SDK vor permite crearea liberă a propriilor pluginuri. Legăturile publice lipsesc încă."
      ],
      [
        "Confidențialitatea protejează orice câmp?",
        "Nu se promite detecție universală. Introducerea protejată sau necunoscută poate fi ascunsă, dar detectarea asincronă a focalizării are limite. Ascunde înainte de date sensibile. Încrederea în pluginuri este separată."
      ],
      [
        "Ce sisteme și licență se aplică?",
        "Pachet verificat pentru Windows 10/11 x64 cu .NET. PolyForm Strict 1.0.0, nu este open source. Condițiile SDK/exemplelor sunt în curs de actualizare."
      ]
    ]
  },
  "sv": {
    "title": "Backpack — Tydliga handlingar. Egna verktyg.",
    "intro": "Gör presentationer, lektioner och skärminspelningar lättare att följa. Visa varje tangent och klick.",
    "extension": "Lägg till verktyg som fristående insticksprogram eller upptäck hur du skapar ett eget.",
    "coreTag": "INBYGGT I BACKPACK",
    "coreTitle": "Börja med det viktigaste.",
    "coreText": "Tangenter och musvisualisering är de två verktygen i Backpacks kärna.",
    "pluginsTag": "UPPHOVSPERSONENS INSTICKSPROGRAM",
    "pluginsTitle": "Verktygen för din berättelse.",
    "pluginsText": "Backpack-upphovspersonens tillägg läggs till oberoende och fungerar som exempel för framtida egna insticksprogram.",
    "transitionNote": "Detta är målkatalogen. I den verifierade versionen är ritning och layout fortfarande inbyggda; förstoringsverktygen flyttas också till insticksprogram. Scenerna visar riktningen, inte en slutförd flytt.",
    "builtin": "Inbyggt",
    "authorPlugin": "Upphovspersonens tillägg",
    "notPublished": "Offentligt paket saknas ännu",
    "byAuthor": "Upphovsperson",
    "statuses": {
      "planned": "Insticksprogram planerat",
      "source": "Insticksprogram i källkod",
      "verified": "Insticksprogram verifierat i paket"
    },
    "availability": {
      "planned": "Verktyget fungerar i preview.3, men det fristående insticksprogrammet är ännu inte implementerat eller paketerat.",
      "source": "Ett SDK 1.1-exempel finns i aktuell källkod. Detta fristående insticksprogram är inte verifierat i preview.3.",
      "verified": "Timer 1.0.0 / SDK 1.0 är verifierad i Samples i preview.3. Aktuell källkod är nyare; ingen offentlig nedladdning."
    },
    "pluginNames": [
      "Ritning",
      "Layoutindikator",
      "Förstoringsglas",
      "Fokus",
      "Pekarlupp",
      "Timer"
    ],
    "pluginBenefits": [
      "Tillfälliga anteckningar, former och text medan du förklarar.",
      "Gör bytet av inmatningsspråk synligt.",
      "Flytta en separat förstorad vy av valt område.",
      "Mörklägg omgivningen och håll området tydligt i originalstorlek.",
      "Förstora detaljer med en lupp som följer pekaren.",
      "Visa en nedräkning, pausa och fortsätt vid behov."
    ],
    "developerTag": "SKAPA DITT EGET VERKTYG",
    "developerTitle": "Din uppgift. Ditt nästa verktyg.",
    "developerText": "Upphovspersonens tillägg visar kontraktet med Backpack. Detaljerade instruktioner förbereds med timerexemplet som utgångspunkt.",
    "developerSteps": [
      [
        "Kommandon och livscykel",
        "SDK kopplar start, stopp och kommandon till Backpack. Kommandon kan få kortkommandon."
      ],
      [
        "Inställningar och gränssnitt",
        "Manifestet beskriver inställningar; Backpack ritar kontroller och överläggspaneler. Färger är uttryckliga parametrar, inte ett utlovat automatiskt tema-API."
      ],
      [
        "Lokalisering och exempel",
        "Lokaliserade etiketter och Context.Locale stöder appens språk. Hjälpen beskriver timer, bygge, paketering och .bppkg-import."
      ]
    ],
    "docsNotice": "F1 → Insticksprogram beskriver kontrakt, bygge och import. Offentliga länkar till SDK-dokumentation och exempelkällor är ännu inte konfigurerade.",
    "licenseNote": "SDK-villkoren uppdateras för att möjliggöra fritt skapande av egna insticksprogram. Detaljerade stegvisa bygg- och importguider samt fungerande exempel förbereds.",
    "applicationTag": "DEL AV PROGRAMMET",
    "applicationTitle": "En uppsättning som förblir din.",
    "applicationText": "Utseende, sparade profiler, inmatningsintegritet och hantering av insticksprogram är infrastruktur, inte separata tillägg.",
    "privacyTitle": "Inmatningsintegritet, separat.",
    "privacyText": "Den verifierade versionen döljer tangenter vid upptäckt skyddad inmatning. Upptäckten har begränsningar och en känd kapplöpning i fokusmeddelanden: dölj manuellt före känslig inmatning. Tillit till kod är ett separat beslut.",
    "workflowTitle": "Demonstration av den nya modellen",
    "workflowNote": "Simulerad genomgång av målgränssnittet. Import och tillit finns i preview.3; scenen är ingen skärmbild eller bevis för att alla tillägg är klara.",
    "workflowSteps": [
      "Börja med inbyggda tangenter och mus",
      "Öppna insticksprogrammen",
      "Välj upphovspersonens timer",
      "Bekräfta tillit och slå på",
      "Det extra verktyget visas"
    ],
    "coreTab": "Inbyggt",
    "pluginsTab": "Insticksprogram",
    "enable": "Slå på",
    "trust": "Aktivera bara kod från betrodda källor. En separat process är ingen sandlåda.",
    "enabled": "Aktiverat",
    "start": "Starta",
    "pause": "Paus",
    "resume": "Fortsätt",
    "reset": "Återställ",
    "finished": "Klart",
    "timerCaption": "Simulerad nedräkning · paus · fortsätt",
    "layoutCaption": "Simulerat byte av inmatningsspråk",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Vad ingår i programmet?",
        "Målkärnan innehåller tangenter och mus. Profiler, utseende, integritet och tilläggshantering hör till appen. preview.3 behåller äldre verktyg under flytten; bara timern är verifierad som fristående tillägg."
      ],
      [
        "Kan Backpack och tilläggen hämtas?",
        "Backpack 0.4.0-preview.3 är verifierad, osignerad och opublicerad. Ett lokalt paket är ingen offentlig nedladdning. Katalogen skiljer planer, källkod och paketerat exempel."
      ],
      [
        "Hur installeras tillägg?",
        "Insticksprogram → Importera → .bppkg. Import kontrollerar och packar upp utan kodkörning eller tillit. Nya och uppdaterade tillägg är avstängda; första aktivering kräver bekräftelse."
      ],
      [
        "Är tilläggen säkert isolerade?",
        "Använd betrodda källor. Separata processer har dina rättigheter och hjälper vid fel, men är ingen sandlåda. Manifestets förmågor begränsar Backpack-gränssnittet, inte alla systemåtgärder."
      ],
      [
        "Hur börjar jag ett verktyg?",
        "F1 → Insticksprogram beskriver kontrakt, livscykel, kommandon, inställningar, språk och timerbygge. Detaljerade guider och fungerande exempel förbereds; SDK-villkoren ska möjliggöra fritt skapande av egna tillägg. Offentliga länkar saknas ännu."
      ],
      [
        "Skyddar integritet alla fält?",
        "Ingen universell upptäckt utlovas. Skyddad eller okänd inmatning kan döljas, men asynkron fokusupptäckt har begränsningar. Dölj före känslig inmatning. Tillit till tillägg är separat."
      ],
      [
        "Vilka system och licens gäller?",
        "Verifierat paket för Windows 10/11 x64 med .NET. PolyForm Strict 1.0.0, inte öppen källkod. SDK-/exempelvillkoren håller på att uppdateras."
      ]
    ]
  },
  "da": {
    "title": "Backpack — Tydelige handlinger. Egne værktøjer.",
    "intro": "Gør præsentationer, lektioner og skærmoptagelser lettere at følge. Vis hver tast og hvert klik.",
    "extension": "Tilføj værktøjer som uafhængige plugins, eller lær at skabe dit eget.",
    "coreTag": "INDBYGGET I BACKPACK",
    "coreTitle": "Start med det vigtigste.",
    "coreText": "Taster og musevisualisering er de to værktøjer i Backpacks kerne.",
    "pluginsTag": "FORFATTERENS PLUGINS",
    "pluginsTitle": "Værktøjerne til din fortælling.",
    "pluginsText": "Backpack-forfatterens udvidelser tilføjes uafhængigt og fungerer som eksempler på fremtidige egne plugins.",
    "transitionNote": "Dette er det planlagte katalog. I den verificerede version er tegning og layout stadig indbygget; forstørrelsesværktøjerne flyttes også til plugins. Scenerne viser retningen, ikke en afsluttet flytning.",
    "builtin": "Indbygget",
    "authorPlugin": "Forfatterens plugin",
    "notPublished": "Offentlig pakke endnu ikke tilgængelig",
    "byAuthor": "Forfatter",
    "statuses": {
      "planned": "Plugin planlagt",
      "source": "Plugin i kildekoden",
      "verified": "Plugin verificeret i pakken"
    },
    "availability": {
      "planned": "Værktøjet fungerer i preview.3, men det uafhængige plugin er endnu ikke implementeret eller pakket.",
      "source": "Et SDK 1.1-eksempel findes i den aktuelle kode. Dette uafhængige plugin er ikke verificeret i preview.3.",
      "verified": "Timer 1.0.0 / SDK 1.0 er verificeret i Samples i preview.3. Den aktuelle kildekode er nyere; ingen offentlig download."
    },
    "pluginNames": [
      "Tegning",
      "Layoutindikator",
      "Lup",
      "Fokus",
      "Markørlup",
      "Timer"
    ],
    "pluginBenefits": [
      "Midlertidige noter, former og tekst, mens du forklarer.",
      "Gør skift af indtastningssprog synligt.",
      "Flyt en separat forstørret visning af et valgt område.",
      "Dæmp omgivelserne, og bevar området tydeligt i original størrelse.",
      "Forstør detaljer med en lup, der følger markøren.",
      "Vis en nedtælling, sæt på pause og fortsæt."
    ],
    "developerTag": "SKAB DIT EGET VÆRKTØJ",
    "developerTitle": "Din opgave. Dit næste værktøj.",
    "developerText": "Forfatterens plugins viser kontrakten med Backpack. Detaljerede instruktioner forberedes med timereksemplet som udgangspunkt.",
    "developerSteps": [
      [
        "Kommandoer og livscyklus",
        "SDK forbinder start, stop og kommandoer med Backpack. Kommandoer kan få tastaturgenveje."
      ],
      [
        "Indstillinger og interface",
        "Manifestet beskriver indstillinger; Backpack tegner kontroller og overlejringspaneler. Farver er udtrykkelige parametre, ikke en lovet automatisk tema-API."
      ],
      [
        "Lokalisering og eksempler",
        "Oversatte etiketter og Context.Locale understøtter appens sprog. Hjælpen forklarer timer, bygning, pakning og .bppkg-import."
      ]
    ],
    "docsNotice": "F1 → Plugins beskriver kontrakt, bygning og import. Offentlige links til SDK-dokumentation og eksempelkode er endnu ikke konfigureret.",
    "licenseNote": "SDK-vilkårene opdateres for at gøre det muligt frit at skabe egne plugins. Detaljerede trinvis bygge- og importvejledninger samt fungerende eksempler forberedes.",
    "applicationTag": "DEL AF PROGRAMMET",
    "applicationTitle": "En opsætning, der forbliver din.",
    "applicationText": "Udseende, gemte profiler, indtastningsbeskyttelse og pluginstyring er infrastruktur, ikke separate plugins.",
    "privacyTitle": "Indtastningsbeskyttelse, særskilt.",
    "privacyText": "Den verificerede version skjuler taster ved registreret beskyttet indtastning. Registreringen har grænser og en kendt kapløbstilstand ved fokusbeskeder: skjul manuelt før følsom indtastning. Tillid til kode er en særskilt beslutning.",
    "workflowTitle": "Demonstration af den nye model",
    "workflowNote": "Simuleret gennemgang af det planlagte interface. Import og tillid findes i preview.3; scenen er intet skærmbillede eller bevis på, at alle plugins er klar.",
    "workflowSteps": [
      "Start med indbyggede taster og mus",
      "Åbn plugins",
      "Vælg forfatterens timer",
      "Bekræft tillid, og slå til",
      "Det ekstra værktøj vises"
    ],
    "coreTab": "Indbygget",
    "pluginsTab": "Plugins",
    "enable": "Slå til",
    "trust": "Aktivér kun kode fra betroede kilder. En separat proces er ikke en sandkasse.",
    "enabled": "Aktiveret",
    "start": "Start",
    "pause": "Pause",
    "resume": "Fortsæt",
    "reset": "Nulstil",
    "finished": "Færdig",
    "timerCaption": "Simuleret nedtælling · pause · fortsæt",
    "layoutCaption": "Simuleret skift af indtastningssprog",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Hvad indeholder programmet?",
        "Den planlagte kerne indeholder taster og mus. Profiler, udseende, beskyttelse og pluginstyring tilhører appen. preview.3 beholder ældre værktøjer under flytningen; kun timeren er verificeret som uafhængigt plugin."
      ],
      [
        "Kan Backpack og plugins hentes?",
        "Backpack 0.4.0-preview.3 er verificeret, usigneret og ikke udgivet. En lokal pakke er ikke en offentlig download. Kataloget skelner mellem planer, kildekode og et pakket eksempel."
      ],
      [
        "Hvordan installeres udvidelser?",
        "Plugins → Importér plugin → .bppkg. Import kontrollerer og pakker ud uden at køre kode eller give tillid. Nye og opdaterede plugins er slået fra; første aktivering kræver bekræftelse."
      ],
      [
        "Er plugins sikkert isolerede?",
        "Brug betroede kilder. Separate processer har dine rettigheder og hjælper ved fejl, men er ikke en sandkasse. Manifestets egenskaber begrænser Backpack-grænsefladen, ikke alle handlinger på computeren."
      ],
      [
        "Hvordan starter jeg et værktøj?",
        "F1 → Plugins beskriver kontrakt, livscyklus, kommandoer, indstillinger, sprog og timerbygning. Detaljerede vejledninger og fungerende eksempler forberedes; SDK-vilkårene skal give frihed til egne plugins. Offentlige links mangler endnu."
      ],
      [
        "Beskytter privatlivsfunktionen alle felter?",
        "Ingen universel registrering loves. Beskyttet eller ukendt indtastning kan skjules, men asynkron fokusregistrering har grænser. Skjul før følsomme data. Tillid til plugins er særskilt."
      ],
      [
        "Hvilke systemer og licens gælder?",
        "Verificeret pakke til Windows 10/11 x64 med .NET. PolyForm Strict 1.0.0, ikke open source. SDK-/eksempelvilkårene opdateres."
      ]
    ]
  },
  "nb": {
    "title": "Backpack — Tydelige handlinger. Egne verktøy.",
    "intro": "Gjør presentasjoner, timer og skjermopptak lettere å følge. Vis hver tast og hvert klikk.",
    "extension": "Legg til verktøy som uavhengige programtillegg, eller lær å lage ditt eget.",
    "coreTag": "INNEBYGD I BACKPACK",
    "coreTitle": "Begynn med det viktigste.",
    "coreText": "Taster og musevisualisering er de to verktøyene i kjernen av Backpack.",
    "pluginsTag": "OPPHAVERENS PROGRAMTILLEGG",
    "pluginsTitle": "Verktøyene til din fortelling.",
    "pluginsText": "Backpack-opphaverens utvidelser legges til uavhengig og fungerer som eksempler på fremtidige egne tillegg.",
    "transitionNote": "Dette er den planlagte katalogen. I den verifiserte versjonen er tegning og oppsett fortsatt innebygd; forstørrelsesverktøyene flyttes også til tillegg. Scenene viser retningen, ikke en fullført flytting.",
    "builtin": "Innebygd",
    "authorPlugin": "Opphaverens tillegg",
    "notPublished": "Offentlig pakke er ikke tilgjengelig ennå",
    "byAuthor": "Opphaver",
    "statuses": {
      "planned": "Planlagt tillegg",
      "source": "Tillegg i kildekoden",
      "verified": "Tillegg verifisert i pakken"
    },
    "availability": {
      "planned": "Verktøyet fungerer i preview.3, men det uavhengige tillegget er ikke implementert eller pakket ennå.",
      "source": "Et SDK 1.1-eksempel finnes i gjeldende kildekode. Dette uavhengige tillegget er ikke verifisert i preview.3.",
      "verified": "Timer 1.0.0 / SDK 1.0 er verifisert i Samples i preview.3. Gjeldende kildekode er nyere; ingen offentlig nedlasting."
    },
    "pluginNames": [
      "Tegning",
      "Oppsettindikator",
      "Lupe",
      "Fokus",
      "Pekerlupe",
      "Timer"
    ],
    "pluginBenefits": [
      "Midlertidige notater, former og tekst mens du forklarer.",
      "Gjør bytte av inndataspråk synlig.",
      "Flytt en separat forstørret visning av valgt område.",
      "Demp omgivelsene og hold området tydelig i opprinnelig størrelse.",
      "Forstørr detaljer med en lupe som følger pekeren.",
      "Vis en nedtelling, sett på pause og fortsett."
    ],
    "developerTag": "LAG DITT EGET VERKTØY",
    "developerTitle": "Din oppgave. Ditt neste verktøy.",
    "developerText": "Opphaverens tillegg viser kontrakten med Backpack. Detaljerte instruksjoner forberedes med timereksemplet som utgangspunkt.",
    "developerSteps": [
      [
        "Kommandoer og livssyklus",
        "SDK knytter start, stopp og kommandoer til Backpack. Kommandoer kan få hurtigtaster."
      ],
      [
        "Innstillinger og grensesnitt",
        "Manifestet beskriver innstillinger; Backpack tegner kontroller og overleggspaneler. Farger er uttrykkelige parametere, ikke et lovet automatisk tema-API."
      ],
      [
        "Lokalisering og eksempler",
        "Oversatte etiketter og Context.Locale støtter appens språk. Hjelpen forklarer timer, bygging, pakking og .bppkg-import."
      ]
    ],
    "docsNotice": "F1 → Programtillegg beskriver kontrakt, bygging og import. Offentlige lenker til SDK-dokumentasjon og eksempelkode er ennå ikke konfigurert.",
    "licenseNote": "SDK-vilkårene oppdateres for å gjøre det mulig å lage egne tillegg fritt. Detaljerte trinnvise bygge- og importveiledninger og fungerende eksempler forberedes.",
    "applicationTag": "DEL AV PROGRAMMET",
    "applicationTitle": "Et oppsett som forblir ditt.",
    "applicationText": "Utseende, lagrede profiler, inndatapersonvern og administrasjon av tillegg er infrastruktur, ikke separate tillegg.",
    "privacyTitle": "Inndatapersonvern, særskilt.",
    "privacyText": "Den verifiserte versjonen skjuler taster ved registrert beskyttet inndata. Registreringen har grenser og en kjent kappløpstilstand ved fokusmeldinger: skjul manuelt før sensitive inndata. Tillit til kode er en egen beslutning.",
    "workflowTitle": "Demonstrasjon av den nye modellen",
    "workflowNote": "Simulert gjennomgang av planlagt grensesnitt. Import og tillit finnes i preview.3; scenen er ikke et skjermbilde eller bevis for at alle tillegg er klare.",
    "workflowSteps": [
      "Start med innebygde taster og mus",
      "Åpne programtillegg",
      "Velg opphaverens timer",
      "Bekreft tillit og slå på",
      "Det ekstra verktøyet vises"
    ],
    "coreTab": "Innebygd",
    "pluginsTab": "Programtillegg",
    "enable": "Slå på",
    "trust": "Aktiver bare kode fra pålitelige kilder. En separat prosess er ikke en sandkasse.",
    "enabled": "Aktivert",
    "start": "Start",
    "pause": "Pause",
    "resume": "Fortsett",
    "reset": "Tilbakestill",
    "finished": "Ferdig",
    "timerCaption": "Simulert nedtelling · pause · fortsett",
    "layoutCaption": "Simulert bytte av inndataspråk",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Hva følger med programmet?",
        "Den planlagte kjernen inneholder taster og mus. Profiler, utseende, personvern og tilleggsadministrasjon hører til appen. preview.3 beholder eldre verktøy under flyttingen; bare timeren er verifisert som uavhengig tillegg."
      ],
      [
        "Kan Backpack og tillegg lastes ned?",
        "Backpack 0.4.0-preview.3 er verifisert, usignert og upublisert. En lokal pakke er ingen offentlig nedlasting. Katalogen skiller mellom planer, kildekode og pakket eksempel."
      ],
      [
        "Hvordan installeres utvidelser?",
        "Programtillegg → Importer → .bppkg. Import kontrollerer og pakker ut uten kodekjøring eller tillit. Nye og oppdaterte tillegg er avslått; første aktivering krever bekreftelse."
      ],
      [
        "Er tillegg sikkert isolert?",
        "Bruk pålitelige kilder. Separate prosesser har dine rettigheter og hjelper ved feil, men er ingen sandkasse. Manifestets funksjoner begrenser Backpack-grensesnittet, ikke alle handlinger på maskinen."
      ],
      [
        "Hvordan begynner jeg et verktøy?",
        "F1 → Programtillegg beskriver kontrakt, livssyklus, kommandoer, innstillinger, språk og timerbygging. Detaljerte veiledninger og fungerende eksempler forberedes; SDK-vilkårene skal gi frihet til egne tillegg. Offentlige lenker mangler ennå."
      ],
      [
        "Beskytter personvern alle felt?",
        "Ingen universell registrering loves. Beskyttet eller ukjent inndata kan skjules, men asynkron fokusregistrering har grenser. Skjul før sensitive data. Tillit til tillegg er separat."
      ],
      [
        "Hvilke systemer og lisens gjelder?",
        "Verifisert pakke for Windows 10/11 x64 med .NET. PolyForm Strict 1.0.0, ikke åpen kildekode. SDK-/eksempelvilkårene oppdateres."
      ]
    ]
  },
  "fi": {
    "title": "Backpack — Selkeät toiminnot. Omat työkalut.",
    "intro": "Tee esityksistä, oppitunneista ja näytön tallenteista helpommin seurattavia. Näytä jokainen näppäin ja napsautus.",
    "extension": "Lisää työkaluja itsenäisinä lisäosina tai tutustu oman tekemiseen.",
    "coreTag": "BACKPACKIN SISÄINEN",
    "coreTitle": "Aloita olennaisesta.",
    "coreText": "Näppäimet ja hiiren havainnollistus ovat Backpackin ytimen kaksi työkalua.",
    "pluginsTag": "TEKIJÄN LISÄOSAT",
    "pluginsTitle": "Työkalut tarinaasi varten.",
    "pluginsText": "Backpackin tekijän laajennukset lisätään erikseen, ja ne toimivat esimerkkeinä tuleville omille lisäosille.",
    "transitionNote": "Tämä on tavoiteluettelo. Tarkistetussa versiossa piirto ja asettelu ovat yhä sisäisiä; suurennustyökalutkin siirtyvät lisäosiksi. Kohtaukset näyttävät suunnan, eivät valmista siirtymää.",
    "builtin": "Sisäinen",
    "authorPlugin": "Tekijän lisäosa",
    "notPublished": "Julkista pakettia ei vielä ole",
    "byAuthor": "Tekijä",
    "statuses": {
      "planned": "Lisäosa suunnitteilla",
      "source": "Lisäosa lähdekoodissa",
      "verified": "Lisäosa tarkistettu paketissa"
    },
    "availability": {
      "planned": "Työkalu toimii preview.3:ssa, mutta itsenäistä lisäosaa ei vielä ole toteutettu tai paketoitu.",
      "source": "SDK 1.1 -esimerkki on nykyisessä lähdekoodissa. Tätä itsenäistä lisäosaa ei ole tarkistettu preview.3:ssa.",
      "verified": "Ajastin 1.0.0 / SDK 1.0 on tarkistettu preview.3:n Samples-kansiossa. Nykyinen lähdekoodi on uudempi; julkista latausta ei ole."
    },
    "pluginNames": [
      "Piirtäminen",
      "Asetteluilmaisin",
      "Suurennuslasi",
      "Kohdistus",
      "Osoittimen suurennuslasi",
      "Ajastin"
    ],
    "pluginBenefits": [
      "Tilapäiset muistiinpanot, muodot ja teksti selityksen aikana.",
      "Tee syöttökielen vaihto näkyväksi.",
      "Siirrä valitun alueen erillistä suurennettua näkymää.",
      "Himmennä ympäristö ja säilytä alue selkeänä alkuperäiskoossa.",
      "Suurenna yksityiskohtia osoitinta seuraavalla suurennuslasilla.",
      "Näytä lähtölaskenta, keskeytä ja jatka tarpeen mukaan."
    ],
    "developerTag": "TEE OMA TYÖKALU",
    "developerTitle": "Oma tehtäväsi. Seuraava työkalusi.",
    "developerText": "Tekijän lisäosat esittelevät Backpackin sopimuksen. Ajastinesimerkin pohjalta valmistellaan yksityiskohtaisia ohjeita.",
    "developerSteps": [
      [
        "Komennot ja elinkaari",
        "SDK yhdistää käynnistyksen, pysäytyksen ja komennot Backpackiin. Komentoihin voi liittää pikanäppäimiä."
      ],
      [
        "Asetukset ja käyttöliittymä",
        "Manifesti kuvaa asetukset; Backpack piirtää säätimet ja peittopaneelit. Värit ovat erillisiä parametreja, ei luvattu automaattinen teema-API."
      ],
      [
        "Lokalisointi ja esimerkit",
        "Lokalisoidut tekstit ja Context.Locale tukevat sovelluksen kieliä. Ohje käsittelee ajastinta, kääntämistä, paketointia ja .bppkg-tuontia."
      ]
    ],
    "docsNotice": "F1 → Lisäosat kuvaa sopimuksen, kääntämisen ja tuonnin. Julkisia SDK-ohjeiden tai esimerkkilähteiden linkkejä ei vielä ole.",
    "licenseNote": "SDK:n ehtoja päivitetään mahdollistamaan omien lisäosien vapaa luominen. Yksityiskohtaisia vaiheittaisia kääntämis- ja tuontiohjeita sekä toimivia esimerkkejä valmistellaan.",
    "applicationTag": "OSA SOVELLUSTA",
    "applicationTitle": "Asetukset pysyvät ominasi.",
    "applicationText": "Ulkoasu, tallennetut profiilit, syötön yksityisyys ja lisäosien hallinta ovat infrastruktuuria, eivät erillisiä lisäosia.",
    "privacyTitle": "Syötön yksityisyys erikseen.",
    "privacyText": "Tarkistettu versio piilottaa näppäimet tunnistaessaan suojatun syötön. Tunnistuksessa on rajoja ja tunnettu kohdistusilmoitusten kilpailutilanne: piilota käsin ennen arkaluonteista syöttöä. Luottamus koodiin on eri päätös.",
    "workflowTitle": "Uuden mallin esittely",
    "workflowNote": "Tavoitekäyttöliittymän simuloitu kierros. Tuonti ja luottamus on toteutettu preview.3:ssa; kohtaus ei ole kuvakaappaus tai todiste kaikkien lisäosien valmiudesta.",
    "workflowSteps": [
      "Aloita sisäisillä näppäimillä ja hiirellä",
      "Avaa lisäosat",
      "Valitse tekijän ajastin",
      "Vahvista luottamus ja ota käyttöön",
      "Lisätyökalu ilmestyy"
    ],
    "coreTab": "Sisäinen",
    "pluginsTab": "Lisäosat",
    "enable": "Ota käyttöön",
    "trust": "Ota käyttöön vain luotettavien lähteiden koodia. Erillinen prosessi ei ole hiekkalaatikko.",
    "enabled": "Käytössä",
    "start": "Aloita",
    "pause": "Tauko",
    "resume": "Jatka",
    "reset": "Nollaa",
    "finished": "Valmis",
    "timerCaption": "Simuloitu lähtölaskenta · tauko · jatko",
    "layoutCaption": "Simuloitu syöttökielen vaihto",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Mitä sovellus sisältää?",
        "Tavoiteydin sisältää näppäimet ja hiiren. Profiilit, ulkoasu, yksityisyys ja lisäosien hallinta kuuluvat sovellukseen. preview.3 säilyttää vanhoja työkaluja siirtymän aikana; vain ajastin on tarkistettu itsenäisenä lisäosana."
      ],
      [
        "Voinko ladata Backpackin ja lisäosat?",
        "Backpack 0.4.0-preview.3 on tarkistettu, allekirjoittamaton ja julkaisematon. Paikallinen paketti ei tarkoita julkista latausta. Luettelo erottaa suunnitelmat, lähdekoodin ja paketoidun esimerkin."
      ],
      [
        "Miten laajennukset asennetaan?",
        "Lisäosat → Tuo lisäosa → .bppkg. Tuonti tarkistaa ja purkaa suorittamatta koodia tai antamatta luottamusta. Uudet ja päivitetyt lisäosat ovat pois käytöstä; ensimmäinen käyttö vaatii vahvistuksen."
      ],
      [
        "Ovatko lisäosat turvallisesti eristettyjä?",
        "Käytä luotettavia lähteitä. Erilliset prosessit toimivat oikeuksillasi ja auttavat virheiden hallinnassa, mutta eivät ole hiekkalaatikko. Manifestin kyvyt rajoittavat Backpackin rajapintaa, eivät kaikkia järjestelmätoimia."
      ],
      [
        "Mistä aloitan oman työkalun?",
        "F1 → Lisäosat kuvaa sopimuksen, elinkaaren, komennot, asetukset, kielet ja ajastimen kääntämisen. Tarkkoja ohjeita ja toimivia esimerkkejä valmistellaan; SDK-ehdot tulevat mahdollistamaan omien lisäosien vapaan tekemisen. Julkiset linkit puuttuvat vielä."
      ],
      [
        "Suojaako yksityisyys kaikki kentät?",
        "Yleistä tunnistusta ei luvata. Suojattu tai tuntematon syöttö voidaan piilottaa, mutta asynkronisessa kohdistustunnistuksessa on rajoja. Piilota ennen arkaluonteista syöttöä. Lisäosiin luottaminen on eri päätös."
      ],
      [
        "Mitkä järjestelmät ja lisenssi?",
        "Tarkistettu paketti Windows 10/11 x64:lle, .NET mukana. PolyForm Strict 1.0.0, ei avoin lähdekoodi. SDK:n ja esimerkkien ehtoja päivitetään."
      ]
    ]
  },
  "lt": {
    "title": "Backpack — Aiškūs veiksmai. Nuosavi įrankiai.",
    "intro": "Padėkite lengviau sekti pristatymus, pamokas ir ekrano įrašus. Rodykite kiekvieną klavišą ir spustelėjimą.",
    "extension": "Pridėkite įrankių kaip nepriklausomų papildinių arba sužinokite, kaip sukurti savą.",
    "coreTag": "ĮDIEGTA Į BACKPACK",
    "coreTitle": "Pradėkite nuo svarbiausio.",
    "coreText": "Klavišai ir pelės vizualizacija yra du pagrindiniai Backpack branduolio įrankiai.",
    "pluginsTag": "AUTORIAUS PAPILDINIAI",
    "pluginsTitle": "Įrankiai jūsų pasakojimui.",
    "pluginsText": "Backpack autoriaus plėtiniai pridedami nepriklausomai ir yra pavyzdžiai būsimiems nuosaviems papildiniams.",
    "transitionNote": "Tai numatomas katalogas. Patikrintoje versijoje piešimas ir išdėstymas dar įdiegti; didinimo įrankiai taip pat perkeliami į papildinius. Scenos rodo kryptį, o ne baigtą perkėlimą.",
    "builtin": "Įdiegta",
    "authorPlugin": "Autoriaus papildinys",
    "notPublished": "Viešo paketo dar nėra",
    "byAuthor": "Autorius",
    "statuses": {
      "planned": "Papildinys planuojamas",
      "source": "Papildinys pirminiame kode",
      "verified": "Papildinys patikrintas pakete"
    },
    "availability": {
      "planned": "Įrankis veikia preview.3, bet nepriklausomas papildinys dar nesukurtas ir nesupakuotas.",
      "source": "SDK 1.1 pavyzdys yra dabartiniame kode. Šis atskiras papildinys nepatikrintas preview.3 pakete.",
      "verified": "Laikmatis 1.0.0 / SDK 1.0 patikrintas preview.3 aplanke Samples. Dabartinis kodas naujesnis; viešo atsisiuntimo nėra."
    },
    "pluginNames": [
      "Piešimas",
      "Išdėstymo indikatorius",
      "Didintuvas",
      "Dėmesys",
      "Didintuvas prie žymeklio",
      "Laikmatis"
    ],
    "pluginBenefits": [
      "Laikinos pastabos, figūros ir tekstas aiškinant mintį.",
      "Parodykite įvesties kalbos pasikeitimą.",
      "Perkelkite atskirą padidintą pasirinktos srities vaizdą.",
      "Pritemdykite aplinką, palikdami sritį aiškią pradiniu masteliu.",
      "Didinkite detales didintuvu, kuris seka žymeklį.",
      "Rodykite atgalinį laiką, pristabdykite ir tęskite."
    ],
    "developerTag": "SUKURKITE SAVO ĮRANKĮ",
    "developerTitle": "Jūsų užduotis. Kitas jūsų įrankis.",
    "developerText": "Autoriaus papildiniai parodo sąsajos sutartį su Backpack. Pagal laikmačio pavyzdį rengiamos išsamios instrukcijos.",
    "developerSteps": [
      [
        "Komandos ir gyvavimo ciklas",
        "SDK susieja paleidimą, stabdymą ir komandas su Backpack. Komandoms galima priskirti sparčiuosius klavišus."
      ],
      [
        "Nustatymai ir sąsaja",
        "Manifestas aprašo nustatymus; Backpack piešia valdiklius ir ekrano skydelius. Spalvos nurodomos parametrais, automatinė temų API nežadama."
      ],
      [
        "Lokalizacija ir pavyzdžiai",
        "Lokalizuoti užrašai ir Context.Locale palaiko programos kalbas. Pagalba aiškina laikmatį, kūrimą, pakavimą ir .bppkg importą."
      ]
    ],
    "docsNotice": "F1 → Papildiniai aprašo sutartį, kūrimą ir importą. Viešos SDK dokumentacijos ir pavyzdžių kodo nuorodos dar nenustatytos.",
    "licenseNote": "SDK sąlygos atnaujinamos, kad būtų galima laisvai kurti savus papildinius. Rengiami išsamūs nuoseklūs kūrimo ir importo vadovai bei veikiantys pavyzdžiai.",
    "applicationTag": "PROGRAMOS DALIS",
    "applicationTitle": "Nustatymai, kurie lieka jūsų.",
    "applicationText": "Išvaizda, išsaugoti profiliai, įvesties privatumas ir papildinių valdymas yra infrastruktūra, ne atskiri papildiniai.",
    "privacyTitle": "Įvesties privatumas — atskirai.",
    "privacyText": "Patikrinta versija slepia klavišus aptikusi apsaugotą įvestį. Aptikimas turi ribas ir žinomą fokusavimo pranešimų lenktynių būseną: prieš jautrią įvestį slėpkite rankiniu būdu. Pasitikėjimas kodu yra atskiras sprendimas.",
    "workflowTitle": "Naujo modelio demonstracija",
    "workflowNote": "Numatomos sąsajos imitacija. Importas ir pasitikėjimas įgyvendinti preview.3; scena nėra ekrano kopija ar visų papildinių parengties įrodymas.",
    "workflowSteps": [
      "Pradėkite nuo įdiegtų klavišų ir pelės",
      "Atidarykite papildinius",
      "Pasirinkite autoriaus laikmatį",
      "Patvirtinkite pasitikėjimą ir įjunkite",
      "Pasirodo papildomas įrankis"
    ],
    "coreTab": "Įdiegta",
    "pluginsTab": "Papildiniai",
    "enable": "Įjungti",
    "trust": "Įjunkite tik patikimų šaltinių kodą. Atskiras procesas nėra saugi smėlio dėžė.",
    "enabled": "Įjungta",
    "start": "Pradėti",
    "pause": "Pauzė",
    "resume": "Tęsti",
    "reset": "Atkurti",
    "finished": "Baigta",
    "timerCaption": "Imituojamas atgalinis laikas · pauzė · tęsimas",
    "layoutCaption": "Imituojamas įvesties kalbos keitimas",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Kas įeina į programą?",
        "Numatomas branduolys apima klavišus ir pelę. Profiliai, išvaizda, privatumas ir papildinių valdymas priklauso programai. preview.3 perkėlimo metu išlaiko senus įrankius; tik laikmatis patikrintas kaip nepriklausomas papildinys."
      ],
      [
        "Ar galima atsisiųsti Backpack ir papildinius?",
        "Backpack 0.4.0-preview.3 patikrintas, nepasirašytas ir nepaskelbtas. Vietinis paketas nereiškia viešo atsisiuntimo. Katalogas skiria planus, kodą ir supakuotą pavyzdį."
      ],
      [
        "Kaip diegiami plėtiniai?",
        "Papildiniai → Importuoti papildinį → .bppkg. Importas tikrina ir išpakuoja nevykdydamas kodo ir nesuteikdamas pasitikėjimo. Nauji ar atnaujinti papildiniai išjungti; pirmas įjungimas reikalauja patvirtinimo."
      ],
      [
        "Ar papildiniai saugiai izoliuoti?",
        "Naudokite patikimus šaltinius. Atskiri procesai turi jūsų teises ir padeda valdyti gedimus, bet nėra saugi smėlio dėžė. Manifesto galimybės riboja Backpack sąsają, ne visus kodo veiksmus sistemoje."
      ],
      [
        "Nuo ko pradėti savą įrankį?",
        "F1 → Papildiniai aiškina sutartį, ciklą, komandas, nustatymus, kalbas ir laikmačio kūrimą. Rengiami išsamūs vadovai ir veikiantys pavyzdžiai; SDK sąlygos leis laisvai kurti savo papildinius. Viešų nuorodų dar nėra."
      ],
      [
        "Ar privatumas apsaugo kiekvieną lauką?",
        "Universalus aptikimas nežadamas. Apsaugota ar nežinoma įvestis gali būti slepiama, bet asinchroninis fokusavimo aptikimas turi ribas. Prieš jautrią įvestį slėpkite rankiniu būdu. Pasitikėjimas papildiniais yra atskiras."
      ],
      [
        "Kokios sistemos ir licencija?",
        "Patikrintas paketas skirtas Windows 10/11 x64, įtrauktas .NET. PolyForm Strict 1.0.0, ne atvirasis kodas. SDK ir pavyzdžių sąlygos atnaujinamos."
      ]
    ]
  },
  "el": {
    "title": "Backpack — Καθαρές ενέργειες. Δικά σας εργαλεία.",
    "intro": "Κάντε παρουσιάσεις, μαθήματα και εγγραφές πιο κατανοητά. Δείξτε κάθε πλήκτρο και κλικ.",
    "extension": "Προσθέστε εργαλεία ως ανεξάρτητα πρόσθετα ή μάθετε να δημιουργείτε το δικό σας.",
    "coreTag": "ΕΝΣΩΜΑΤΩΜΕΝΟ ΣΤΟ BACKPACK",
    "coreTitle": "Ξεκινήστε από τα βασικά.",
    "coreText": "Τα πλήκτρα και η οπτικοποίηση του ποντικιού είναι τα δύο εργαλεία του πυρήνα Backpack.",
    "pluginsTag": "ΠΡΟΣΘΕΤΑ ΤΟΥ ΔΗΜΙΟΥΡΓΟΥ",
    "pluginsTitle": "Τα εργαλεία για την ιστορία σας.",
    "pluginsText": "Οι επεκτάσεις του δημιουργού του Backpack προστίθενται ανεξάρτητα και λειτουργούν ως παραδείγματα για μελλοντικά δικά σας πρόσθετα.",
    "transitionNote": "Αυτός είναι ο προβλεπόμενος κατάλογος. Στην επαληθευμένη έκδοση, σχεδίαση και διάταξη παραμένουν ενσωματωμένες· τα εργαλεία μεγέθυνσης επίσης μεταφέρονται σε πρόσθετα. Οι σκηνές δείχνουν την κατεύθυνση, όχι ολοκληρωμένη μετάβαση.",
    "builtin": "Ενσωματωμένο",
    "authorPlugin": "Πρόσθετο του δημιουργού",
    "notPublished": "Δημόσιο πακέτο δεν υπάρχει ακόμη",
    "byAuthor": "Δημιουργός",
    "statuses": {
      "planned": "Πρόσθετο υπό σχεδιασμό",
      "source": "Πρόσθετο στον πηγαίο κώδικα",
      "verified": "Πρόσθετο επαληθευμένο στο πακέτο"
    },
    "availability": {
      "planned": "Το εργαλείο λειτουργεί στο preview.3, αλλά το ανεξάρτητο πρόσθετο δεν έχει ακόμη υλοποιηθεί ή συσκευαστεί.",
      "source": "Υπάρχει παράδειγμα SDK 1.1 στον τρέχοντα κώδικα. Αυτό το ανεξάρτητο πρόσθετο δεν έχει επαληθευτεί στο preview.3.",
      "verified": "Ο χρονομετρητής 1.0.0 / SDK 1.0 επαληθεύτηκε στο Samples του preview.3. Ο τρέχων κώδικας είναι νεότερος· δεν υπάρχει δημόσια λήψη."
    },
    "pluginNames": [
      "Σχεδίαση",
      "Ένδειξη διάταξης",
      "Μεγεθυντικός φακός",
      "Εστίαση",
      "Φακός δείκτη",
      "Χρονομετρητής"
    ],
    "pluginBenefits": [
      "Προσωρινές σημειώσεις, σχήματα και κείμενο καθώς εξηγείτε.",
      "Δείξτε την αλλαγή της γλώσσας εισαγωγής.",
      "Μετακινήστε μια ξεχωριστή μεγεθυμένη προβολή της επιλεγμένης περιοχής.",
      "Σκοτεινιάστε το περιβάλλον, κρατώντας την περιοχή καθαρή στο αρχικό μέγεθος.",
      "Μεγεθύνετε λεπτομέρειες με φακό που ακολουθεί τον δείκτη.",
      "Δείξτε αντίστροφη μέτρηση, παύση και συνέχιση."
    ],
    "developerTag": "ΔΗΜΙΟΥΡΓΗΣΤΕ ΔΙΚΟ ΣΑΣ ΕΡΓΑΛΕΙΟ",
    "developerTitle": "Η εργασία σας. Το επόμενο εργαλείο σας.",
    "developerText": "Τα πρόσθετα του δημιουργού δείχνουν τη σύμβαση με το Backpack. Ετοιμάζονται αναλυτικές οδηγίες με βάση το παράδειγμα του χρονομετρητή.",
    "developerSteps": [
      [
        "Εντολές και κύκλος ζωής",
        "Το SDK συνδέει έναρξη, διακοπή και εντολές με το Backpack. Οι εντολές δέχονται συντομεύσεις."
      ],
      [
        "Ρυθμίσεις και διεπαφή",
        "Το μανιφέστο περιγράφει ρυθμίσεις· το Backpack σχεδιάζει χειριστήρια και πάνελ. Τα χρώματα είναι ρητές παράμετροι, όχι εγγυημένο αυτόματο API θεμάτων."
      ],
      [
        "Τοπικοποίηση και παραδείγματα",
        "Οι μεταφρασμένες ετικέτες και το Context.Locale υποστηρίζουν τις γλώσσες της εφαρμογής. Η βοήθεια εξηγεί χρονομετρητή, μεταγλώττιση, συσκευασία και εισαγωγή .bppkg."
      ]
    ],
    "docsNotice": "Το F1 → Πρόσθετα εξηγεί σύμβαση, μεταγλώττιση και εισαγωγή. Δημόσιοι σύνδεσμοι τεκμηρίωσης SDK και πηγαίου κώδικα παραδειγμάτων δεν έχουν ακόμη οριστεί.",
    "licenseNote": "Οι όροι του SDK ενημερώνονται ώστε να επιτρέπουν την ελεύθερη δημιουργία δικών σας προσθέτων. Ετοιμάζονται αναλυτικοί οδηγοί βήμα προς βήμα για μεταγλώττιση και εισαγωγή, καθώς και λειτουργικά παραδείγματα.",
    "applicationTag": "ΜΕΡΟΣ ΤΗΣ ΕΦΑΡΜΟΓΗΣ",
    "applicationTitle": "Ρυθμίσεις που παραμένουν δικές σας.",
    "applicationText": "Εμφάνιση, αποθηκευμένα προφίλ, απόρρητο εισαγωγής και διαχείριση προσθέτων είναι υποδομή της εφαρμογής, όχι ξεχωριστά πρόσθετα.",
    "privacyTitle": "Το απόρρητο εισαγωγής, χωριστά.",
    "privacyText": "Η επαληθευμένη έκδοση κρύβει πλήκτρα όταν εντοπίζει προστατευμένη εισαγωγή. Υπάρχουν όρια και γνωστός ανταγωνισμός ειδοποιήσεων εστίασης: αποκρύψτε χειροκίνητα πριν από ευαίσθητη εισαγωγή. Η εμπιστοσύνη στον κώδικα είναι άλλη απόφαση.",
    "workflowTitle": "Επίδειξη του νέου μοντέλου",
    "workflowNote": "Προσομοιωμένη περιήγηση της προβλεπόμενης διεπαφής. Εισαγωγή και εμπιστοσύνη υλοποιούνται στο preview.3· η σκηνή δεν είναι στιγμιότυπο ούτε απόδειξη ετοιμότητας όλων των προσθέτων.",
    "workflowSteps": [
      "Ξεκινήστε με ενσωματωμένα πλήκτρα και ποντίκι",
      "Ανοίξτε τα πρόσθετα",
      "Επιλέξτε τον χρονομετρητή του δημιουργού",
      "Επιβεβαιώστε εμπιστοσύνη και ενεργοποιήστε",
      "Εμφανίζεται το επιπλέον εργαλείο"
    ],
    "coreTab": "Ενσωματωμένα",
    "pluginsTab": "Πρόσθετα",
    "enable": "Ενεργοποίηση",
    "trust": "Ενεργοποιείτε μόνο κώδικα από έμπιστες πηγές. Η ξεχωριστή διεργασία δεν είναι ασφαλές περιβάλλον απομόνωσης.",
    "enabled": "Ενεργό",
    "start": "Έναρξη",
    "pause": "Παύση",
    "resume": "Συνέχεια",
    "reset": "Επαναφορά",
    "finished": "Ολοκληρώθηκε",
    "timerCaption": "Προσομοίωση αντίστροφης μέτρησης · παύση · συνέχεια",
    "layoutCaption": "Προσομοίωση αλλαγής γλώσσας εισαγωγής",
    "layoutSequence": [
      "English",
      "Українська",
      "Deutsch"
    ],
    "faq": [
      [
        "Τι περιλαμβάνει η εφαρμογή;",
        "Ο προβλεπόμενος πυρήνας περιέχει πλήκτρα και ποντίκι. Προφίλ, εμφάνιση, απόρρητο και διαχείριση προσθέτων ανήκουν στην εφαρμογή. Το preview.3 διατηρεί παλιά εργαλεία κατά τη μετάβαση· μόνο ο χρονομετρητής επαληθεύτηκε ως ανεξάρτητο πρόσθετο."
      ],
      [
        "Μπορώ να κατεβάσω Backpack και πρόσθετα;",
        "Το Backpack 0.4.0-preview.3 είναι επαληθευμένο, χωρίς υπογραφή και αδημοσίευτο. Ένα τοπικό πακέτο δεν σημαίνει δημόσια λήψη. Ο κατάλογος διακρίνει σχέδια, κώδικα και συσκευασμένο παράδειγμα."
      ],
      [
        "Πώς εγκαθίστανται οι επεκτάσεις;",
        "Πρόσθετα → Εισαγωγή προσθέτου → .bppkg. Η εισαγωγή ελέγχει και αποσυμπιέζει χωρίς εκτέλεση κώδικα ή παραχώρηση εμπιστοσύνης. Νέα ή ενημερωμένα πρόσθετα είναι ανενεργά· η πρώτη ενεργοποίηση ζητά επιβεβαίωση."
      ],
      [
        "Είναι τα πρόσθετα ασφαλώς απομονωμένα;",
        "Χρησιμοποιείτε έμπιστες πηγές. Οι ξεχωριστές διεργασίες έχουν τα δικαιώματά σας και βοηθούν σε σφάλματα, αλλά δεν αποτελούν ασφαλή απομόνωση. Οι δυνατότητες περιορίζουν τη διεπαφή Backpack, όχι όλες τις ενέργειες του κώδικα στον υπολογιστή."
      ],
      [
        "Από πού αρχίζω ένα εργαλείο;",
        "F1 → Πρόσθετα εξηγεί σύμβαση, κύκλο ζωής, εντολές, ρυθμίσεις, γλώσσες και μεταγλώττιση χρονομετρητή. Ετοιμάζονται αναλυτικοί οδηγοί και λειτουργικά παραδείγματα· οι όροι SDK θα επιτρέπουν ελεύθερα δικά σας πρόσθετα. Δημόσιοι σύνδεσμοι ακόμη λείπουν."
      ],
      [
        "Προστατεύει το απόρρητο κάθε πεδίο;",
        "Δεν υπόσχεται καθολική ανίχνευση. Προστατευμένη ή άγνωστη εισαγωγή μπορεί να κρύβεται, αλλά η ασύγχρονη ανίχνευση εστίασης έχει όρια. Αποκρύψτε πριν από ευαίσθητα δεδομένα. Η εμπιστοσύνη στα πρόσθετα είναι ξεχωριστή."
      ],
      [
        "Ποια συστήματα και άδεια ισχύουν;",
        "Επαληθευμένο πακέτο για Windows 10/11 x64 με .NET. PolyForm Strict 1.0.0, όχι ανοικτός κώδικας. Οι όροι SDK και παραδειγμάτων ενημερώνονται."
      ]
    ]
  }
};
