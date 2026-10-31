namespace KeyOverlay.Visual.Rendering;

/// <summary>
/// Что показать для одной записи модели отображения. Визуальный слой не знает о захвате и модели ввода.
/// <see cref="Id"/> — идентичность появления (новая при каждом создании записи). <see cref="MatchKey"/> — физическая клавиша:
/// по нему исчезающий кейкап «оживает», если та же клавиша появилась снова, пока шла анимация исчезновения.
/// <see cref="Key"/> — семантическое имя клавиши («Ctrl», «Enter», «ArrowUp», «MouseLeft», «A», «7»…); подпись и символ
/// выбирает <see cref="KeyLabels"/> по набору обозначений. <see cref="Secondary"/> — дополнительная подпись раскладки
/// (левый верхний угол символьной клавиши, см. <see cref="Layout.SecondaryLabels"/>).
/// </summary>
public sealed record KeycapItem(
    long Id,
    int MatchKey,
    string Key,
    bool IsModifier,
    bool IsPressed,
    KeySide Side = KeySide.None,
    bool Numpad = false,
    bool IsUnknown = false,
    int PressCount = 1,
    string? Secondary = null)
{
    public bool IsMouse => KeyLabels.IsMouse(Key);
}

public interface IAnimationClock
{
    TimeSpan Now { get; }
}

public sealed class StopwatchClock : IAnimationClock
{
    private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

    public TimeSpan Now => _sw.Elapsed;
}
