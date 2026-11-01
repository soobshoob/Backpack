using System.IO;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

/// <summary>
/// Phase 2D, падение «открытие раздела „Клавиши и сочетания“ завершает программу».
/// Причина: превью разделов «Внешний вид» и «Клавиши и сочетания» использует один и тот же элемент группы кейкапов;
/// при переходе он вставлялся в новый контейнер, оставаясь дочерним элементом прежнего → InvalidOperationException.
/// </summary>
public class CrashRegressionTests
{
    private static SettingsStore TempStore() => SettingsStore.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));

    [Fact]
    public void OpeningKeysSection_AfterAppearance_DoesNotThrow()
    {
        Sta.Run(() =>
        {
            var w = new SettingsWindow(TempStore(), new SettingsWindowActions(() => { }, () => { }, () => ""));
            w.Show(SettingsSection.Keys);
            w.Show(SettingsSection.Appearance);
            w.Show(SettingsSection.Keys);
            w.ReleaseResources();
            w.Close();
        });
    }
}
