namespace KeyOverlay.Lab.Core.Privacy;

/// <summary>Что UI Automation сообщила о свойстве IsPassword сфокусированного элемента.</summary>
public enum PasswordProperty : byte
{
    /// <summary>Элемента нет или вызов завершился ошибкой.</summary>
    Error = 0,

    /// <summary>Провайдер не поддерживает свойство (ReservedNotSupportedValue).</summary>
    NotSupported = 1,

    False = 2,
    True = 3,
}

/// <summary>
/// Метаданные контекста фокуса для решения приватности: только классы окон, имя исполняемого файла, тип элемента UIA и
/// фреймворк. Имена элементов, значения, текст и заголовки окон не собираются.
/// </summary>
/// <param name="ControlType">UIA_ControlTypePropertyId (например, 50008 — List); 0 — неизвестно.</param>
public readonly record struct FocusFacts(
    PasswordProperty Password,
    int ControlType = 0,
    string? FrameworkId = null,
    string? FocusClass = null,
    string? FocusParentClass = null,
    string? RootClass = null,
    bool RootIsForeground = false,
    string? Process = null);

/// <summary>
/// Перевод ответа UIA в решение приватности (чистая логика, проверяется тестами).
/// <para>
/// <b>Рабочий стол Windows.</b> Список значков рабочего стола — стандартный Win32 ListView (<c>SysListView32</c>) внутри
/// <c>SHELLDLL_DefView</c> окна <c>Progman</c> или <c>WorkerW</c> процесса explorer.exe. Его прокси UIA (MSAA → UIA) не реализует
/// IsPassword: ответ «не поддерживается», и раньше это давало «нет данных» — новый ввод скрывался, а событий фокуса на рабочем
/// столе больше не приходило, поэтому защита «залипала» до перехода в другое окно. Этот список не принимает ввод текста (поле
/// переименования значка — отдельный элемент Edit со своим событием фокуса и своим IsPassword), поэтому в этом и только в этом
/// контексте отсутствие свойства означает «обычный элемент». Остальной Explorer, панель задач и прочие системные окна
/// не считаются безопасными: для них «не поддерживается» по-прежнему означает «нет данных».
/// </para>
/// </summary>
public static class FocusClassifier
{
    public const int ControlTypeList = 50008;
    public const int ControlTypeListItem = 50007;

    public static ProbeVerdict Classify(in FocusFacts f) => f.Password switch
    {
        PasswordProperty.True => ProbeVerdict.Password,
        PasswordProperty.False => ProbeVerdict.NotPassword,
        PasswordProperty.NotSupported when IsDesktopIconList(f) => ProbeVerdict.NotPassword,
        _ => ProbeVerdict.NoData,
    };

    /// <summary>Фокус в списке значков рабочего стола Windows (все признаки должны совпасть).</summary>
    public static bool IsDesktopIconList(in FocusFacts f) =>
        f.ControlType is ControlTypeList or ControlTypeListItem
        && string.Equals(f.FrameworkId, "Win32", StringComparison.Ordinal)
        && string.Equals(f.FocusClass, "SysListView32", StringComparison.Ordinal)
        && string.Equals(f.FocusParentClass, "SHELLDLL_DefView", StringComparison.Ordinal)
        && f.RootClass is "Progman" or "WorkerW"
        && f.RootIsForeground
        && string.Equals(f.Process, "explorer.exe", StringComparison.OrdinalIgnoreCase);
}
