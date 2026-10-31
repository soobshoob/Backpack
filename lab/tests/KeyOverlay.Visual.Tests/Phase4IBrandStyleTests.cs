using System.IO;
using System.Windows;
using KeyOverlay.Visual.Rendering;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Styles;

namespace KeyOverlay.Visual.Tests;

/// <summary>
/// Phase 4I, п. 16: фирменные пресеты «Backpack Neon Emerald». Чистая установка и новый профиль получают их; существующие
/// настройки (в том числе без новых полей) сохраняют выбранные стили; сброс раздела возвращает фирменный пресет; встроенные
/// эталоны неизменяемы; свечение сохраняется и не обрезается краем окна; тема интерфейса не перекрашивает стиль.
/// </summary>
public class Phase4IBrandStyleTests
{
    [Fact]
    public void Clean_install_selects_brand_keycaps_and_mouse_without_touching_tools()
    {
        var store = SettingsStore.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));
        Assert.Equal(BrandStyles.KeycapId, store.Current.Appearance.KeycapStyleId);
        Assert.Equal(BrandStyles.MouseId, store.Current.Appearance.MouseLook.StyleId);
        Assert.Equal(KeycapRenderer.Neon, store.Current.Appearance.Renderer);
        Assert.Equal(MouseRenderer.Neon, store.Current.Appearance.MouseLook.Renderer);
        // Только стили: остальные разделы — прежние значения по умолчанию.
        Assert.Equal(LabSettings.Default.Keys, store.Current.Keys);
        Assert.Equal(LabSettings.Default.MouseCursor, store.Current.MouseCursor);
        Assert.Equal(LabSettings.Default.Placement, store.Current.Placement);
        Assert.True(StyleOps.Matches(store.Current, BuiltInStyles.Find(BrandStyles.KeycapId)!));
        Assert.True(StyleOps.Matches(store.Current, BuiltInStyles.Find(BrandStyles.MouseId)!));
    }

    [Fact]
    public void Existing_settings_keep_their_styles_including_files_without_new_fields()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bp-brand-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            // Файл прежней версии: классический lowprofile и контурная мышь; полей renderer neon и glow в нём нет.
            var old = StyleOps.Apply(StyleOps.Apply(LabSettings.Default, BuiltInStyles.Find("keyoverlay.pbt.ocean")!), BuiltInStyles.Find("keyoverlay.mouse.retro-sage")!);
            var json = System.Text.Json.Nodes.JsonNode.Parse(SettingsSerializer.Write(old))!.AsObject();
            json["appearance"]!["border"]!.AsObject().Remove("glow");
            string path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, json.ToJsonString());
            var store = SettingsStore.Load(path);
            Assert.Empty(store.LoadProblems);
            Assert.Equal("keyoverlay.pbt.ocean", store.Current.Appearance.KeycapStyleId);
            Assert.Equal(KeycapRenderer.Pbt, store.Current.Appearance.Renderer);
            Assert.Equal(0, store.Current.Appearance.Glow);
            Assert.Equal("keyoverlay.mouse.retro-sage", store.Current.Appearance.MouseLook.StyleId);
            Assert.Equal(MouseRenderer.Retro, store.Current.Appearance.MouseLook.Renderer);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Glow_round_trips_in_settings_and_style_files_and_zero_turns_it_off()
    {
        var s = BrandStyles.Starter with { Appearance = BrandStyles.Starter.Appearance with { Glow = 0.35, Mouse = BrandStyles.Starter.Appearance.MouseLook with { Glow = 0.8 } } };
        var back = SettingsSerializer.Read(SettingsSerializer.Write(s));
        Assert.Empty(back.Problems);
        Assert.Equal(0.35, back.Settings.Appearance.Glow);
        Assert.Equal(0.8, back.Settings.Appearance.MouseLook.Glow);

        var keycap = StyleOps.Capture(s, StyleKind.Keycap, "u-neon000001", "Mine", BrandStyles.KeycapId);
        var read = StyleSerializer.Read(StyleSerializer.Write(keycap));
        Assert.True(read.Ok, string.Join("; ", read.Errors));
        Assert.Equal(0.35, read.Style!.Keycap!.Glow);
        Assert.Equal(KeycapRenderer.Neon, read.Style.Keycap.Renderer);

        Sta.Run(() =>
        {
            var k = (NeonKeycap)KeycapFactory.Create(KeycapRenderer.Neon);
            k.Apply(BrandStyles.Keycap, new KeycapItem(1, 1, "A", false, false), showBadge: false);
            Assert.True(k.GlowVisible);
            double idle = k.GlowRadius;
            k.PressOffset = k.PressTravel;
            Assert.True(k.GlowRadius > idle);                                   // нажатие — свечение сильнее
            k.PressOffset = 0;
            Assert.Equal(idle, k.GlowRadius, 3);                                // отпускание — обычное состояние
            k.Apply(BrandStyles.Keycap with { Glow = 0 }, new KeycapItem(1, 1, "A", false, false), showBadge: false);
            Assert.False(k.GlowVisible);
        });
    }

    [Fact]
    public void Glow_fits_into_the_overlay_margin_at_any_size()
    {
        foreach (double size in new[] { 8.0, 16, 32, 64, 128 })
        {
            var m = KeycapMetrics.From(BrandStyles.Keycap with { Size = size, Glow = 1 });
            Assert.True(m.OuterMargin >= m.GlowBlur(1), $"S={size}");
        }

        Assert.Equal(KeycapMetrics.From(AppearanceSettings.Default).OuterMargin, KeycapMetrics.From(AppearanceSettings.Default with { Glow = 1 }).OuterMargin); // другие рендеры не меняются
    }

    [Fact]
    public void Built_in_brand_presets_are_immutable_and_ui_theme_does_not_recolor_them()
    {
        var before = BuiltInStyles.Find(BrandStyles.KeycapId)!;
        Assert.True(before.BuiltIn);
        Assert.True(StyleIds.IsBuiltIn(BrandStyles.KeycapId));
        Assert.True(StyleIds.IsBuiltIn(BrandStyles.MouseId));
        Assert.Equal("Backpack Neon Emerald", BuiltInStyles.LocalizedName(before));
        // Значения — константы стиля, а не ресурсы темы: переключение палитры интерфейса их не меняет.
        Sta.Run(() =>
        {
            UiTheme.Set(UiThemeMode.Light);
            var light = BuiltInStyles.Find(BrandStyles.KeycapId)!.Keycap;
            UiTheme.Set(UiThemeMode.Dark);
            Assert.Equal(light, BuiltInStyles.Find(BrandStyles.KeycapId)!.Keycap);
        });
    }
}

/// <summary>
/// Phase 4I, п. 17: значок программы — знак сайта. Контуры совпадают с исходником, которым сайт рисует логотип
/// (lucide-react 0.468.0, icons/backpack.js), а ICO в ресурсах приложения собраны из этих же контуров.
/// </summary>
public class Phase4IAppIconTests
{
    [Fact]
    public void Logo_geometry_is_the_website_mark_unchanged()
    {
        string site = Repo.PathOf("Website", "node_modules", "lucide-react", "dist", "esm", "icons", "backpack.js");
        string app = Repo.PathOf("Website", "src", "App.tsx");
        Assert.Contains("import {Backpack", File.ReadAllText(app));                 // шапка и подвал сайта рисуют этот значок
        if (!File.Exists(site))
        {
            return; // зависимости сайта не установлены — сравнение с исходником невозможно (контуры ниже зафиксированы)
        }

        var d = System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(site), "d: \"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(d, KeyOverlay.Visual.Brand.LogoArt.Spec.SitePaths);
        Assert.Equal(1.5, KeyOverlay.Visual.Brand.LogoArt.Spec.Full.Stroke);       // .brand>svg { stroke-width: 1.5 }
    }

    [Fact]
    public void App_icon_resources_are_built_from_the_current_mark()
    {
        Sta.Run(() =>
        {
            var expected = KeyOverlay.Visual.Brand.LogoArt.Ico(KeyOverlay.Visual.Brand.LogoArt.IconSizes);
            var actual = File.ReadAllBytes(Repo.PathOf("lab", "src", "KeyOverlay.Lab.App", "Assets", "Backpack.ico"));
            Assert.Equal(expected.Length, actual.Length);
            Assert.True(expected.AsSpan().SequenceEqual(actual), "Assets/Backpack.ico устарел: перегенерируйте --lab --render-brand");
        });
    }
}
