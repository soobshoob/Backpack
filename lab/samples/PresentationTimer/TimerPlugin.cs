using System.Runtime.InteropServices;
using Backpack.Sdk;

namespace PresentationTimer;

/// <summary>
/// Презентационный таймер 2.0: обратный отсчёт на панели поверх экрана (панель рисует Backpack, её можно перемещать и
/// растягивать; кнопки и ручки появляются при наведении).
/// <list type="bullet">
/// <item>Команды: <c>toggle</c> — старт, пауза, продолжение; <c>reset</c> — сброс к заданному времени; <c>show</c> — показать
///       или скрыть панель. Те же действия — кнопками панели.</item>
/// <item>Время считается по часам (момент окончания), а не по числу тиков: задержки и пропущенные тики не копятся. Сон
///       компьютера: время во сне тоже идёт; если отсчёт закончился во сне, после пробуждения панель показывает 00:00 и
///       состояние «закончено».</item>
/// <item>Окончание: 00:00 остаётся на панели с ненавязчивой рамкой акцента; звук — только если включён (по умолчанию выключен).</item>
/// <item>После перезапуска Backpack таймер не запускается сам и панель не появляется до первой команды.</item>
/// </list>
/// Не читает ввод и экран.
/// </summary>
public sealed class TimerPlugin : BackpackPlugin
{
    private readonly Lock _lock = new();
    private Timer? _tick;
    private DateTime? _endsAt;     // идёт: момент окончания (UTC)
    private TimeSpan _left;        // пауза или стоп: оставшееся время
    private bool _finished;
    private bool _visible;

    private TimeSpan Duration
    {
        get
        {
            var s = Context.Settings;
            var d = TimeSpan.FromMinutes(s.Number("minutes", 5)) + TimeSpan.FromSeconds(s.Number("seconds", 0));
            return d > TimeSpan.Zero ? d : TimeSpan.FromSeconds(1);
        }
    }

    public override void OnStart()
    {
        _left = Duration;
        _tick = new Timer(_ => Tick(), null, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
    }

    public override void OnCommand(string commandId)
    {
        lock (_lock)
        {
            switch (commandId)
            {
                case "toggle":
                    _visible = true;
                    if (_endsAt is { } end)
                    {
                        _left = Clamp(end - DateTime.UtcNow); // пауза
                        _endsAt = null;
                    }
                    else
                    {
                        if (_finished)
                        {
                            _left = Duration; // после окончания — новый отсчёт
                            _finished = false;
                        }

                        _endsAt = DateTime.UtcNow + _left;
                    }

                    break;
                case "reset":
                    _visible = true;
                    _endsAt = null;
                    _finished = false;
                    _left = Duration;
                    break;
                case "show":
                    _visible = !_visible;
                    break;
            }
        }

        Show();
    }

    public override void OnPanelButton(string buttonId) => OnCommand(buttonId switch { "play" => "toggle", "reset" => "reset", "close" => "show", _ => "" });

    public override void OnSettingsChanged()
    {
        lock (_lock)
        {
            if (_endsAt is null && !_finished)
            {
                _left = Duration;
            }
        }

        Show();
    }

    public override void OnStop()
    {
        _tick?.Dispose();
        _tick = null;
    }

    private static TimeSpan Clamp(TimeSpan t) => t > TimeSpan.Zero ? t : TimeSpan.Zero;

    private void Tick()
    {
        bool beep = false;
        lock (_lock)
        {
            if (_endsAt is not { } end)
            {
                return;
            }

            if (DateTime.UtcNow >= end)
            {
                _endsAt = null;
                _left = TimeSpan.Zero;
                _finished = true;
                beep = Context.Settings.Flag("sound", false);
            }
        }

        if (beep)
        {
            MessageBeep(0x40 /* MB_ICONASTERISK: системный звук «Уведомление» */);
        }

        Show();
    }

    private void Show()
    {
        TimeSpan left;
        bool running, finished, visible;
        lock (_lock)
        {
            running = _endsAt is not null;
            left = _endsAt is { } end ? Clamp(end - DateTime.UtcNow) : _left;
            finished = _finished;
            visible = _visible;
        }

        if (!visible)
        {
            Context.HidePanel();
            return;
        }

        var s = Context.Settings;
        string[] t = Texts.TryGetValue(Context.Locale, out var found) ? found : Texts["en"];
        string title = t[0];
        if (!running && !finished && left < Duration)
        {
            title += " · " + t[1];
        }

        // Секунды округляются вверх: 00:00 показывается только в момент окончания.
        var shown = TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds));
        bool warn = running && left.TotalSeconds <= s.Number("warnSeconds", 60);
        string state = finished ? "finished" : warn ? "warning" : "normal";
        double total = Duration.TotalSeconds;
        var buttons = new List<PanelButton>
        {
            running ? new PanelButton("play", "pause", t[2]) : new PanelButton("play", "play", t[3]),
            new PanelButton("reset", "rotate-ccw", t[4]),
            new PanelButton("close", "x", t[5]),
        };
        Context.ShowPanel(new PanelRequest(title, SettingValues.Clock(shown),
            s.Flag("progress", true) && total > 0 ? 1 - (left.TotalSeconds / total) : null,
            s.Number("fontSize", 40), s.Number("opacity", 90) / 100, s.Text("accent", "#31C99A"), state, buttons, Movable: true));
    }

    /// <summary>Подписи панели по языку интерфейса: заголовок, «на паузе», пауза, старт, сброс, скрыть.</summary>
    private static readonly Dictionary<string, string[]> Texts = new()
    {
        ["en"] = ["Timer", "paused", "Pause", "Start", "Reset", "Hide"],
        ["uk"] = ["Таймер", "пауза", "Пауза", "Старт", "Скинути", "Сховати"],
        ["ru"] = ["Таймер", "пауза", "Пауза", "Старт", "Сбросить", "Скрыть"],
        ["de"] = ["Timer", "pausiert", "Pause", "Start", "Zurücksetzen", "Ausblenden"],
        ["fr"] = ["Minuteur", "en pause", "Pause", "Démarrer", "Remettre à zéro", "Masquer"],
        ["es"] = ["Temporizador", "en pausa", "Pausa", "Iniciar", "Reiniciar", "Ocultar"],
        ["it"] = ["Timer", "in pausa", "Pausa", "Avvia", "Azzera", "Nascondi"],
        ["pt"] = ["Temporizador", "em pausa", "Pausa", "Iniciar", "Repor", "Ocultar"],
        ["nl"] = ["Timer", "gepauzeerd", "Pauze", "Start", "Herstellen", "Verbergen"],
        ["pl"] = ["Minutnik", "wstrzymany", "Pauza", "Start", "Resetuj", "Ukryj"],
        ["cs"] = ["Časovač", "pozastaveno", "Pozastavit", "Spustit", "Vynulovat", "Skrýt"],
        ["hu"] = ["Időzítő", "szünetel", "Szünet", "Indítás", "Visszaállítás", "Elrejtés"],
        ["ro"] = ["Cronometru", "în pauză", "Pauză", "Pornire", "Resetare", "Ascundere"],
        ["sv"] = ["Timer", "pausad", "Pausa", "Starta", "Återställ", "Dölj"],
        ["da"] = ["Timer", "sat på pause", "Pause", "Start", "Nulstil", "Skjul"],
        ["nb"] = ["Tidtaker", "satt på pause", "Pause", "Start", "Tilbakestill", "Skjul"],
        ["fi"] = ["Ajastin", "tauolla", "Tauko", "Käynnistä", "Nollaa", "Piilota"],
        ["lt"] = ["Laikmatis", "pristabdyta", "Pristabdyti", "Paleisti", "Atkurti", "Slėpti"],
        ["el"] = ["Χρονόμετρο", "σε παύση", "Παύση", "Έναρξη", "Επαναφορά", "Απόκρυψη"],
    };

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint type);
}
