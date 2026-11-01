using System.IO;
using Backpack.Visual.Settings;
using Backpack.Visual.Styles;
using Microsoft.Win32;

namespace Backpack.Visual.Tests;

/// <summary>
/// 0.4.0-preview.7: автозапуск с Windows (настоящее состояние записи, другая копия, перенесённая папка, отключение в Windows) и
/// оформление чистой установки Backpack Neon Emerald (тема, прозрачность, Spectral, кейкапы и мышь) без перезаписи сохранённого
/// выбора. Реестр — собственный временный раздел теста, не настоящий автозапуск.
/// </summary>
public class Phase4JTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bp-4j-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _key = @"Software\Backpack.Tests\" + Guid.NewGuid().ToString("N")[..8];

    public Phase4JTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_key, throwOnMissingSubKey: false);
        Directory.Delete(_root, true);
    }

    private string Exe(string folder)
    {
        string dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        string exe = Path.Combine(dir, "Backpack.exe");
        File.WriteAllText(exe, "");
        return exe;
    }

    private Autostart For(string exe) => new(exe, _key + @"\Run", _key + @"\Approved");

    private string? RunValue()
    {
        using var k = Registry.CurrentUser.OpenSubKey(_key + @"\Run");
        return k?.GetValue(Autostart.ValueName) as string;
    }

    private void SetRun(string command)
    {
        using var k = Registry.CurrentUser.CreateSubKey(_key + @"\Run");
        k.SetValue(Autostart.ValueName, command);
    }

    private void SetApproved(byte first)
    {
        using var k = Registry.CurrentUser.CreateSubKey(_key + @"\Approved");
        k.SetValue(Autostart.ValueName, new byte[12] { first, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
    }

    [Fact]
    public void Enable_and_disable_write_the_quoted_command_of_this_copy()
    {
        var a = For(Exe("Program Files"));
        Assert.Equal(AutostartState.Off, a.Status().State);
        a.Enable();
        Assert.Equal($"\"{a.ExePath}\" --autostart", RunValue());
        Assert.Equal(AutostartState.On, a.Status().State);
        Assert.Equal(a.ExePath, a.Status().RegisteredPath);
        a.Disable();
        Assert.Null(RunValue());
        Assert.Equal(AutostartState.Off, a.Status().State);
        a.Disable(); // повторное выключение — без ошибки
    }

    [Fact]
    public void Another_existing_copy_is_reported_and_not_taken_over_by_repair()
    {
        var other = For(Exe("other"));
        other.Enable();
        var mine = For(Exe("mine"));
        Assert.Equal(AutostartState.OtherCopy, mine.Status().State);
        Assert.False(mine.Repair());
        Assert.Equal(other.Command, RunValue());
        mine.Enable(); // явное включение здесь — автозапуск этой копии
        Assert.Equal(AutostartState.On, mine.Status().State);
        Assert.Equal(AutostartState.OtherCopy, other.Status().State);
    }

    [Fact]
    public void Moved_folder_or_stale_command_is_repaired_to_this_copy()
    {
        SetRun($"\"{Path.Combine(_root, "gone", "Backpack.exe")}\" --autostart");
        var a = For(Exe("new"));
        Assert.Equal(AutostartState.Missing, a.Status().State);
        Assert.True(a.Repair());
        Assert.Equal(AutostartState.On, a.Status().State);
        Assert.False(a.Repair()); // уже верно — без записи

        SetRun($"\"{a.ExePath}\""); // эта копия, но без --autostart (например, запись прежней версии)
        Assert.True(a.Repair());
        Assert.Equal(a.Command, RunValue());
    }

    [Fact]
    public void Disabled_in_Windows_is_shown_and_turning_on_here_enables_it_again()
    {
        var a = For(Exe("app"));
        a.Enable();
        SetApproved(3); // «Автозагрузка» диспетчера задач: отключено
        Assert.Equal(AutostartState.DisabledInWindows, a.Status().State);
        Assert.False(a.Repair()); // отключение пользователя в Windows не снимается само
        a.Enable();
        Assert.Equal(AutostartState.On, a.Status().State);
        SetApproved(6); // другой вариант «включено»
        Assert.Equal(AutostartState.On, a.Status().State);
        a.Disable();
        using var k = Registry.CurrentUser.OpenSubKey(_key + @"\Approved");
        Assert.Null(k?.GetValue(Autostart.ValueName));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\Backpack\\Backpack.exe\" --autostart", "C:\\Program Files\\Backpack\\Backpack.exe")]
    [InlineData("C:\\Program Files\\Backpack\\Backpack.exe --autostart", "C:\\Program Files\\Backpack\\Backpack.exe")]
    [InlineData("  \"D:\\Apps\\Backpack.exe\"", "D:\\Apps\\Backpack.exe")]
    public void Command_path_is_parsed(string command, string path) => Assert.Equal(path, Autostart.PathOf(command));

    [Fact]
    public void Settings_toggle_shows_the_real_state_and_changes_it()
    {
        var a = For(Exe("ui"));
        Sta.Run(() =>
        {
            var store = SettingsStore.Load(Path.Combine(_root, "settings.json"));
            var prefs = AppPreferencesStore.Load(Path.Combine(_root, "app.json"));
            var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => "", Autostart: a), SettingsSection.Application, prefs: prefs);
            Assert.False(w.AutostartShown);
            w.ClickAutostart();
            Assert.Equal(AutostartState.On, a.Status().State);
            Assert.True(w.AutostartShown);
            a.Disable(); // изменено вне окна (например, в «Автозагрузке» Windows)
            w.RefreshAutostart();
            Assert.False(w.AutostartShown);
            w.ReleaseResources();
            w.Close();
        });
    }

    [Fact]
    public void Clean_install_uses_Backpack_Neon_Emerald_with_Spectral()
    {
        var prefs = AppPreferencesStore.Load(Path.Combine(_root, "app.json")).Current;
        Assert.Equal(UiThemeMode.System, prefs.Theme);                    // «как у системы»
        Assert.Equal("emerald", prefs.ColorTheme);
        Assert.Equal(SurfaceStyle.Neon, prefs.MaterialOrDefault.Surface);
        Assert.Equal(0.35 * 1.25, prefs.MaterialOrDefault.Intensity, 6);   // +25 % к прежним 0,35

        var s = SettingsStore.Load(Path.Combine(_root, "settings.json")).Current;
        Assert.Equal(FontSettings.Spectral, s.FontsOrDefault.Ui);
        Assert.Equal(FontSettings.Spectral, s.FontsOrDefault.Keycap);
        Assert.Equal(KeycapRenderer.Neon, s.Appearance.Renderer);
        Assert.Equal(BrandStyles.KeycapId, s.Appearance.KeycapStyleId);
        Assert.Equal(MouseRenderer.Neon, s.Appearance.MouseLook.Renderer);
        Assert.Equal(BrandStyles.MouseId, s.Appearance.MouseLook.StyleId);
        Assert.Equal(BrandStyles.Mouse.ClickColor, s.Appearance.MouseLook.ClickColor); // круг щелчка — изумрудный, как мышь
    }

    [Fact]
    public void Saved_choices_of_an_existing_user_are_kept_after_update()
    {
        string appPath = Path.Combine(_root, "app.json"), settingsPath = Path.Combine(_root, "settings.json");
        var chosen = AppPreferences.Default with
        {
            Theme = UiThemeMode.Light,
            ColorTheme = "classic",
            Material = new AppearanceMaterial(WindowMaterial.Opaque, 0.1, false, 0.2, SurfaceStyle.Standard),
        };
        File.WriteAllText(appPath, AppPreferencesStore.Write(chosen));
        var mine = BrandStyles.Starter with { Fonts = FontSettings.Default };
        mine = StyleOps.Apply(mine, BuiltInStyles.Find("backpack.pbt.ocean")!);
        File.WriteAllText(settingsPath, SettingsSerializer.Write(mine));

        var prefs = AppPreferencesStore.Load(appPath).Current;   // запуск новой версии
        Assert.Equal(UiThemeMode.Light, prefs.Theme);
        Assert.Equal("classic", prefs.ColorTheme);
        Assert.Equal(SurfaceStyle.Standard, prefs.MaterialOrDefault.Surface);
        Assert.Equal(0.1, prefs.MaterialOrDefault.Intensity, 6);
        var s = SettingsStore.Load(settingsPath).Current;
        Assert.Null(s.FontsOrDefault.Ui);
        Assert.Null(s.FontsOrDefault.Keycap);
        Assert.Equal("backpack.pbt.ocean", s.Appearance.KeycapStyleId);
    }

    [Fact]
    public void Foreign_format_files_get_new_defaults_and_are_kept_as_backups()
    {
        string appPath = Path.Combine(_root, "app.json"), settingsPath = Path.Combine(_root, "settings.json");
        File.WriteAllText(appPath, "{\"format\":\"other-app\",\"formatVersion\":5}");
        File.WriteAllText(settingsPath, "{\"format\":\"other-settings\",\"formatVersion\":9}");

        var prefs = AppPreferencesStore.Load(appPath);
        Assert.True(prefs.Unreadable);
        Assert.Equal(SurfaceStyle.Neon, prefs.Current.MaterialOrDefault.Surface);
        prefs.Update(p => p with { HomeAnimation = false });
        Assert.Equal("{\"format\":\"other-app\",\"formatVersion\":5}", File.ReadAllText(Path.Combine(_root, "app.unreadable.bak.json")));

        var store = SettingsStore.Load(settingsPath);
        Assert.Equal(FontSettings.Spectral, store.Current.FontsOrDefault.Ui); // как при чистой установке
        Assert.Equal(BrandStyles.KeycapId, store.Current.Appearance.KeycapStyleId);
        store.Update(x => x with { Keys = x.Keys with { LingerMs = x.Keys.LingerMs + 100 } });
        store.Flush();
        Assert.Equal("{\"format\":\"other-settings\",\"formatVersion\":9}", File.ReadAllText(Path.Combine(_root, "settings.unreadable.bak.json")));
    }
}
