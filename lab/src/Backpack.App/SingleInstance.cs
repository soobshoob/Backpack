using System.Security.Cryptography;
using System.Text;

namespace Backpack.App;

/// <summary>
/// Один экземпляр Backpack на папку данных: два процесса с одними settings.json, профилями и плагинами мешали бы друг другу
/// (и дважды ставили бы глобальные хуки). Именованный мьютекс сеанса (<c>Local\</c>) с хешем папки данных — копия с отдельной
/// папкой (<c>--settings</c>, проверка пакета) запускается рядом, а повторный запуск той же копии или автозапуск при уже
/// работающей программе — нет. Повторный обычный запуск просит первый экземпляр показать главное окно (именованное событие);
/// повторный автозапуск просто завершается.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _show;
    private RegisteredWaitHandle? _wait;

    private SingleInstance(Mutex mutex, EventWaitHandle show)
    {
        _mutex = mutex;
        _show = show;
    }

    public static string NameFor(string dataDirectory)
    {
        string key = System.IO.Path.GetFullPath(dataDirectory).TrimEnd('\\').ToUpperInvariant();
        return @"Local\Backpack-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
    }

    /// <summary>
    /// Захватить экземпляр для папки данных. null — уже запущен другой процесс; тогда при <paramref name="showExisting"/> ему
    /// отправляется просьба показать главное окно.
    /// </summary>
    public static SingleInstance? TryAcquire(string dataDirectory, bool showExisting)
    {
        string name = NameFor(dataDirectory);
        var mutex = new Mutex(initiallyOwned: true, name, out bool createdNew);
        var show = new EventWaitHandle(false, EventResetMode.AutoReset, name + "-show");
        if (createdNew)
        {
            return new SingleInstance(mutex, show);
        }

        if (showExisting)
        {
            AllowSetForegroundWindow(-1); // ASFW_ANY: этот процесс запустил пользователь — первый может вывести окно вперёд
            show.Set();
        }

        show.Dispose();
        mutex.Dispose();
        return null;
    }

    /// <summary>Просьба второго экземпляра показать окно: <paramref name="onShow"/> вызывается в потоке пула.</summary>
    public void Listen(Action onShow) =>
        _wait = ThreadPool.RegisterWaitForSingleObject(_show, (_, _) => onShow(), null, Timeout.Infinite, executeOnlyOnce: false);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

    public void Dispose()
    {
        _wait?.Unregister(null);
        _wait = null;
        _show.Dispose();
        _mutex.Dispose(); // владение снимает завершение процесса; освобождение из другого потока не требуется
    }
}
