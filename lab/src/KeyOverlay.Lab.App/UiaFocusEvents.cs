using System.Windows.Automation;

namespace KeyOverlay.Lab.App;

/// <summary>
/// Подписка на событие изменения фокуса UI Automation (AutomationFocusChangedEvent). Нужна в дополнение к WinEvent:
/// провайдеры (в том числе WPF) поднимают события фокуса UIA только при наличии UIA-подписчика, а WinEvent
/// EVENT_OBJECT_FOCUS при смене элемента внутри одного окна приходит не всегда. Обработчик ничего не читает у элемента:
/// только сообщает о смене контекста (проверка IsPassword — в рабочем потоке <see cref="KeyOverlay.Lab.Win32.FocusPrivacySource"/>).
/// Регистрация и снятие выполняются вне UI-потока (рекомендация UIA для клиентов, у которых есть собственный UI).
/// </summary>
internal sealed class UiaFocusEvents : IDisposable
{
    private readonly Action _changed;
    private readonly AutomationFocusChangedEventHandler _handler;
    private Task? _registration;
    private volatile bool _disposed;

    public UiaFocusEvents(Action changed)
    {
        _changed = changed;
        _handler = OnFocusChanged;
    }

    public bool Registered { get; private set; }

    /// <summary>
    /// Регистрация завершена (успешно или нет). До этого смена фокуса внутри окна WPF может не порождать событий, поэтому
    /// «обычное поле» не подтверждается (причина нестабильной самопроверки Phase 4A).
    /// </summary>
    public bool Ready { get; private set; }

    /// <summary>Регистрация завершена — вызывается в фоновом потоке (вызывающий запрашивает проверку заново).</summary>
    public event Action? ReadyChanged;

    public void Start() => _registration = Task.Run(() =>
    {
        try
        {
            Automation.AddAutomationFocusChangedEventHandler(_handler);
            Registered = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ElementNotAvailableException or System.Runtime.InteropServices.COMException)
        {
            Registered = false; // остаётся WinEvent; при отсутствии событий проверка по окну с фокусом в DrainAll
        }
        finally
        {
            Ready = true;
            if (!_disposed)
            {
                ReadyChanged?.Invoke();
            }
        }
    });

    private void OnFocusChanged(object? sender, AutomationFocusChangedEventArgs e)
    {
        if (!_disposed)
        {
            _changed();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var reg = _registration;
        // Снятие — в фоне с ограниченным ожиданием: зависший провайдер не задерживает выход.
        var remove = Task.Run(() =>
        {
            try
            {
                reg?.Wait(TimeSpan.FromSeconds(1));
                if (Registered)
                {
                    Automation.RemoveAutomationFocusChangedEventHandler(_handler);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ElementNotAvailableException or System.Runtime.InteropServices.COMException or AggregateException)
            {
            }
        });
        remove.Wait(TimeSpan.FromMilliseconds(800));
    }
}
