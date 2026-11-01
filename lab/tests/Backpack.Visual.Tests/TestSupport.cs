using System.IO;
using System.Runtime.ExceptionServices;
using Backpack.Visual.Rendering;

// Проверки утечек читают общие счётчики процесса (подписки на CompositionTarget.Rendering по всем потокам),
// поэтому тесты этой сборки выполняются последовательно.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Backpack.Visual.Tests;

internal static class Sta
{
    /// <summary>
    /// Схема pack:// регистрируется WPF лениво (при первом обращении к Application/PackUriHelper); без неё ресурсы шрифтов
    /// (VisualFonts) не разбираются. Регистрация здесь делает любой отдельный тест независимым от порядка запуска.
    /// </summary>
    static Sta() => _ = System.IO.Packaging.PackUriHelper.UriSchemePack;

    /// <summary>WPF-элементы создаются и проверяются в отдельном STA-потоке (окна на экран не выводятся).</summary>
    public static void Run(Action action)
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error is not null)
        {
            ExceptionDispatchInfo.Throw(error);
        }
    }
}

internal sealed class FakeClock : IAnimationClock
{
    public TimeSpan Now { get; set; }

    public void Advance(double ms) => Now += TimeSpan.FromMilliseconds(ms);
}

internal static class Repo
{
    public static string Root { get; } = Find();

    public static string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "themes")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Не найден корень репозитория (docs/themes)");
    }
}
